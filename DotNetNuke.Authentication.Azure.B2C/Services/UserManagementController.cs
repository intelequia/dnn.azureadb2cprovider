using DotNetNuke.Authentication.Azure.B2C.Components;
using DotNetNuke.Authentication.Azure.B2C.Components.Graph;
using DotNetNuke.Authentication.Azure.B2C.Components.Graph.Models;
using DotNetNuke.Common;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Security;
using DotNetNuke.Web.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;

namespace DotNetNuke.Authentication.Azure.B2C.Services
{
    public class UserManagementController: DnnApiController
    {
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
            public Components.Graph.Models.User user { get; set; }
            public string password { get; set; }
            public bool sendEmail { get; set; }
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
                newUser.PasswordProfile.Password = parameters.password;                
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
                var portalProfileMapping = ProfileMappings.GetFieldProfileMapping(HttpContext.Current.Server.MapPath(ProfileMappings.DefaultProfileMappingsFilePath), "PortalId");

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
                //var groups = graphClient.GetAllGroups("");
                //foreach (var group in groups.Values)
                //{
                //    var membershipChanged = false;
                //    var groupMembers = graphClient.GetGroupMembers(group.ObjectId);
                //    if (groupMembers.Values.Any(u => u.ObjectId == user.ObjectId) 
                //        && !parameters.groups.Any(x => x.ObjectId == group.ObjectId))
                //    {
                //        membershipChanged = true;
                //        groupMembers.Values.Add(user);
                //    }
                //    if (!groupMembers.Values.Any(u => u.ObjectId == user.ObjectId)
                //        && parameters.groups.Any(x => x.ObjectId == group.ObjectId))
                //    {
                //        membershipChanged = true;
                //        groupMembers.Values.Remove(user);
                //    }
                //    if (membershipChanged)
                //    {
                //        graphClient.UpdateGroupMembers(group.ObjectId, groupMembers);
                //    }
                //}

                //var groups = new GraphList<Group>();
                //groups.Values = new List<Group>();
                //groups.Values.AddRange(parameters.groups);
                //graphClient.UpdateUserGroups(parameters.user.ObjectId, groups);

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
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }
    }
}
