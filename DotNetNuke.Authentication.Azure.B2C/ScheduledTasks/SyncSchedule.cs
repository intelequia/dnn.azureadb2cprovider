using Azure.Core;
using DotNetNuke.Authentication.Azure.B2C.Common;
using DotNetNuke.Authentication.Azure.B2C.Components;
using DotNetNuke.Authentication.Azure.B2C.Components.Graph;
using DotNetNuke.Authentication.Azure.B2C.Components.Models;
using DotNetNuke.Authentication.Azure.B2C.Data;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Data;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Users;
using DotNetNuke.Instrumentation;
using DotNetNuke.Security.Roles;
using DotNetNuke.Services.Authentication;
using DotNetNuke.Services.FileSystem;
using DotNetNuke.Services.Scheduling;
using DotNetNuke.UI.UserControls;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System.Web.Security;
using DotNetNuke.Web.InternalServices;

namespace DotNetNuke.Authentication.Azure.B2C.ScheduledTasks
{
    public class SyncSchedule : SchedulerClient
    {
        private static readonly ILog Logger = LoggerSource.Instance.GetLogger(typeof(SyncSchedule));

        private List<RoleMapping> _globalRoleMappings;
        public List<RoleMapping> GlobalRoleMappings
        {
            get
            {
                if (_globalRoleMappings == null)
                {
                    _globalRoleMappings = RoleMappingsRepository.Instance.GetRoleMappings(-1).ToList();
                }
                return _globalRoleMappings;
            }
        }

        public List<RoleMapping> GetRoleMappingsForPortal(int portalId, AzureConfig settings)
        {
            if (settings.UseGlobalSettings)
                return GlobalRoleMappings;
            else
                return RoleMappingsRepository.Instance.GetRoleMappings(portalId).ToList();
        }

        public SyncSchedule(ScheduleHistoryItem oItem) : base()
        {
            ScheduleHistoryItem = oItem;
        }

        public override void DoWork()
        {
            try
            {
                //Perform required items for logging
                Progressing();

                ScheduleHistoryItem.AddLogNote("Starting Azure AD B2C Synchronization.\n");

                // Run optional pre-sync stored procedure
                RunStoredProcedureIfExists($"{AzureConfig.ServiceName}_BeforeScheduledSync");

                var portals = PortalController.Instance.GetPortalList(Null.NullString);
                foreach (var portal in portals)
                {
                    var settings = new AzureConfig(AzureConfig.ServiceName, portal.PortalID);
                    if (settings.Enabled)
                    {
                        if (settings.RoleSyncEnabled)
                        {
                            var message = SyncRoles(portal.PortalID, settings);
                            ScheduleHistoryItem.AddLogNote(message);
                        }
                        if (settings.UserSyncEnabled)
                        {
                            var message = SyncUsers(portal.PortalID, settings);
                            ScheduleHistoryItem.AddLogNote(message);
                        }
                        if (settings.ExpiredRolesSyncEnabled)
                        {
                            var message = SyncExpiredRoles(portal.PortalID, settings);
                            ScheduleHistoryItem.AddLogNote(message);
                        }
                    }
                }

                // Run optional post-sync stored procedure
                RunStoredProcedureIfExists($"{AzureConfig.ServiceName}_AfterScheduledSync");

                ScheduleHistoryItem.AddLogNote(string.Format("Azure AD B2C Synchronization finished successfully.\n"));

                //Show success
                ScheduleHistoryItem.Succeeded = true;
            }
            catch (Exception ex)
            {
                ScheduleHistoryItem.Succeeded = false;
                ScheduleHistoryItem.AddLogNote(string.Format("Error performing Azure AD B2C Synchronization: {0}.\n", ex.Message));

                Errored(ref ex);
                DotNetNuke.Services.Exceptions.Exceptions.LogException(ex);
            }
        }

        private void RunStoredProcedureIfExists(string storedProcedureName)
        {
            try
            {
                using (IDataContext ctx = DataContext.Instance())
                {
                    var query = $"IF OBJECT_ID(N'[{storedProcedureName}]', N'P') IS NOT NULL BEGIN EXEC {storedProcedureName} END";
                    ctx.Execute(System.Data.CommandType.Text, query);
                }
            }
            catch (Exception ex)
            {
                DotNetNuke.Services.Exceptions.Exceptions.LogException(ex);
            }
        }

