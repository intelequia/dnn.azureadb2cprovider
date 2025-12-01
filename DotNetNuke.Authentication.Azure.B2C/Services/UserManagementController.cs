using DotNetNuke.Authentication.Azure.B2C.Common;
using DotNetNuke.Authentication.Azure.B2C.Components;
using DotNetNuke.Authentication.Azure.B2C.Components.Graph;
using DotNetNuke.Authentication.Azure.B2C.Components.Graph.Models;
using DotNetNuke.Authentication.Azure.B2C.Components.Models;
using DotNetNuke.Authentication.Azure.B2C.Data;
using DotNetNuke.Common;
using DotNetNuke.Entities.Controllers;
using DotNetNuke.Entities.Users;
using DotNetNuke.Instrumentation;
using DotNetNuke.Security;
using DotNetNuke.Security.Roles;
using DotNetNuke.Services.FileSystem;
using DotNetNuke.Services.Localization;
using DotNetNuke.Web.Api;
using Microsoft.Graph;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Web.Http;
using System.Web.Security;

namespace DotNetNuke.Authentication.Azure.B2C.Services
{
    public class UserManagementController: DnnApiController
    {
        private static readonly ILog _logger = LoggerSource.Instance.GetLogger(typeof(UserManagementController));

        private string LocalResourceFile
        {
            get
            {
                return System.Web.Hosting.HostingEnvironment.MapPath("~/DesktopModules/AuthenticationServices/AzureB2C/App_LocalResources/UserManagement.ascx.resx");
            }
        }

        private string LocalizeString(string key)
        {
            return Localization.GetString(key, LocalResourceFile);
        }

