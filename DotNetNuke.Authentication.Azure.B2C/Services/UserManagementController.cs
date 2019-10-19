using DotNetNuke.Authentication.Azure.B2C.Components;
using DotNetNuke.Authentication.Azure.B2C.Components.Graph;
using DotNetNuke.Authentication.Azure.B2C.Components.Graph.Models;
using DotNetNuke.Common;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Controllers;
using DotNetNuke.Entities.Users;
using DotNetNuke.Security;
using DotNetNuke.Services.Localization;
using DotNetNuke.Web.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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
                var settings = new AzureConfig("AzureB2C", PortalSettings.PortalId);
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
        public HttpResponseMessage GetAllUsers()
        {
            try
            {
                var settings = new AzureConfig("AzureB2C", PortalSettings.PortalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);
                var query = "";
                var profileMapping = ProfileMappings.GetProfileMappings(HttpContext.Current.Server.MapPath(ProfileMappings.DefaultProfileMappingsFilePath))
                    .ProfileMapping.FirstOrDefault(x => x.DnnProfilePropertyName == "PortalId");
                if (profileMapping != null)
                {
                    query = $"$filter={profileMapping.B2cExtensionName} eq {PortalSettings.PortalId}";
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
                var settings = new AzureConfig("AzureB2C", PortalSettings.PortalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);
                var profileMapping = ProfileMappings.GetProfileMappings(HttpContext.Current.Server.MapPath(ProfileMappings.DefaultProfileMappingsFilePath))
                    .ProfileMapping.FirstOrDefault(x => x.DnnProfilePropertyName == "PortalId");
                var user = graphClient.GetUser(objectId);
                if (profileMapping != null)
                {
                    if (user?.AdditionalData == null || !user.AdditionalData.ContainsKey(profileMapping.B2cExtensionName)
                        || (int)(long)user.AdditionalData[profileMapping.B2cExtensionName] != PortalSettings.PortalId)
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
                var settings = new AzureConfig("AzureB2C", PortalSettings.PortalId);
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
                var profileMapping = ProfileMappings.GetProfileMappings(HttpContext.Current.Server.MapPath(ProfileMappings.DefaultProfileMappingsFilePath))
                    .ProfileMapping.FirstOrDefault(x => x.DnnProfilePropertyName == "PortalId");
                if (profileMapping != null)
                {
                    newUser.AdditionalData.Add(profileMapping.B2cExtensionName, PortalSettings.PortalId);
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
                var settings = new AzureConfig("AzureB2C", PortalSettings.PortalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);
                var portalProfileMapping = ProfileMappings.GetFieldProfileMapping(System.Web.Hosting.HostingEnvironment.MapPath(ProfileMappings.DefaultProfileMappingsFilePath), "PortalId");

                // Validate permissions
                var user = graphClient.GetUser(parameters.user.ObjectId);
                // Check user is from current portal, if PortalId is an extension name
                if (portalProfileMapping != null)
                {
                    if (!user.AdditionalData.ContainsKey(portalProfileMapping.B2cExtensionName)
                        || (int) (long) user.AdditionalData[portalProfileMapping.B2cExtensionName] != PortalSettings.PortalId)
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
                user.AdditionalData.Add("signInNames", new List<SignInName>() { 
                    new SignInName()
                    {
                        Type = "emailAddress",
                        Value = parameters.user.Mail
                    }
                });
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
                var settings = new AzureConfig("AzureB2C", PortalSettings.PortalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);

                graphClient.DeleteUser(parameters.objectId);

                // Delete user if exist locally
                var userInfo = UserController.GetUserByName(PortalSettings.PortalId, $"AzureB2C-{parameters.objectId}");
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
    }
}