        private int AddRole(int portalId, string roleName, string roleDescription, bool isFromB2c)
        {
            if (string.IsNullOrEmpty(roleName))
            {
                throw new ArgumentNullException(nameof(roleName));
            }
            int roleGroupID = AzureClient.GetOrCreateRoleGroup(ref roleDescription);

            var roleId = RoleController.Instance.AddRole(new RoleInfo
            {
                RoleName = roleName,
                Description = roleDescription,
                PortalID = portalId,
                Status = RoleStatus.Approved,
                RoleGroupID = roleGroupID,
                AutoAssignment = false,
                IsPublic = false
            });

            if (isFromB2c)
            {
                var role = RoleController.Instance.GetRoleById(portalId, roleId);
                role.Settings.Add(AzureClient.RoleSettingsB2cPropertyName, AzureClient.RoleSettingsB2cPropertyValue);
                RoleController.Instance.UpdateRoleSettings(role, true);
            }

            return roleId;
        }

        private List<RoleInfo> GetDnnB2cRoles(int portalId)
        {
            // This is the list of DNN roles coming from B2C
            return RoleController.Instance.GetRoles(portalId)
                .Where(r => r.Settings.ContainsKey(AzureClient.RoleSettingsB2cPropertyName) && r.Settings[AzureClient.RoleSettingsB2cPropertyName] == AzureClient.RoleSettingsB2cPropertyValue)
                .ToList();
        }

        internal string SyncRoles(int portalId, AzureConfig settings)
        {
            try
            {
                var syncErrorsDesc = "";
                var syncErrors = 0;
                var groupsCreated = 0;
                var groupsDeleted = 0;
                var customRoleMappings = GetRoleMappingsForPortal(portalId, settings);

                if (string.IsNullOrEmpty(settings.AADApplicationId) || string.IsNullOrEmpty(settings.AADApplicationKey))
                {
                    throw new Exception($"AAD application ID or key are not valid on portal {portalId}");
                }

                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);
                // Add roles from AAD B2C
                var aadGroups = graphClient.GetAllGroups();
                var allaadGroups = new List<Microsoft.Graph.Group>();
                if (aadGroups != null)
                {
                    var groupPrefix = settings.GroupNamePrefixEnabled ? $"{AzureConfig.ServiceName}-" : "";
                    while (aadGroups != null && aadGroups.Count > 0)
                    {
                        var groups = aadGroups.CurrentPage.ToList();
                        allaadGroups.AddRange(groups);
                        if (customRoleMappings != null && customRoleMappings.Count > 0)
                        {
                            groupPrefix = "";
                            var b2cRoles = customRoleMappings.Select(rm => rm.B2cRoleName);
                            groups.RemoveAll(x => !b2cRoles.Contains(x.DisplayName));
                        }

                        foreach (var aadGroup in groups)
                        {
                            var displayName = $"{groupPrefix}{aadGroup.DisplayName}";
                            var mapping = customRoleMappings?.FirstOrDefault(x => x.B2cRoleName == aadGroup.DisplayName);
                            if (mapping != null)
                            {
                                displayName = mapping.DnnRoleName;
                            }

                            var dnnRole = RoleController.Instance.GetRoleByName(portalId, displayName);
                            if (dnnRole == null)
                            {
                                try
                                {
                                    // Create role
                                    var roleId = AddRole(portalId, displayName, aadGroup.Description, true);
                                    groupsCreated++;
                                }
                                catch (Exception ex)
                                {
                                    syncErrors++;
                                    syncErrorsDesc += $"\n{ex.Message}";
                                }
                            }
                            else
                            {
                                AzureClient.UpdateRoleGroup(portalId, aadGroup, dnnRole);
                            }
                        }

                        aadGroups = aadGroups.NextPageRequest?.GetSync();
                    }
                }
                // Let's remove DNN roles imported from B2C that no longer exists in B2C
                var dnnB2cRoles = GetDnnB2cRoles(portalId);
                // Let's exclude first the mapped roles (we won't remove mapped roles)
                foreach (var role in customRoleMappings)
                {
                    var mappedRole = dnnB2cRoles.FirstOrDefault(ri => ri.RoleName == role.DnnRoleName);
                    if (mappedRole != null)
                    {
                        dnnB2cRoles.Remove(mappedRole);
                    }
                }
                // Remove roles no longer exists in AAD B2C (only the ones that are not mapped against a DNN role)
                foreach (var dnnRole in dnnB2cRoles)
                {
                    if (allaadGroups.Count == 0
                        || allaadGroups.FirstOrDefault(x => x.DisplayName == 
                            (settings.GroupNamePrefixEnabled 
                            ? dnnRole.RoleName.Substring($"{AzureConfig.ServiceName}-".Length) 
                            : dnnRole.RoleName)) == null)
                    {
                        try
                        {
                            RoleController.Instance.DeleteRole(dnnRole);
                            // This is a workaround due to a bug in DNN where RoleSettings are not deleted when a role is deleted
                            DotNetNuke.Data.DataContext.Instance().Execute(System.Data.CommandType.Text, $"DELETE {DotNetNuke.Data.DataProvider.Instance().DatabaseOwner}{DotNetNuke.Data.DataProvider.Instance().ObjectQualifier}RoleSettings WHERE RoleID = @0", dnnRole.RoleID);
                            groupsDeleted++;
                        }
                        catch (Exception ex)
                        {
                            syncErrors++;
                            syncErrorsDesc += $"\n{ex.Message}";
                        }
                    }
                }

                var syncResultDesc = "";
                var syncResultStats = $"sync errors: {syncErrors}; groups created: {groupsCreated}; groups deleted: {groupsDeleted}";
                if (!string.IsNullOrEmpty(syncErrorsDesc))
                {
                    Logger.Error($"AAD Role Sync errors detected: {syncErrorsDesc}");
                    syncResultDesc = $"Portal {portalId} synced with errors, check logs for more information ({syncResultStats}).\n";
                }
                else
                {
                    syncResultDesc = $"Successfully synchronized portal {portalId} ({syncResultStats}).\n";                    
                }
                return syncResultDesc;
            }
            catch (Exception e)
            {
                string message = $"Error while synchronizing the roles from portal {portalId}: {e}.\n";
                Logger.Error(message);
                return message;
            }
        }