        [HttpGet]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        public HttpResponseMessage GetAllGroups()
        {
            try
            {
                var settings = new AzureConfig(AzureConfig.ServiceName, PortalSettings.PortalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);
                var query = "";

                var groups = graphClient.GetAllGroups(query);
                var allGroups = new List<Group>();
                
                // Iterate through all pages to get all groups
                while (groups != null && groups.Count > 0)
                {
                    allGroups.AddRange(groups.ToList());
                    groups = groups.NextPageRequest?.GetSync();
                }
                
                return Request.CreateResponse(HttpStatusCode.OK, allGroups);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        public HttpResponseMessage GetAllUsers(string search, string skipToken = null)
        {
            try
            {
                var settings = new AzureConfig(AzureConfig.ServiceName, PortalSettings.PortalId);
                var customAttributes = Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "CustomFields").Replace(" ", "");
                var portalIdUserMapping = UserMappingsRepository.Instance.GetUserMapping("PortalId", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId, customAttributes, settings.B2cApplicationId, portalIdUserMapping);
                var filter = ConfigurationManager.AppSettings["AzureADB2C.GetAllUsers.Filter"];
                var moduleFilter = Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "GraphFilter");
                if (!string.IsNullOrEmpty(moduleFilter))
                {
                    moduleFilter = ReplaceFilterTokens(moduleFilter);
                    if (!string.IsNullOrEmpty(filter))
                    {
                        filter += " and ";
                    }
                    filter += moduleFilter;
                }
                if (!string.IsNullOrEmpty(search))
                {
                    if (!string.IsNullOrEmpty(filter))
                    {
                        filter += " and ";
                    }
                    // Escape single quotes in search term
                    var escapedSearch = search.Replace("'", "''");
                    // Build comprehensive search filter across multiple fields
                    filter += $"(startswith(displayName, '{escapedSearch}') or startswith(givenName, '{escapedSearch}') or startswith(surname, '{escapedSearch}') or startswith(mail, '{escapedSearch}'))";
                }                
                if (portalIdUserMapping != null && !string.IsNullOrEmpty(portalIdUserMapping.GetB2cCustomAttributeName(PortalSettings.PortalId)))
                {
                    if (!string.IsNullOrEmpty(filter))
                    {
                        filter += " and ";
                    }
                    filter += $"{portalIdUserMapping.GetB2cCustomAttributeName(PortalSettings.PortalId)} eq {PortalSettings.PortalId}";
                }

                var usersPage = graphClient.GetAllUsers(filter, skipToken);
                var hasMore = usersPage.NextPageRequest != null;
                
                string nextSkipToken = null;
                if (hasMore && usersPage.NextPageRequest != null)
                {
                    // Try to extract from QueryOptions first
                    var skipTokenOption = usersPage.NextPageRequest.QueryOptions?
                        .FirstOrDefault(q => q.Name == "$skiptoken" || q.Name == "skiptoken");
                    
                    if (skipTokenOption != null)
                    {
                        nextSkipToken = skipTokenOption.Value;
                        _logger.Debug($"GetAllUsers: Extracted skipToken from QueryOptions = {nextSkipToken}");
                    }
                    else
                    {
                        // Fallback to extracting from URL
                        nextSkipToken = ExtractSkipToken(usersPage.NextPageRequest.RequestUrl);
                    }
                }
                
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    users = usersPage.ToList(),
                    hasMore = hasMore,
                    skipToken = nextSkipToken
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        private string ExtractSkipToken(string requestUrl)
        {
            if (string.IsNullOrEmpty(requestUrl))
            {
                _logger.Debug("ExtractSkipToken: requestUrl is null or empty");
                return null;
            }

            try
            {
                _logger.Debug($"ExtractSkipToken: requestUrl = {requestUrl}");
                
                // The URL might be relative or absolute, handle both cases
                Uri uri;
                if (requestUrl.StartsWith("http"))
                {
                    uri = new Uri(requestUrl);
                }
                else
                {
                    // If it's a relative URL, use a dummy base
                    uri = new Uri(new Uri("https://dummy.com"), requestUrl);
                }
                
                var query = HttpUtility.ParseQueryString(uri.Query);
                var skipToken = query["$skiptoken"];
                
                _logger.Debug($"ExtractSkipToken: extracted skipToken = {skipToken ?? "null"}");
                
                return skipToken;
            }
            catch (Exception ex)
            {
                _logger.Error($"ExtractSkipToken: Error extracting skipToken from URL: {requestUrl}", ex);
                return null;
            }
        }

        [HttpGet]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        public HttpResponseMessage GetUserGroups(string objectId)
        {
            try
            {
                var settings = new AzureConfig(AzureConfig.ServiceName, PortalSettings.PortalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);
                var userMapping = UserMappingsRepository.Instance.GetUserMapping("PortalId", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);
                var user = graphClient.GetUser(objectId);
                // Check user is from current portal, if PortalId is an extension name
                string portalUserMappingB2cCustomClaimName = userMapping?.GetB2cCustomClaimName();
                if (!UserInfo.IsSuperUser && userMapping != null && !string.IsNullOrEmpty(portalUserMappingB2cCustomClaimName))
                {
                    var b2cExtensionName = userMapping.GetB2cCustomAttributeName(PortalSettings.PortalId);
                    if (string.IsNullOrEmpty(b2cExtensionName) 
                        && (user?.AdditionalData == null || !user.AdditionalData.ContainsKey(b2cExtensionName)
                        || (int)(long)user.AdditionalData[b2cExtensionName] != PortalSettings.PortalId))
                    {
                        return Request.CreateResponse(HttpStatusCode.Forbidden, "You are not allowed to modify this user");
                    }
                }

                var groups = graphClient.GetUserGroups(objectId);
                return Request.CreateResponse(HttpStatusCode.OK, groups);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        public class AddUserParameters
        {
            public User user { get; set; }
            public string passwordType { get; set; }
            public string password { get; set; }
            public bool sendEmail { get; set; }
            public List<Group> groups { get; set; }
        }
        [HttpPost]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        public HttpResponseMessage AddUser(AddUserParameters parameters)
        {
            try
            {
                if (!bool.Parse(Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "EnableAdd", "True")))
                {
                    return Request.CreateResponse(HttpStatusCode.Forbidden, "You are not allowed to add users");
                }

                var settings = new AzureConfig(AzureConfig.ServiceName, PortalSettings.PortalId);
                var customAttributes = Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "CustomFields").Replace(" ", "");
                var userMapping = UserMappingsRepository.Instance.GetUserMapping("PortalId", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId, customAttributes, settings.B2cApplicationId, userMapping);

                var newUser = new NewUser(parameters.user);

                if (newUser?.Identities == null || newUser.Identities.Count() != 1)
                {
                    throw new ApplicationException("Identity is required");
                }
                // Ensure  user is on this tenant
                var identity = newUser.Identities.FirstOrDefault();
                var overrideIssuer = Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "Issuer");
                var tenantName = !string.IsNullOrEmpty(overrideIssuer) ? overrideIssuer : settings.TenantName;
                if (!tenantName.Contains("."))
                {
                    tenantName += ".onmicrosoft.com";
                }
                identity.Issuer = tenantName;

                if (bool.Parse(Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "EnableAddUsersByUsername", "False"))
                    && !string.IsNullOrEmpty(newUser.UserPrincipalName))
                {
                    AddIdentity(newUser, tenantName, "userName", newUser.UserPrincipalName);
                }
                if (bool.Parse(Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "EnableAddUsersByEmail", "True"))
                    && !string.IsNullOrEmpty(newUser.Mail))
                {
                    AddIdentity(newUser, tenantName, "emailAddress", newUser.Mail);
                    newUser.OtherMails = new string[] { newUser.Mail };
                }
                newUser.PasswordProfile.Password = parameters.passwordType == "auto"
                    ? Membership.GeneratePassword(Membership.MinRequiredPasswordLength < 8 ? 8 : Membership.MinRequiredPasswordLength,
                        Membership.MinRequiredNonAlphanumericCharacters < 2 ? 2 : Membership.MinRequiredNonAlphanumericCharacters)
                    : parameters.password;

                // Add custom extension claim PortalId if configured
                if (userMapping != null)
                {
                    var b2cExtensionName = userMapping.GetB2cCustomAttributeName(PortalSettings.PortalId);
                    if (!string.IsNullOrEmpty(b2cExtensionName))
                    {
                        newUser.AdditionalData.Add(b2cExtensionName, PortalSettings.PortalId);
                    }
                }                

                var user = graphClient.AddUser(newUser);

                // Update group membership
                UpdateGroupMemberShip(graphClient, user, parameters.groups);

                // Send welcome email with password
                if (parameters.sendEmail && !string.IsNullOrEmpty(newUser.Mail))
                {
                    SendWelcomeEmail(newUser);
                }

                // Sync DNN user if setting is enabled
                if (bool.Parse(Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "SyncDnnUserAfterModification", "False")))
                {
                    try
                    {
                        _logger.Info($"Syncing DNN user after adding B2C user {user.Id}");
                        SyncDnnUser(user.Id, PortalSettings.PortalId);
                    }
                    catch (Exception syncEx)
                    {
                        _logger.Error($"Error syncing DNN user after adding B2C user {user.Id}: {syncEx.Message}", syncEx);
                        // Don't fail the whole operation if sync fails
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, user);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        public class ChangePasswordParameters
        {
            public User user { get; set; }
            public string passwordType { get; set; }
            public string password { get; set; }
            public bool sendEmail { get; set; }
        }

        [HttpPost]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        public HttpResponseMessage ChangePassword(AddUserParameters parameters)
        {
            try
            {
                if (!bool.Parse(Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "EnableUpdate", "True")))
                {
                    return Request.CreateResponse(HttpStatusCode.Forbidden, "You are not allowed to update users");
                }

                var settings = new AzureConfig(AzureConfig.ServiceName, PortalSettings.PortalId);
                var customAttributes = Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "CustomFields").Replace(" ", "");
                var portalIdUserMapping = UserMappingsRepository.Instance.GetUserMapping("PortalId", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId, customAttributes, settings.B2cApplicationId, portalIdUserMapping);

                // Validate permissions
                var user = graphClient.GetUser(parameters.user.Id);
                string portalUserMappingB2cCustomClaimName = portalIdUserMapping?.GetB2cCustomClaimName();
                if (!UserInfo.IsSuperUser && portalIdUserMapping != null && !string.IsNullOrEmpty(portalUserMappingB2cCustomClaimName))
                {
                    if (!user.AdditionalData.ContainsKey(portalIdUserMapping.GetB2cCustomClaimName())
                        || (int)(long)user.AdditionalData[portalIdUserMapping.GetB2cCustomClaimName()] != PortalSettings.PortalId)
                    {
                        return Request.CreateResponse(HttpStatusCode.Forbidden, "You are not allowed to modify this user");
                    }
                }
                var newUser = new NewUser(user, false);
                newUser.PasswordProfile.Password = parameters.passwordType == "auto"
                    ? Membership.GeneratePassword(Membership.MinRequiredPasswordLength < 8 ? 8 : Membership.MinRequiredPasswordLength,
                        Membership.MinRequiredNonAlphanumericCharacters < 2 ? 2 : Membership.MinRequiredNonAlphanumericCharacters)
                    : parameters.password;

                graphClient.UpdateUserPassword(newUser);

                // Send welcome email with password
                if (parameters.sendEmail)
                {
                    SendWelcomeEmail(newUser);
                }

                return Request.CreateResponse(HttpStatusCode.OK, user);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }
        public class UpdateUserParameters
        {
            public User user { get; set; }
            public List<Group> groups { get; set; }
        }
        [HttpPost]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        public HttpResponseMessage UpdateUser(UpdateUserParameters parameters)
        {
            try
            {
                if (!bool.Parse(Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "EnableUpdate", "True")))
                {
                    return Request.CreateResponse(HttpStatusCode.Forbidden, "You are not allowed to update users");
                }

                var settings = new AzureConfig(AzureConfig.ServiceName, PortalSettings.PortalId);
                var customAttributes = Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "CustomFields").Replace(" ", "");
                var portalUserMapping = UserMappingsRepository.Instance.GetUserMapping("PortalId", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId, customAttributes, settings.B2cApplicationId, portalUserMapping);

