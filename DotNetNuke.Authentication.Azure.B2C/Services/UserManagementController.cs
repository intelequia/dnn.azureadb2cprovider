using DotNetNuke.Authentication.Azure.B2C.Common;
using DotNetNuke.Authentication.Azure.B2C.Components;
using DotNetNuke.Authentication.Azure.B2C.Components.Graph;
using DotNetNuke.Authentication.Azure.B2C.Components.Graph.Models;
using DotNetNuke.Authentication.Azure.B2C.Components.Models;
using DotNetNuke.Authentication.Azure.B2C.Data;
using DotNetNuke.Common;
using DotNetNuke.Entities.Controllers;
using DotNetNuke.Entities.Users;
using DotNetNuke.Security;
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
                return Request.CreateResponse(HttpStatusCode.OK, groups.ToList());
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
                    filter += $"startswith(displayName, '{search}')";
                }                
                if (portalIdUserMapping != null && !string.IsNullOrEmpty(portalIdUserMapping.GetB2cCustomAttributeName(PortalSettings.PortalId)))
                {
                    if (!string.IsNullOrEmpty(filter))
                    {
                        filter += " and ";
                    }
                    filter += $"{portalIdUserMapping.GetB2cCustomAttributeName(PortalSettings.PortalId)} eq {PortalSettings.PortalId}";
                }

                var users = graphClient.GetAllUsers(filter);
                return Request.CreateResponse(HttpStatusCode.OK, users.ToList());
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
                return Request.CreateResponse(HttpStatusCode.OK, groups.ToList());
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
                identity.Issuer = $"{settings.TenantName}.onmicrosoft.com";

                if (bool.Parse(Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "EnableAddUsersByUsername", "False"))
                    && !string.IsNullOrEmpty(newUser.UserPrincipalName))
                {
                    AddIdentity(newUser, $"{settings.TenantName}.onmicrosoft.com", "userName", newUser.UserPrincipalName);
                }
                if (bool.Parse(Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "EnableAddUsersByEmail", "True"))
                    && !string.IsNullOrEmpty(newUser.Mail))
                {
                    AddIdentity(newUser, $"{settings.TenantName}.onmicrosoft.com", "emailAddress", newUser.Mail);
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
                    if (bool.Parse(Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "EnableAddUsersByUsername", "False"))
                        && !string.IsNullOrEmpty(parameters.user.UserPrincipalName))
                    {
                        AddIdentity(user, $"{settings.TenantName}.onmicrosoft.com", "userName", parameters.user.UserPrincipalName);
                    }

                    if (bool.Parse(Utils.GetTabModuleSetting(ActiveModule.TabModuleID, "EnableAddUsersByUsername", "False"))
                        && !string.IsNullOrEmpty(parameters.user.Mail))
                    {
                        AddIdentity(user, $"{settings.TenantName}.onmicrosoft.com", "emailAddress", parameters.user.Mail);
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

                return Request.CreateResponse(HttpStatusCode.OK, user);
            }
            catch (Exception ex)
            {
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
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        private static void UpdateGroupMemberShip(GraphClient graphClient, User user, List<Group> userGroups)
        {
            graphClient.UpdateGroupMembers(user, userGroups);
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
                user = graphClient.GetAllUsers($"signInNames/any(c:c/value eq '{usernameWithoutPrefix}')").FirstOrDefault();
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

                var opId = Guid.NewGuid().ToString();
                var filename = Path.Combine(Path.GetTempPath(), $"{opId}.tmp");
                System.IO.File.AppendAllText(filename, $"userPrincipalName,displayName,surname,givenName,issuer,mail,objectId,userType,jobTitle,department,accountEnabled,usageLocation,streetAddress,state,country,physicalDeliveryOfficeName,city,postalCode,telephoneNumber,mobile,ageGroup,legalAgeGroupClassification{(!string.IsNullOrEmpty(customAttributes) ? "," + customAttributes : "")}\n", System.Text.Encoding.UTF8);
                var users = graphClient.GetAllUsers(filter);
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
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

    }
}