        private void UpdateUserProfilePicture(GraphClient graphClient, string aadUserId, UserInfo userInfo, bool saveUserInfo = false)
        {
            try
            {
                if (!string.IsNullOrEmpty(aadUserId) && userInfo != null)
                {
                    var profilePictureMetadata = graphClient.GetUserProfilePictureMetadata(aadUserId);
                    if (profilePictureMetadata != null && profilePictureMetadata.AdditionalData.ContainsKey("@odata.mediaContentType"))
                    {
                        var pictureBytes = graphClient.GetUserProfilePicture(aadUserId);
                        var userFolder = FolderManager.Instance.GetUserFolder(userInfo);
                        var stream = new MemoryStream(pictureBytes);
                        var profilePictureInfo = FileManager.Instance.AddFile(userFolder,
                            $"{aadUserId}.{AzureClient.GetExtensionFromMediaContentType(profilePictureMetadata.AdditionalData["@odata.mediaContentType"].ToString())}",
                            stream, true);

                        userInfo.Profile.Photo = profilePictureInfo.FileId.ToString();
                    }
                    if (saveUserInfo)
                    {
                        AuthenticationController.AddUserAuthentication(userInfo.UserID, AzureConfig.ServiceName, userInfo.Username);
                        UserController.UpdateUser(userInfo.PortalID, userInfo);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Warn($"Error while synchronizing user profile picture from user {aadUserId}", e);
            }
        }

        private UserInfo AddUser(int portalId, string userName, string displayName, string firstName, string lastName, string eMail)
        {
            var user = new UserInfo()
            {
                PortalID = portalId,
                Username = userName,
                DisplayName = displayName,
                FirstName = firstName,
                LastName = lastName,
                Email = eMail,
                Membership = new UserMembership()
                {
                    Password = Guid.NewGuid().ToString(),
                    PasswordQuestion = String.Empty,
                    PasswordAnswer = String.Empty
                },
                Profile = new Entities.Users.UserProfile()
                {
                    FirstName = firstName,
                    LastName = lastName,
                }
            };

            var userCreateStatus = UserController.CreateUser(ref user, false);
            if (userCreateStatus != Security.Membership.UserCreateStatus.Success)
            {
                Logger.Error($"Error creating user {userName} on portal {portalId}: {userCreateStatus}");
                return null;
            }
            else
            {
                AuthenticationController.AddUserAuthentication(user.UserID, AzureConfig.ServiceName, userName);
                UserController.AddUserPortal(portalId, user.UserID);
                return user;
            }
        }

        private UserInfo UpdateUser(UserInfo user, int portalId, string userName, string displayName, string firstName, string lastName, string eMail)
        {
            user.PortalID = portalId;
            user.Username = userName;
            user.DisplayName = displayName;
            user.FirstName = firstName;
            user.LastName = lastName;
            user.Email = eMail;
            user.Profile.FirstName = firstName;
            user.Profile.LastName = lastName;
            user.Membership.UpdatePassword = false;

            AuthenticationController.AddUserAuthentication(user.UserID, AzureConfig.ServiceName, userName);
            UserController.UpdateUser(portalId, user);

            // Updates the password to a new one to avoid password expiration on Azure AD B2C users
            MembershipUser aspnetUser = Membership.GetUser(user.Username);
            aspnetUser.ResetPassword();
            return user;
        }

        internal string SyncUsers(int portalId, AzureConfig settings)
        {
            try
            {
                var syncErrorsDesc = "";
                var syncErrors = 0;
                var usersCreated = 0;
                var usersUpdated = 0;

                if (string.IsNullOrEmpty(settings.AADApplicationId) || string.IsNullOrEmpty(settings.AADApplicationKey))
                {
                    throw new Exception($"AAD application ID or key are not valid on portal {portalId}");
                }

                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);
                var userMappings = UserMappingsRepository.Instance.GetUserMappings(portalId).ToList();

                // Add users from AAD 
                var aadUsers = graphClient.GetAllUsers();
                var allAadUsers = new List<Microsoft.Graph.User>();
                if (aadUsers != null)
                {
                    var userPrefix = settings.UsernamePrefixEnabled ? $"{AzureConfig.ServiceName}-" : "";
                    while (aadUsers != null && aadUsers.Count > 0)
                    {
                        var users = aadUsers.CurrentPage.ToList();
                        allAadUsers.AddRange(users);

                        foreach (var aadUser in users)
                        {
                            try
                            {
                                var userName = userPrefix + GetPropertyValueByClaimName(aadUser, userMappings.FirstOrDefault(x => x.DnnPropertyName == "Id")?.B2cClaimName);
                                var displayName = GetPropertyValueByClaimName(aadUser, userMappings.FirstOrDefault(x => x.DnnPropertyName == "DisplayName")?.B2cClaimName);
                                var firstName = GetPropertyValueByClaimName(aadUser, userMappings.FirstOrDefault(x => x.DnnPropertyName == "FirstName")?.B2cClaimName);
                                var lastName = GetPropertyValueByClaimName(aadUser, userMappings.FirstOrDefault(x => x.DnnPropertyName == "LastName")?.B2cClaimName);
                                var eMail = GetPropertyValueByClaimName(aadUser, userMappings.FirstOrDefault(x => x.DnnPropertyName == "Email")?.B2cClaimName);

                                // Store checks in variables to increase readability and avoid executing the same logic more than once.
                                bool noFirstName = string.IsNullOrEmpty(firstName);
                                bool noLastName = string.IsNullOrEmpty(lastName);

                                // If no display name, try to get it from the first and last name.
                                if (string.IsNullOrEmpty(displayName))
                                {
                                    displayName = !noFirstName ? firstName + (!noLastName ? " " + lastName : "") : "";
                                }
                                else
                                {
                                    // If no first name, try and get it from the display name.
                                    if (noFirstName)
                                    {
                                        firstName = Utils.GetFirstName(displayName);
                                    }
                                    // If no last name, try and get it from the display name.
                                    if (noLastName)
                                    {
                                        lastName = Utils.GetLastName(displayName);
                                    }
                                }

                                var dnnUser = UserController.GetUserByName(portalId, userName);
                                if (dnnUser == null)
                                {
                                    dnnUser = AddUser(portalId, userName, displayName, firstName, lastName, eMail);
                                    usersCreated++;
                                }
                                else
                                {
                                    dnnUser = UpdateUser(dnnUser, portalId, userName, displayName, firstName, lastName, eMail);
                                    usersUpdated++;
                                }
                                
                                if (dnnUser != null && settings.ProfileSyncEnabled)
                                {
                                    UpdateUserProfilePicture(graphClient, aadUser.Id, dnnUser, true);
                                }
                            }
                            catch (Exception ex)
                            {
                                syncErrors++;
                                syncErrorsDesc += $"\n{ex.Message}";
                            }
                        }
                        aadUsers = aadUsers.NextPageRequest?.GetSync();
                    }
                }

                string syncResultDesc = "";
                string syncResultStats = $"sync errors: {syncErrors}; users created: {usersCreated}; users updated: {usersUpdated}";
                if (!string.IsNullOrEmpty(syncErrorsDesc))
                {
                    Logger.Error($"AAD User Sync errors detected: {syncErrorsDesc}");
                    syncResultDesc = $"Portal {portalId} synced with errors, check logs for more information ({syncResultStats}).\n";
                }
                else
                {
                    syncResultDesc = $"Successfully synchronized portal {portalId} ({syncResultStats}).\n";
                }
                return syncResultDesc;
            }
            catch (Exception e)
            {
                string message = $"Error while synchronizing the users from portal {portalId}: {e}.\n";
                Logger.Error(message);
                return message;
            }
        }

        private string GetPropertyValueByClaimName(Microsoft.Graph.User user, string claimName)
        {
            switch (claimName.ToLowerInvariant())
            {
                case "unique_name":
                case "upn":
                    return user.UserPrincipalName;
                case "given_name":
                    return user.GivenName;
                case "family_name":
                    return user.Surname;
                case "name":
                    return user.DisplayName;
                case "emails":
                case "email":
                case "mail":
                    return user.Mail ?? user.OtherMails?.FirstOrDefault();
                case "oid":
                case "sub":
                    return user.Id;
                default: return "";
            }
        }

        internal string SyncExpiredRoles(int portalId, AzureConfig settings)
        {
            try
            {
                string results = "";

                List<ExpiredUserRoleInfo> expiredRoles = RolesRepository.Instance.GetExpiredUserRoles(portalId);

                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);
                PortalSettings portalSettings = new PortalSettings(portalId);                

                foreach (ExpiredUserRoleInfo userRole in expiredRoles)
                {
                    Microsoft.Graph.User user = graphClient.GetUserByEmail(userRole.Email);
                    Group group = graphClient.GetGroupByName(userRole.RoleName);

                    if (user != null && group != null)
                    {
                        try
                        {
                            graphClient.DeleteMember(group.Id, user.Id);
                            results += $"Removed user {userRole.Email} from group {userRole.RoleName}.\n";

                            //// Remove user role in DNN
                            //UserInfo dnnUser = UserController.GetUserById(portalId, userRole.UserID);
                            //if (dnnUser == null)
                            //{
                            //    results += $"User {userRole.Email} not found in DNN.\n";
                            //    continue;
                            //}
                            //RoleInfo role = RoleController.Instance.GetRoleById(portalId, userRole.RoleID);
                            //if (role == null)
                            //{
                            //    results += $"Role {userRole.RoleName} not found in DNN.\n";
                            //    continue;
                            //}
                            //// Remove user role in DNN
                            //RoleController.DeleteUserRole(dnnUser, role, portalSettings, false);
                        }
                        catch (Exception ex)
                        {
                            results += $"Failed to remove user {userRole.Email} from group {userRole.RoleName}: {ex.Message}\n";
                        }
                    }
                    else
                    {
                        results += $"User or group not found for email {userRole.Email} and role {userRole.RoleName}.\n";
                    }
                }

                return results;
            }
            catch (Exception e)
            {
                Logger.Error("Error syncing expired roles", e);
                return "Error syncing expired roles: " + e;
            }
        }

    }
}