                // Validate permissions
                var user = graphClient.GetUser(parameters.user.Id);
                string portalUserMappingB2cCustomClaimName = portalUserMapping?.GetB2cCustomClaimName();
                if (!UserInfo.IsSuperUser && portalUserMapping != null && !string.IsNullOrEmpty(portalUserMappingB2cCustomClaimName))
                {
                    if (!user.AdditionalData.ContainsKey(portalUserMapping.GetB2cCustomClaimName())
                        || (int) (long) user.AdditionalData[portalUserMapping.GetB2cCustomClaimName()] != PortalSettings.PortalId)
                    {
                        return Request.CreateResponse(HttpStatusCode.Forbidden, "You are not allowed to modify this user");
                    }
                }

                // Update user
                user.DisplayName = parameters.user.DisplayName;
                user.GivenName = parameters.user.GivenName;
                user.Surname = parameters.user.Surname;
                // WORKAROUND: "A stream property was found in a JSON Light request payload. Stream properties are only supported in responses."
                // ==> Patch only the PortalId extension
                user.AdditionalData.Clear();
                if (user.UserPrincipalName.StartsWith("cpim_")) // Is a federated user?
                {
                    // Can't modify this properties on federated users
                    user.Identities = null;
                }
                else
                {
                    var enableUpdateUsernames = bool.Parse(Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "EnableUpdateUsernames", "True"));
                    var enableUpdateEmails = bool.Parse(Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "EnableUpdateEmails", "True"));
                    
                    var tenantName = settings.TenantName;
                    if (!tenantName.Contains("."))
                    {
                        tenantName += ".onmicrosoft.com";
                    }
                    
