using DotNetNuke.Authentication.Azure.B2C.Common;
using DotNetNuke.Authentication.Azure.B2C.Components;
using DotNetNuke.Authentication.Azure.B2C.Components.Graph;
using DotNetNuke.Authentication.Azure.B2C.Components.Graph.Models;
using DotNetNuke.Authentication.Azure.B2C.Components.Models;
using DotNetNuke.Authentication.Azure.B2C.Data;
using DotNetNuke.Common;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Controllers;
using DotNetNuke.Entities.Users;
using DotNetNuke.Security;
using DotNetNuke.Services.Authentication.OAuth;
using DotNetNuke.Services.Localization;
using DotNetNuke.Web.Api;
using System;
using System.Collections.Generic;
using System.Configuration;
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

                var users = graphClient.GetAllGroups(query);
                return Request.CreateResponse(HttpStatusCode.OK, users.Values);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        public HttpResponseMessage GetAllUsers(string search)
        {
            try
            {
                var settings = new AzureConfig(AzureConfig.ServiceName, PortalSettings.PortalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);
                var query = "$orderby=displayName";
                var filter = ConfigurationManager.AppSettings["AzureADB2C.GetAllUsers.Filter"];
                if (!string.IsNullOrEmpty(search))
                {
                    if (!string.IsNullOrEmpty(filter))
                    {
                        filter += " and ";
                    }
                    filter += $"startswith(displayName, '{search}')";
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
                if (!string.IsNullOrEmpty(filter))
                {
                    query = $"$filter={filter}";
                }

                var users = graphClient.GetAllUsers(query);
                return Request.CreateResponse(HttpStatusCode.OK, users.Values);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
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
                return Request.CreateResponse(HttpStatusCode.OK, groups.Values);
            }
            catch (Exception ex)
            {
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
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);

                var newUser = new NewUser(parameters.user);
                newUser.SignInNames.Add(new SignInName()
                {
                    Type = "emailAddress",
                    Value = newUser.Mail
                });
                newUser.PasswordProfile.Password = parameters.passwordType == "auto" 
                    ? Membership.GeneratePassword(Membership.MinRequiredPasswordLength < 8 ? 8 : Membership.MinRequiredPasswordLength, 
                        Membership.MinRequiredNonAlphanumericCharacters < 2 ? 2 : Membership.MinRequiredNonAlphanumericCharacters) 
                    : parameters.password;
                newUser.OtherMails = new string[] { newUser.Mail };
                newUser.Mail = null;

                // Add custom extension claim PortalId if configured
                var userMapping = UserMappingsRepository.Instance.GetUserMapping("PortalId", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);
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
                if (parameters.sendEmail)
                {
                    SendWelcomeEmail(newUser);
                }

                return Request.CreateResponse(HttpStatusCode.OK, user);
            }
            catch (Exception ex)
            {
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
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);
                var portalUserMapping = UserMappingsRepository.Instance.GetUserMapping("PortalId", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);

                // Validate permissions
                var user = graphClient.GetUser(parameters.user.ObjectId);
                string portalUserMappingB2cCustomClaimName = portalUserMapping?.GetB2cCustomClaimName();
                if (!UserInfo.IsSuperUser && portalUserMapping != null && !string.IsNullOrEmpty(portalUserMappingB2cCustomClaimName))
                {
                    if (!user.AdditionalData.ContainsKey(portalUserMapping.GetB2cCustomClaimName())
                        || (int)(long)user.AdditionalData[portalUserMapping.GetB2cCustomClaimName()] != PortalSettings.PortalId)
                    {
                        return Request.CreateResponse(HttpStatusCode.Forbidden, "You are not allowed to modify this user");
                    }
                }
                var newUser = new NewUser(user, false);
                newUser.PasswordProfile.Password = parameters.passwordType == "auto"
                    ? Membership.GeneratePassword(Membership.MinRequiredPasswordLength < 8 ? 8 : Membership.MinRequiredPasswordLength,
                        Membership.MinRequiredNonAlphanumericCharacters < 2 ? 2 : Membership.MinRequiredNonAlphanumericCharacters)
                    : parameters.password;

                newUser.AdditionalData.Clear();
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
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);
                var portalUserMapping = UserMappingsRepository.Instance.GetUserMapping("PortalId", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);

                // Validate permissions
                var user = graphClient.GetUser(parameters.user.ObjectId);
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
                    user.UserIdentities = null;
                    user.SignInNames = null;
                }
                else
                {
                    var signInName = new SignInName()
                    {
                        Type = "emailAddress",
                        Value = parameters.user.Mail
                    };
                    if (user.SignInNames == null)
                    {
                        user.AdditionalData.Add("signInNames", signInName);
                    }
                    else if (user.SignInNames.Count() == 0)
                    {
                        user.SignInNames.Add(signInName);
                    }
                    else
                    {
                        user.SignInNames[0] = signInName;
                    }
                }

                user.OtherMails = new string[] { parameters.user.Mail };
                graphClient.UpdateUser(user);

                // Update group membership
                UpdateGroupMemberShip(graphClient, user, parameters.groups);

                return Request.CreateResponse(HttpStatusCode.OK, user);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
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
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);
                var portalUserMapping = UserMappingsRepository.Instance.GetUserMapping("PortalId", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);

                // Validate permissions
                var user = graphClient.GetUser(parameters.user.ObjectId);
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
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }


        public class RemoveParameters
        {
            public string objectId { get; set; }
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
                graphClient.DeleteUser(parameters.objectId);

                // Delete user if exist locally
                var usernamePrefix = settings.UsernamePrefixEnabled ? "AzureB2C-" : "";
                var userInfo = UserController.GetUserByName(PortalSettings.PortalId, $"{usernamePrefix}{parameters.objectId}");
                if (userInfo != null)
                {                    
                    UserController.DeleteUser(ref userInfo, false, true);
                    UserController.RemoveDeletedUsers(PortalSettings.PortalId);
                }

                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        private static void UpdateGroupMemberShip(GraphClient graphClient, User user, List<Group> userGroups)
        {
            var groups = graphClient.GetAllGroups("");
            foreach (var group in groups.Values)
            {
                var groupMembers = graphClient.GetGroupMembers(group.ObjectId);
                if (groupMembers.Values.Any(u => u.ObjectId == user.ObjectId)
                    && !userGroups.Any(x => x.ObjectId == group.ObjectId))
                {
                    graphClient.RemoveGroupMember(group.ObjectId, user.ObjectId);
                }
                if (!groupMembers.Values.Any(u => u.ObjectId == user.ObjectId)
                    && userGroups.Any(x => x.ObjectId == group.ObjectId))
                {
                    graphClient.AddGroupMember(group.ObjectId, user.ObjectId);
                }
            }
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
            message = message.Replace("[User:UserName]", user.SignInNames.FirstOrDefault().Value);
            message = message.Replace("[User:DisplayName]", user.DisplayName);
            message = message.Replace("[User:FirstName]", user.GivenName);
            message = message.Replace("[User:LastName]", user.Surname);
            message = message.Replace("[User:Password]", user.PasswordProfile.Password);
            return message;
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
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);
                var portalUserMapping = UserMappingsRepository.Instance.GetUserMapping("PortalId", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);
                var idUserMapping = UserMappingsRepository.Instance.GetUserMapping("Id", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);

                if (string.IsNullOrEmpty(settings.ImpersonatePolicy))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Impersonate policy has not been setup");
                }

                // Validate permissions
                // Allow the current user to impersonate by setting canImpersonate
                var user = GetGraphUserForImpersonation(settings, graphClient, idUserMapping);

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

                user.AdditionalData.Clear();
                user.AdditionalData.Add($"extension_{settings.B2cApplicationId.Replace("-", "")}_canImpersonate", true);
                // HACK: Avoid error "Property alternativeSecurityIds value is required but is empty or missing." when using
                // federated users
                user.UserIdentities = null; 
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
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        private User GetGraphUserForImpersonation(AzureConfig settings, GraphClient graphClient, UserMapping idUserMapping)
        {
            var usernameWithoutPrefix = settings.UsernamePrefixEnabled ? UserInfo.Username.Substring(AzureConfig.ServiceName.Length + 1) : UserInfo.Username;
            var user = idUserMapping.B2cClaimName == "sub" ?
                    graphClient.GetUser(usernameWithoutPrefix)
                    : graphClient.GetAllUsers($"{idUserMapping.B2cClaimName} eq '{usernameWithoutPrefix}").Values.FirstOrDefault();
            return user;
        }

        [HttpPost]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        public HttpResponseMessage Export()
        {
            try
            {
                if (!bool.Parse(Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "EnableExport", "True")))
                {
                    return Request.CreateResponse(HttpStatusCode.Forbidden, "You are not allowed to export users");
                }
                var settings = new AzureConfig(AzureConfig.ServiceName, PortalSettings.PortalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);
                var portalUserMapping = UserMappingsRepository.Instance.GetUserMapping("PortalId", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);
                var idUserMapping = UserMappingsRepository.Instance.GetUserMapping("Id", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);

                var query = "$orderby=displayName";
                var filter = ConfigurationManager.AppSettings["AzureADB2C.GetAllUsers.Filter"];
                var userMapping = UserMappingsRepository.Instance.GetUserMapping("PortalId", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);
                if (userMapping != null && !string.IsNullOrEmpty(userMapping.GetB2cCustomAttributeName(PortalSettings.PortalId)))
                {
                    if (!string.IsNullOrEmpty(filter))
                    {
                        filter += " and ";
                    }
                    filter += $"{userMapping.GetB2cCustomAttributeName(PortalSettings.PortalId)} eq {PortalSettings.PortalId}";
                }
                if (!string.IsNullOrEmpty(filter))
                {
                    query = $"$filter={filter}";
                }

                var opId = Guid.NewGuid().ToString();
                var filename = Path.Combine(Path.GetTempPath(), $"{opId}.tmp");
                File.AppendAllText(filename, $"userPrincipalName,displayName,surname,givenName,issuer,mail,objectId,userType,jobTitle,department,accountEnabled,usageLocation,streetAddress,state,country,physicalDeliveryOfficeName,city,postalCode,telephoneNumber,mobile,ageGroup,consentProvidedForMinor,legalAgeGroupClassification\n", System.Text.Encoding.UTF8);
                var users = graphClient.GetAllUsers(query);
                while (users.Values.Count > 0)
                {
                    foreach (var user in users.Values)
                    {
                        var mail = user.Mail ?? user.OtherMails?.FirstOrDefault() ?? user.SignInNames?.FirstOrDefault()?.Value;
                        File.AppendAllText(filename, $"{user.UserPrincipalName},{user.DisplayName},{user.Surname},{user.GivenName},{user.UserIdentities?.FirstOrDefault()?.Issuer},{mail},{user.ObjectId},{user.UserType},{user.JobTitle},{user.Department},{user.AccountEnabled},{user.UsageLocation},{user.StreetAddress},{user.State},{user.Country},\"{user.OfficeLocation}\",{user.City},{user.PostalCode},{user.BusinessPhones?.FirstOrDefault()},{user.MobilePhone},{user.AgeGroup},{user.LegalAgeGroupClassification}\n", System.Text.Encoding.UTF8);
                    }
                    if (string.IsNullOrEmpty(users.ODataNextLink))
                        break;
                    users = graphClient.GetNextUsers(users.ODataNextLink);
                }

                // Return the impersonation URL
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    downloadUrl = Request.RequestUri.ToString().Replace("/Export", "/DownloadUsers") + "?id=" + opId
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }


        [HttpGet]
        [AllowAnonymous]
        public HttpResponseMessage DownloadUsers(string id)
        {
            try
            {
                var settings = new AzureConfig(AzureConfig.ServiceName, PortalSettings.PortalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);
                var portalUserMapping = UserMappingsRepository.Instance.GetUserMapping("PortalId", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);
                var idUserMapping = UserMappingsRepository.Instance.GetUserMapping("Id", settings.UseGlobalSettings ? -1 : PortalSettings.PortalId);

                if (string.IsNullOrEmpty(id))
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Operation not found");
                }
                var filename = Path.Combine(Path.GetTempPath(), $"{id}.tmp");
                if (!File.Exists(filename) || !(File.GetCreationTimeUtc(filename) > DateTime.UtcNow.AddMinutes(-1)))
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Operation not found");
                }
                var dataBytes = File.ReadAllBytes(filename);
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
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

    }
}