                    // Only update username identity if setting is enabled
                    if (enableUpdateUsernames 
                        && bool.Parse(Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "EnableAddUsersByUsername", "False"))
                        && !string.IsNullOrEmpty(parameters.user.Mail)
                        && !parameters.user.Mail.Contains("@"))
                    {
                        AddIdentity(user, tenantName, "userName", parameters.user.Mail);
                    }

                    // Only update email identity if setting is enabled
                    if (enableUpdateEmails 
                        && !string.IsNullOrEmpty(parameters.user.Mail)
                        && parameters.user.Mail.Contains("@"))
                    {
                        AddIdentity(user, tenantName, "emailAddress", parameters.user.Mail);
                        user.OtherMails = new string[] { parameters.user.Mail };
                    }
                }

                // Custom Attributes
                if (!string.IsNullOrEmpty(customAttributes) && parameters.user.AdditionalData != null)
                {
                    string[] attr = customAttributes.Split(',');
                    foreach (var key in parameters.user.AdditionalData.Keys)
                    {
                        if (key.StartsWith("extension_") && attr.Any(x => key.EndsWith(x)))
                        {
                            user.AdditionalData.Add(key, parameters.user.AdditionalData[key]);
                        }
                    }
                }

                graphClient.UpdateUser(user);

                // Update group membership
                UpdateGroupMemberShip(graphClient, user, parameters.groups);

                // Sync DNN user if setting is enabled
                if (bool.Parse(Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "SyncDnnUserAfterModification", "False")))
                {
                    try
                    {
                        _logger.Info($"Syncing DNN user after updating B2C user {user.Id}");
                        SyncDnnUser(user.Id, PortalSettings.PortalId);
                    }
                    catch (Exception syncEx)
                    {
                        _logger.Error($"Error syncing DNN user after updating B2C user {user.Id}: {syncEx.Message}", syncEx);
                        // Don't fail the whole operation if sync fails
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, user);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        private void AddIdentity(User user, string issuer, string signInType, string issuerAssignedId)
        {
            var identity = new ObjectIdentity()
            {
                Issuer = issuer,
                SignInType = signInType,
                IssuerAssignedId = issuerAssignedId
            };
            if (user.Identities == null)
            {
                user.Identities = new List<ObjectIdentity>();
            }

            var identities = user.Identities.ToList();
            var current = identities.FirstOrDefault(x => x.SignInType == signInType);
            if (current == null)
            {
                identities.Add(identity);
            }
            else
            {
                current.IssuerAssignedId = issuerAssignedId;
            }
            user.Identities = identities;
        }

        public class ForceChangePasswordParameters
        {
            public User user { get; set; }
        }
        [HttpPost]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        public HttpResponseMessage ForceChangePassword(ForceChangePasswordParameters parameters)
        {
            try
            {
                if (!bool.Parse(Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "EnableUpdate", "True")))
                {
                    return Request.CreateResponse(HttpStatusCode.Forbidden, "You are not allowed to update users");
                }

                var settings = new AzureConfig(AzureConfig.ServiceName, PortalSettings.PortalId);
                var customAttributes = Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "CustomFields").Replace(" ", "");
                var portalUserMapping = UserMappingsRepository.Instance.GetUserMapping("PortalId", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId, customAttributes, settings.B2cApplicationId, portalUserMapping);                

                // Validate permissions
                var user = graphClient.GetUser(parameters.user.Id);
                // Check user is from current portal, if PortalId is an extension name
                string portalUserMappingB2cCustomClaimName = portalUserMapping?.GetB2cCustomClaimName();
                if (!UserInfo.IsSuperUser && portalUserMapping != null && !string.IsNullOrEmpty(portalUserMappingB2cCustomClaimName))
                {
                    if (!user.AdditionalData.ContainsKey(portalUserMappingB2cCustomClaimName)
                        || (int)(long)user.AdditionalData[portalUserMappingB2cCustomClaimName] != PortalSettings.PortalId)
                    {
                        return Request.CreateResponse(HttpStatusCode.Forbidden, "You are not allowed to modify this user");
                    }
                }

                // Update user
                user.AdditionalData.Clear();
                user.AdditionalData.Add($"extension_{settings.B2cApplicationId.Replace("-", "")}_mustResetPassword", true);
                graphClient.UpdateUser(user);

                return Request.CreateResponse(HttpStatusCode.OK, user);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }


        public class RemoveParameters
        {
            public string id { get; set; }
        }
        [HttpPost]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        public HttpResponseMessage Remove(RemoveParameters parameters)
        {
            try
            {
                if (!bool.Parse(Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "EnableDelete", "True")))
                {
                    return Request.CreateResponse(HttpStatusCode.Forbidden, "You are not allowed to delete users");
                }

                var settings = new AzureConfig(AzureConfig.ServiceName, PortalSettings.PortalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);
                graphClient.DeleteUser(parameters.id);

                // Delete user if exist locally
                var usernamePrefix = settings.UsernamePrefixEnabled ? $"{AzureConfig.ServiceName}-" : "";
                var userInfo = UserController.GetUserByName(PortalSettings.PortalId, $"{usernamePrefix}{parameters.id}");
                if (userInfo != null)
                {                    
                    UserController.DeleteUser(ref userInfo, false, true);
                    UserController.RemoveDeletedUsers(PortalSettings.PortalId);
                }

                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        private static void UpdateGroupMemberShip(GraphClient graphClient, User user, List<Group> userGroups)
        {
            graphClient.UpdateGroupMembers(user, userGroups);
        }

        /// <summary>
        /// Syncs the DNN user with the B2C user data, including profile properties, profile picture, and role membership.
        /// This method replicates the sync behavior that occurs during the login process.
        /// </summary>
        /// <param name="b2cUserId">The B2C user object ID</param>
        /// <param name="portalId">The portal ID</param>
        private void SyncDnnUser(string b2cUserId, int portalId)
        {
            try
            {
                var settings = new AzureConfig(AzureConfig.ServiceName, portalId);
                var customAttributes = Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "CustomFields").Replace(" ", "");
                var portalUserMapping = UserMappingsRepository.Instance.GetUserMapping("PortalId", settings.UseGlobalSettings ? -1 : portalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId, customAttributes, settings.B2cApplicationId, portalUserMapping);

                // Get the B2C user
                var b2cUser = graphClient.GetUser(b2cUserId);
                if (b2cUser == null)
                {
                    _logger.Warn($"SyncDnnUser: B2C user {b2cUserId} not found");
                    return;
                }

                // Find the corresponding DNN user
                var idUserMapping = UserMappingsRepository.Instance.GetUserMapping("Id", settings.UseGlobalSettings ? -1 : portalId);
                var usernamePrefix = settings.UsernamePrefixEnabled ? $"{AzureConfig.ServiceName}-" : "";
                string dnnUsername = null;

                if (idUserMapping?.B2cClaimName == "sub")
                {
                    dnnUsername = $"{usernamePrefix}{b2cUserId}";
                }
                else if (idUserMapping?.B2cClaimName?.ToLowerInvariant() == "emails")
                {
                    var email = b2cUser.Mail ?? b2cUser.OtherMails?.FirstOrDefault() ?? b2cUser.Identities?.FirstOrDefault()?.IssuerAssignedId;
                    if (!string.IsNullOrEmpty(email))
                    {
                        dnnUsername = $"{usernamePrefix}{email}";
                    }
                }
                else if (!string.IsNullOrEmpty(idUserMapping?.GetB2cCustomAttributeName(portalId)))
                {
                    var customAttrName = idUserMapping.GetB2cCustomAttributeName(portalId);
                    if (b2cUser.AdditionalData != null && b2cUser.AdditionalData.ContainsKey(customAttrName))
                    {
                        dnnUsername = $"{usernamePrefix}{b2cUser.AdditionalData[customAttrName]}";
                    }
                }

                if (string.IsNullOrEmpty(dnnUsername))
                {
                    _logger.Warn($"SyncDnnUser: Could not determine DNN username for B2C user {b2cUserId}");
                    return;
                }

                var userInfo = UserController.GetUserByName(portalId, dnnUsername);
                if (userInfo == null)
                {
                    _logger.Info($"SyncDnnUser: DNN user {dnnUsername} not found, creating new user");
                    
                    // Create new DNN user
                    userInfo = new UserInfo
                    {
                        PortalID = portalId,
                        Username = dnnUsername,
                        FirstName = b2cUser.GivenName ?? "",
                        LastName = b2cUser.Surname ?? "",
                        DisplayName = b2cUser.DisplayName ?? dnnUsername,
                        Email = b2cUser.Mail ?? b2cUser.OtherMails?.FirstOrDefault() ?? "",
                        Membership = new UserMembership
                        {
                            Password = Membership.GeneratePassword(
                                Membership.MinRequiredPasswordLength < 8 ? 8 : Membership.MinRequiredPasswordLength,
                                Membership.MinRequiredNonAlphanumericCharacters < 2 ? 2 : Membership.MinRequiredNonAlphanumericCharacters),
                            PasswordQuestion = string.Empty,
                            PasswordAnswer = string.Empty
                        },
                        Profile = new Entities.Users.UserProfile
                        {
                            FirstName = b2cUser.GivenName ?? "",
                            LastName = b2cUser.Surname ?? ""
                        }
                    };

                    var userCreateStatus = UserController.CreateUser(ref userInfo, false);
                    if (userCreateStatus != Security.Membership.UserCreateStatus.Success)
                    {
                        _logger.Error($"SyncDnnUser: Error creating DNN user {dnnUsername}: {userCreateStatus}");
                        return;
                    }
                    
                    // Add authentication entry for the user
                    DotNetNuke.Services.Authentication.AuthenticationController.AddUserAuthentication(
                        userInfo.UserID, 
                        AzureConfig.ServiceName, 
                        dnnUsername);
                    
                    _logger.Info($"SyncDnnUser: Successfully created DNN user {dnnUsername}");
                }

                // Update user properties
                userInfo.FirstName = b2cUser.GivenName ?? userInfo.FirstName;
                userInfo.LastName = b2cUser.Surname ?? userInfo.LastName;
                userInfo.DisplayName = b2cUser.DisplayName ?? userInfo.DisplayName;
                userInfo.Email = b2cUser.Mail ?? b2cUser.OtherMails?.FirstOrDefault() ?? userInfo.Email;

                // Reset user password to avoid password expiration issues
                try
                {
                    MembershipUser aspnetUser = Membership.GetUser(userInfo.Username);
                    if (aspnetUser != null)
                    {
                        aspnetUser.ResetPassword();
                        Membership.UpdateUser(aspnetUser);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn($"SyncDnnUser: Error resetting password for user {dnnUsername}: {ex.Message}");
                }

                // Update profile properties if profile sync is enabled
                if (settings.ProfileSyncEnabled)
                {
                    var profileMappings = ProfileMappingsRepository.Instance.GetProfileMappings(settings.UseGlobalSettings ? -1 : portalId);
                    foreach (var mapping in profileMappings)
                    {
                        var b2cClaimName = mapping.GetB2cCustomClaimName();
                        object claimValue = null;

                        // Try to get the value from standard properties first
                        switch (b2cClaimName.ToLowerInvariant())
                        {
                            case "city":
                                claimValue = b2cUser.City;
                                break;
                            case "country":
                                claimValue = b2cUser.Country;
                                break;
                            case "postalcode":
                                claimValue = b2cUser.PostalCode;
                                break;
                            case "state":
                                claimValue = b2cUser.State;
                                break;
                            case "streetaddress":
                                claimValue = b2cUser.StreetAddress;
                                break;
                            default:
                                // Try to get from additional data (custom attributes)
                                if (b2cUser.AdditionalData != null && b2cUser.AdditionalData.ContainsKey(b2cClaimName))
                                {
                                    claimValue = b2cUser.AdditionalData[b2cClaimName];
                                }
                                break;
                        }

                        if (claimValue != null)
                        {
                            userInfo.Profile.SetProfileProperty(mapping.DnnProfilePropertyName, claimValue.ToString());
                        }
                    }

                    // Update profile picture
                    try
                    {
                        var profilePictureMetadata = graphClient.GetUserProfilePictureMetadata(b2cUserId);
                        if (profilePictureMetadata != null && profilePictureMetadata.AdditionalData.ContainsKey("@odata.mediaContentType"))
                        {
                            var pictureBytes = graphClient.GetUserProfilePicture(b2cUserId);
                            var userFolder = FolderManager.Instance.GetUserFolder(userInfo);
                            var stream = new MemoryStream(pictureBytes);
                            var profilePictureInfo = FileManager.Instance.AddFile(userFolder,
                                $"{b2cUserId}.{GetExtensionFromMediaContentType(profilePictureMetadata.AdditionalData["@odata.mediaContentType"].ToString())}",
                                stream, true);

                            userInfo.Profile.Photo = profilePictureInfo.FileId.ToString();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"SyncDnnUser: Error syncing profile picture for user {dnnUsername}: {ex.Message}");
                    }
                }

                // Save user info
                UserController.UpdateUser(portalId, userInfo);

                // Update role membership if role sync is enabled
                if (settings.RoleSyncEnabled)
                {
                    try
                    {
                        var customRoleMappings = RoleMappingsRepository.Instance.GetRoleMappings(settings.UseGlobalSettings ? -1 : portalId).ToList();
                        var syncOnlyMappedRoles = (customRoleMappings != null && customRoleMappings.Count > 0);
                        var groupPrefix = settings.GroupNamePrefixEnabled ? $"{AzureConfig.ServiceName}-" : "";

                        var groups = graphClient.GetUserGroups(b2cUserId);

                        var filter = ConfigurationManager.AppSettings["AzureADB2C.GetUserGroups.Filter"];
                        if (!string.IsNullOrEmpty(filter))
                        {
                            var onlyGroups = filter.Split(';');
                            var g = new List<Microsoft.Graph.Group>();
                            foreach (var f in onlyGroups)
                            {
                                var r = groups.Where(x => x.DisplayName.StartsWith(f));
                                if (r.Count() > 0)
                                    g.AddRange(r);
                            }
                            groups = g;
                        }

                        if (syncOnlyMappedRoles)
                        {
                            groupPrefix = "";
                            var b2cRoles = customRoleMappings.Select(rm => rm.B2cRoleName);
                            groups.RemoveAll(x => !b2cRoles.Contains(x.DisplayName));
                        }

                        var dnnB2cRoles = GetDnnB2cRoles(portalId);
                        // Remove user from roles they no longer belong to
                        foreach (var dnnUserRole in userInfo.Roles.Where(role => dnnB2cRoles.Contains(role)))
                        {
                            var aadGroupName = dnnUserRole;
                            var roleName = dnnUserRole;
                            var mapping = customRoleMappings?.FirstOrDefault(x => x.DnnRoleName == dnnUserRole);
                            if (mapping != null)
                            {
                                aadGroupName = mapping.B2cRoleName;
                                roleName = mapping.DnnRoleName;
                            }
                            if (groups.FirstOrDefault(aadGroup => $"{groupPrefix}{aadGroup.DisplayName}" == aadGroupName) == null)
                            {
                                var role = RoleController.Instance.GetRoleByName(portalId, roleName);
                                RoleController.DeleteUserRole(userInfo, role, PortalSettings, false);
                            }
                        }

                        // Add user to roles they should belong to
                        foreach (var group in groups)
                        {
                            var roleToAssign = syncOnlyMappedRoles ? customRoleMappings.Find(r => r.B2cRoleName == group.DisplayName).DnnRoleName : $"{groupPrefix}{group.DisplayName}";
                            var dnnRole = RoleController.Instance.GetRoleByName(portalId, roleToAssign);

                            if (dnnRole == null)
                            {
                                // Create role
                                var roleId = AddRole(portalId, $"{groupPrefix}{group.DisplayName}", group.Description, true);
                                dnnRole = RoleController.Instance.GetRoleById(portalId, roleId);
                                // Add user to Role
                                RoleController.Instance.AddUserRole(portalId,
                                                                    userInfo.UserID,
                                                                    roleId,
                                                                    RoleStatus.Approved,
                                                                    false,
                                                                    group.CreatedDateTime.HasValue ? group.CreatedDateTime.Value.DateTime : DotNetNuke.Common.Utilities.Null.NullDate,
                                                                    DotNetNuke.Common.Utilities.Null.NullDate);
                            }
                            else
                            {
                                UpdateRoleGroup(portalId, group, dnnRole);

                                // If user doesn't belong to that DNN role, add them
                                if (!userInfo.Roles.Contains(roleToAssign))
                                {
                                    RoleController.Instance.AddUserRole(portalId,
                                                                        userInfo.UserID,
                                                                        dnnRole.RoleID,
                                                                        Security.Roles.RoleStatus.Approved,
                                                                        false,
                                                                        group.CreatedDateTime.HasValue ? group.CreatedDateTime.Value.DateTime : DateTime.Today,
                                                                        DotNetNuke.Common.Utilities.Null.NullDate);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"SyncDnnUser: Error syncing roles for user {dnnUsername}: {ex.Message}");
                    }
                }

                _logger.Info($"Successfully synced DNN user {dnnUsername} with B2C user {b2cUserId}");
            }
            catch (Exception ex)
            {
                _logger.Error($"SyncDnnUser: Error syncing DNN user for B2C user {b2cUserId}: {ex.Message}", ex);
            }
        }

        private List<string> GetDnnB2cRoles(int portalId)
        {
            // This is the list of DNN roles coming from B2C
            var settings = new AzureConfig(AzureConfig.ServiceName, portalId);
            var customRoleMappings = RoleMappingsRepository.Instance.GetRoleMappings(settings.UseGlobalSettings ? -1 : portalId).ToList();
            
            var result = RoleController.Instance.GetRoles(portalId)
                .Where(r => r.Settings.ContainsKey(AzureClient.RoleSettingsB2cPropertyName) && r.Settings[AzureClient.RoleSettingsB2cPropertyName] == AzureClient.RoleSettingsB2cPropertyValue)
                .Select((roleInfo) => roleInfo.RoleName)
                .ToList();

            // Also add mapped roles
            result.AddRange(customRoleMappings.Select((role) => role.DnnRoleName).ToList());

            return result;
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

        private void UpdateRoleGroup(int portalId, Microsoft.Graph.Group group, RoleInfo dnnRole)
        {
            AzureClient.UpdateRoleGroup(portalId, group, dnnRole);
        }

        private static string GetExtensionFromMediaContentType(string contentType)
        {
            return AzureClient.GetExtensionFromMediaContentType(contentType);
        }

        private void SendWelcomeEmail(NewUser user)
        {
            var subject = ReplaceTokens(LocalizeString("WelcomeMailSubject"), user);
            var body = ReplaceTokens(LocalizeString("WelcomeMailBody"), user);
            
            DotNetNuke.Services.Mail.Mail.SendMail(HostController.Instance.GetString("HostEmail"), 
                user.OtherMails.FirstOrDefault(), "", subject, body, "", "Html", "", "", "", "");
        }

        private string ReplaceTokens(string message, NewUser user)
        {            

            message = message.Replace("[Portal:PortalName]", PortalSettings.PortalName);
            message = message.Replace("[Portal:URL]", Globals.AddHTTP(PortalSettings.DefaultPortalAlias));
            message = message.Replace("[Portal:LogoURL]", $"{Globals.AddHTTP(PortalSettings.DefaultPortalAlias)}/Portals/{PortalSettings.PortalId}/{PortalSettings.LogoFile}");
            message = message.Replace("[Portal:Copyright]", PortalSettings.FooterText.Replace("[year]", DateTime.Now.ToString("yyyy")));
            message = message.Replace("[User:UserName]", user.Identities.FirstOrDefault().IssuerAssignedId);
            message = message.Replace("[User:DisplayName]", user.DisplayName);
            message = message.Replace("[User:FirstName]", user.GivenName);
            message = message.Replace("[User:LastName]", user.Surname);
            message = message.Replace("[User:Password]", user.PasswordProfile.Password);
            return message;
        }

        private string ReplaceFilterTokens(string filter)
        {
            filter = filter.Replace("[Portal:PortalName]", PortalSettings.PortalName);
            filter = filter.Replace("[Portal:URL]", Globals.AddHTTP(PortalSettings.DefaultPortalAlias));
            filter = filter.Replace("[Portal:LogoURL]", $"{Globals.AddHTTP(PortalSettings.DefaultPortalAlias)}/Portals/{PortalSettings.PortalId}/{PortalSettings.LogoFile}");
            filter = filter.Replace("[Portal:Copyright]", PortalSettings.FooterText.Replace("[year]", DateTime.Now.ToString("yyyy")));
            filter = filter.Replace("[User:UserName]", UserInfo.Username);
            filter = filter.Replace("[User:DisplayName]", UserInfo.DisplayName);
            filter = filter.Replace("[User:FirstName]", UserInfo.FirstName);
            filter = filter.Replace("[User:LastName]", UserInfo.LastName);

            // Claims replacement
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(
                @"\[Claims:([_a-zA-Z][_a-zA-Z0-9]{0,128})\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            System.Text.RegularExpressions.Match match = regex.Match(filter);
            if (match.Success)
            {
                string t = GetBearerToken();
                if (!string.IsNullOrEmpty(t))
                {
                    JwtSecurityToken token = new JwtSecurityToken(t);
                    while (match.Success)
                    {
                        filter = filter.Replace(match.Groups[0].Value, 
                            token.Claims.FirstOrDefault(x => x.Type == match.Groups[1].Value)?.Value);
                        match = match.NextMatch();
                    }
                }
            }
            return filter;
        }

        internal static string GetBearerToken()
        {
            HttpCookie cookie = HttpContext.Current.Request.Cookies.Get("AzureB2CUserToken");
            if (cookie == null)
            {
                return "";
            }
            NameValueCollection qparams = HttpUtility.ParseQueryString(cookie.Value);
            return qparams["oauth_token"];
        }


        public class ImpersonateParams
        {
            public string returnUri { get; set; }
        }
        [HttpPost]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        public HttpResponseMessage Impersonate(ImpersonateParams parameters)
        {
            try
            {
                if (!bool.Parse(Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "EnableImpersonate", "True")))
                {
                    return Request.CreateResponse(HttpStatusCode.Forbidden, "You are not allowed to impersonate users");
                }

                var settings = new AzureConfig(AzureConfig.ServiceName, PortalSettings.PortalId);
                var customAttributes = Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "CustomFields").Replace(" ", "");
                var portalUserMapping = UserMappingsRepository.Instance.GetUserMapping("PortalId", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId, customAttributes, settings.B2cApplicationId, portalUserMapping);
                var idUserMapping = UserMappingsRepository.Instance.GetUserMapping("Id", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);

                if (string.IsNullOrEmpty(settings.ImpersonatePolicy))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Impersonate policy has not been setup");
                }

                // Validate permissions
                // Allow the current user to impersonate by setting canImpersonate
                var user = GetGraphUserForImpersonation(settings, graphClient, idUserMapping);
                if (user == null)
                {
                    return Request.CreateResponse(HttpStatusCode.Forbidden, "User not found on Azure AD B2C, can't impersonate with this user. Only users registered on Azure AD B2C can Impersonate.");
                }

                // Check user is from current portal, if PortalId is an extension name
                string portalUserMappingB2cCustomClaimName = portalUserMapping?.GetB2cCustomClaimName();
                if (!UserInfo.IsSuperUser && portalUserMapping != null && !string.IsNullOrEmpty(portalUserMappingB2cCustomClaimName))
                {
                    if (!user.AdditionalData.ContainsKey(portalUserMappingB2cCustomClaimName)
                        || (int)(long)user.AdditionalData[portalUserMappingB2cCustomClaimName] != PortalSettings.PortalId)
                    {
                        return Request.CreateResponse(HttpStatusCode.Forbidden, "You can't impersonate");
                    }
                }

                if (user.AdditionalData == null)
                {
                    user.AdditionalData = new Dictionary<string, object>();
                }
                else
                {
                    user.AdditionalData.Clear();
                }
                user.AdditionalData.Add($"extension_{settings.B2cApplicationId.Replace("-", "")}_canImpersonate", true);
                // HACK: Avoid error "Property alternativeSecurityIds value is required but is empty or missing." when using
                // federated users
                //user.UserIdentities = null; 
                graphClient.UpdateUser(user);

                // Return the impersonation URL
                var azureClient = new AzureClient(this.PortalSettings.PortalId, DotNetNuke.Services.Authentication.AuthMode.Login);                
                var url = azureClient.NavigateImpersonation(new Uri(parameters.returnUri), user.Mail ?? user.OtherMails?.FirstOrDefault());
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    impersonateUrl = url
                }); ;
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        private User GetGraphUserForImpersonation(AzureConfig settings, GraphClient graphClient, UserMapping idUserMapping)
        {
            var usernameWithoutPrefix = settings.UsernamePrefixEnabled ? UserInfo.Username.Substring(AzureConfig.ServiceName.Length + 1) : UserInfo.Username;
            User user;
            if (idUserMapping.B2cClaimName == "sub")
            {
                user = graphClient.GetUser(usernameWithoutPrefix);
            }
            else if (idUserMapping.B2cClaimName.ToLowerInvariant() == "emails")
            {
                var tenantName = settings.TenantName;
                if (!tenantName.Contains("."))
                {
                    tenantName += ".onmicrosoft.com";
                }
                user = graphClient.GetAllUsers($"identities/any(c:c/issuer eq '{tenantName}' and c/issuerAssignedId eq '{usernameWithoutPrefix}')").FirstOrDefault();
            }
            else
            {
                user = graphClient.GetAllUsers($"{idUserMapping.GetB2cCustomAttributeName(settings.PortalID)} eq '{usernameWithoutPrefix}'").FirstOrDefault();
            }
            return user;
        }

        [HttpGet]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        public HttpResponseMessage Export(string search)
        {
            try
            {
                if (!bool.Parse(Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "EnableExport", "True")))
                {
                    return Request.CreateResponse(HttpStatusCode.Forbidden, "You are not allowed to export users");
                }
                var settings = new AzureConfig(AzureConfig.ServiceName, PortalSettings.PortalId);
                var customAttributes = Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "CustomFields").Replace(" ", "");
                var portalUserMapping = UserMappingsRepository.Instance.GetUserMapping("PortalId", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId, customAttributes, settings.B2cApplicationId, portalUserMapping);
                var idUserMapping = UserMappingsRepository.Instance.GetUserMapping("Id", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);

                var filter = ConfigurationManager.AppSettings["AzureADB2C.GetAllUsers.Filter"];
                if (!string.IsNullOrEmpty(search))
                {
                    if (!string.IsNullOrEmpty(filter))
                    {
                        filter += " and ";
                    }
                    // Escape single quotes in search term
                    var escapedSearch = search.Replace("'", "''");
                    filter += $"(startswith(displayName, '{escapedSearch}') or startswith(givenName, '{escapedSearch}') or startswith(surname, '{escapedSearch}') or startswith(mail, '{escapedSearch}'))";
                }
                var userMapping = UserMappingsRepository.Instance.GetUserMapping("PortalId", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);
                if (userMapping != null && !string.IsNullOrEmpty(userMapping.GetB2cCustomAttributeName(PortalSettings.PortalId)))
                {
                    if (!string.IsNullOrEmpty(filter))
                    {
                        filter += " and ";
                    }
                    filter += $"{userMapping.GetB2cCustomAttributeName(PortalSettings.PortalId)} eq {PortalSettings.PortalId}";
                }

                var opId = Guid.NewGuid().ToString();
                var filename = Path.Combine(Path.GetTempPath(), $"{opId}.tmp");
                System.IO.File.AppendAllText(filename, $"userPrincipalName,displayName,surname,givenName,issuer,mail,objectId,userType,jobTitle,department,accountEnabled,usageLocation,streetAddress,state,country,physicalDeliveryOfficeName,city,postalCode,telephoneNumber,mobile,ageGroup,legalAgeGroupClassification{(!string.IsNullOrEmpty(customAttributes) ? "," + customAttributes : "")}\n", System.Text.Encoding.UTF8);
                var users = graphClient.GetAllUsers(filter, null);
                while (users != null && users.Count > 0)
                {
                    foreach (var user in users)
                    {
                        var mail = user.Mail ?? user.OtherMails?.FirstOrDefault() ?? user.Identities?.FirstOrDefault()?.IssuerAssignedId;
                        var userLine = $"{user.UserPrincipalName},{user.DisplayName},{user.Surname},{user.GivenName},{user.Identities?.FirstOrDefault()?.Issuer},{mail},{user.Id},{user.UserType},{user.JobTitle},{user.Department},{user.AccountEnabled},{user.UsageLocation},{user.StreetAddress},{user.State},{user.Country},\"{user.OfficeLocation}\",{user.City},{user.PostalCode},{user.BusinessPhones?.FirstOrDefault()},{user.MobilePhone},{user.AgeGroup},{user.LegalAgeGroupClassification}";

                        foreach (string attr in customAttributes.Split(','))
                        {
                            userLine += ",";
                            var extensionName = $"extension_{settings.B2cApplicationId.Replace("-", "")}_{attr}";
                            if (user?.AdditionalData != null && user.AdditionalData.ContainsKey(extensionName))
                            {
                                userLine += $"{user.AdditionalData[extensionName]}";
                            }
                        }

                        userLine += "\n";
                        System.IO.File.AppendAllText(filename, userLine, System.Text.Encoding.UTF8);
                    }
                    users = users.NextPageRequest?.GetSync();
                }

                // Return the download URL
                var url = Request.RequestUri.ToString().ToLowerInvariant();
                url = url.Substring(0, url.IndexOf("/export")) + "/downloadusers?id=" + opId;
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    downloadUrl = url
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }


        [HttpGet]
        [AllowAnonymous]
        public HttpResponseMessage DownloadUsers(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Operation not found");
                }
                var filename = Path.Combine(Path.GetTempPath(), $"{id}.tmp");
                if (!System.IO.File.Exists(filename) || !(System.IO.File.GetCreationTimeUtc(filename) > DateTime.UtcNow.AddMinutes(-1)))
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Operation not found");
                }
                var dataBytes = System.IO.File.ReadAllBytes(filename);
                var dataStream = new MemoryStream(dataBytes);
                    
                var result = new HttpResponseMessage(HttpStatusCode.OK);
                result.Content = new StreamContent(dataStream);
                result.Content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
                result.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileName = $"{id}.csv"
                };
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

    }
}
