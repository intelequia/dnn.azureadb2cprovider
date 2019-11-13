#region Copyright

// 
// Intelequia Software solutions - https://intelequia.com
// Copyright (c) 2010-2017
// by Intelequia Software Solutions
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
// documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
// the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
// to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or substantial portions 
// of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
// TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
// CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE.

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using Dnn.PersonaBar.Library;
using Dnn.PersonaBar.Library.Attributes;
using DotNetNuke.Authentication.Azure.B2C.Components;
using DotNetNuke.Authentication.Azure.B2C.Components.Graph;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Modules.Definitions;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Profile;
using DotNetNuke.Entities.Tabs;
using DotNetNuke.Instrumentation;
using DotNetNuke.Security.Permissions;
using DotNetNuke.Security.Roles;
using DotNetNuke.Services.Exceptions;
using DotNetNuke.Services.Upgrade;
using DotNetNuke.Web.Api;
using DotNetNuke.Web.UI;

namespace DotNetNuke.Authentication.Azure.B2C.Services
{
    [MenuPermission(Scope = ServiceScope.Host)]
    public class AzureADB2CController : PersonaBarApiController
    {
        private static readonly ILog Logger = LoggerSource.Instance.GetLogger(typeof(AzureADB2CController));

        /// GET: api/AzureAD/GetSettings
        /// <summary>
        /// Gets the settings
        /// </summary>
        /// <returns>settings</returns>
        [HttpGet]
        public HttpResponseMessage GetSettings()
        {
            try
            {
                var settings = AzureADB2CProviderSettings.LoadSettings("AzureB2C", PortalId);
                return Request.CreateResponse(HttpStatusCode.OK, settings);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        // POST: api/RedisCaching/UpdateGeneralSettings
        /// <summary>
        /// Updates the general settings
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage UpdateGeneralSettings(AzureADB2CProviderSettings settings)
        {
            try
            {
                if (!UserInfo.IsSuperUser)
                {
                    var config = new AzureConfig("AzureB2C", PortalId);
                    if (config.UseGlobalSettings || config.UseGlobalSettings != settings.UseGlobalSettings)
                        return Request.CreateResponse(HttpStatusCode.Forbidden, "Only super users can change this setting");
                }

                AzureADB2CProviderSettings.SaveGeneralSettings("AzureB2C", PortalId, settings);
                AddUserProfilePage(PortalId, settings.Enabled && !string.IsNullOrEmpty(settings.ProfilePolicy));
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [HttpGet]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage GetProfileSettings()
        {
            try
            {
                var profileSettings = ProfileMappings.GetProfileMappings();
                
                return Request.CreateResponse(HttpStatusCode.OK, profileSettings);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [HttpGet]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage GetRoleMappingSettings()
        {
            try
            {
                var roleMappings = RoleMappings.GetRoleMappings();

                return Request.CreateResponse(HttpStatusCode.OK, roleMappings);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [HttpGet]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage GetUserMappingSettings()
        {
            try
            {
                var userMappings = UserMappings.GetUserMappings();

                return Request.CreateResponse(HttpStatusCode.OK, userMappings);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [HttpGet]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage GetAvailableRoles()
        {
            try
            {
                var availableRoles = RoleController.Instance.GetRoles(PortalId);
                var result = new List<string>();
                foreach (var availableRole in availableRoles)
                {
                    result.Add(availableRole.RoleName);
                }

                result.Sort();

                return Request.CreateResponse(HttpStatusCode.OK, result);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        public class UpdateUserMappingInput
        {
            public UserMappingsUserMapping mappingDetail;
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage UpdateUserMapping(UpdateUserMappingInput input)
        {
            try
            {
                // Get all the user mappings
                var userMappings = UserMappings.GetUserMappings();

                var itemFound = Array.Find(userMappings.UserMapping, item => item.DnnPropertyName == input.mappingDetail.DnnPropertyName);
                if (itemFound != null)
                {
                    itemFound.DnnPropertyName = input.mappingDetail.DnnPropertyName;
                    itemFound.B2cPropertyName = input.mappingDetail.B2cPropertyName;
                    UserMappings.UpdateUserMappings(userMappings);
                }

                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        public class UpdateRoleMappingInput
        {
            public string originalDnnRoleName;
            public RoleMappingsRoleMapping mappingDetail;
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage UpdateRoleMapping(UpdateRoleMappingInput input)
        {
            try
            {
                // Get all the role mappings
                var roleMappings = RoleMappings.GetRoleMappings();
                // Find the one with DnnRoleName = input.originalDnnRoleName
                var itemFound = Array.Find(roleMappings.RoleMapping, item => item.DnnRoleName == input.originalDnnRoleName);
                if (itemFound == null)
                {
                    // The item is not in the list, so it's new
                    var list = new List<RoleMappingsRoleMapping>(roleMappings.RoleMapping);
                    itemFound = new RoleMappingsRoleMapping
                    {
                        DnnRoleName = input.mappingDetail.DnnRoleName,
                        B2cRoleName = input.mappingDetail.B2cRoleName
                    };

                    list.Add(itemFound);

                    roleMappings.RoleMapping = list.OrderBy(e => e.DnnRoleName).ToArray();
                }
                else
                {
                    itemFound.DnnRoleName = input.mappingDetail.DnnRoleName;
                    itemFound.B2cRoleName = input.mappingDetail.B2cRoleName;
                }

                RoleMappings.UpdateRoleMappings(roleMappings);

                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        public class DeleteRoleMappingInput
        {
            public string dnnRoleName;
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage DeleteRoleMapping(DeleteRoleMappingInput input)
        {
            try
            {
                var roleMappings = RoleMappings.GetRoleMappings();
                var list = new List<RoleMappingsRoleMapping>(roleMappings.RoleMapping);
                var itemToRemove = list.Find(item => item.DnnRoleName == input.dnnRoleName);
                if (itemToRemove != null)
                {
                    list.Remove(itemToRemove);
                    roleMappings.RoleMapping = list.ToArray();

                    RoleMappings.UpdateRoleMappings(roleMappings);
                }

                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        public class UpdateProfileMappingInput
        {
            public string originalDnnPropertyName;
            public ProfileMappingsProfileMapping profileMappingDetail;
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage UpdateProfileMapping(UpdateProfileMappingInput input)
        {
            try
            {
                // Get all the profile mappings
                var profileMappings = ProfileMappings.GetProfileMappings();
                // Find the one with DnnPropertyName = input.originalDnnPropertyName
                var itemFound = Array.Find(profileMappings.ProfileMapping, item => item.DnnProfilePropertyName == input.originalDnnPropertyName);
                if (itemFound == null)
                {
                    // The item is not in the list, so it's new
                    var list = new List<ProfileMappingsProfileMapping>(profileMappings.ProfileMapping);
                    itemFound = new ProfileMappingsProfileMapping
                    {
                        DnnProfilePropertyName = input.profileMappingDetail.DnnProfilePropertyName,
                        B2cClaimName = input.profileMappingDetail.B2cClaimName,
                        B2cExtensionName = input.profileMappingDetail.B2cExtensionName
                    };

                    list.Add(itemFound);

                    profileMappings.ProfileMapping = list.OrderBy(e => e.DnnProfilePropertyName).ToArray();
                }
                else
                {
                    itemFound.DnnProfilePropertyName = input.profileMappingDetail.DnnProfilePropertyName;
                    itemFound.B2cClaimName = input.profileMappingDetail.B2cClaimName;
                    itemFound.B2cExtensionName = input.profileMappingDetail.B2cExtensionName;
                }

                ProfileMappings.UpdateProfileMappings(profileMappings);

                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        public class DeleteProfileMappingInput
        {
            public string dnnProfilePropertyName;
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage DeleteProfileMapping(DeleteProfileMappingInput input)
        {
            try
            {
                var profileMappings = ProfileMappings.GetProfileMappings();
                var list = new List<ProfileMappingsProfileMapping>(profileMappings.ProfileMapping);
                var itemToRemove = list.Find(item => item.DnnProfilePropertyName == input.dnnProfilePropertyName);
                if (itemToRemove != null)
                {
                    list.Remove(itemToRemove);
                    profileMappings.ProfileMapping = list.ToArray();

                    ProfileMappings.UpdateProfileMappings(profileMappings);
                }

                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [HttpGet]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage GetProfileProperties()
        {
            try
            {
                var profileProperties = ProfileController.GetPropertyDefinitionsByPortal(0, false, false).Cast<ProfilePropertyDefinition>().Select(v => new
                {
                    v.PropertyName
                });
                
                var result = new List<string>();
                foreach (var item in profileProperties)
                {
                    result.Add(item.PropertyName);
                }
                result.Add("PortalId");

                result.Sort();

                return Request.CreateResponse(HttpStatusCode.OK, result);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage UpdateProfileSettings(ProfileMappings profileMappings)
        {
            try
            {
                ProfileMappings.UpdateProfileMappings(profileMappings);
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        // POST: api/RedisCaching/UpdateGeneralSettings
        /// <summary>
        /// Updates the general settings
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage UpdateAdvancedSettings(AzureADB2CProviderSettings settings)
        {
            try
            {
                if (!UserInfo.IsSuperUser)
                {
                    var config = new AzureConfig("AzureB2C", PortalId);
                    if (config.UseGlobalSettings || config.UseGlobalSettings != settings.UseGlobalSettings)
                        return Request.CreateResponse(HttpStatusCode.Forbidden, "Only super users can change this setting");
                }
                AzureADB2CProviderSettings.SaveAdvancedSettings("AzureB2C", PortalId, settings);
                ProfileMappings.UpdateProfileMappingsExtensionNames(HttpContext.Current.Server.MapPath(ProfileMappings.DefaultProfileMappingsFilePath), PortalId);
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }



        private void AddUserProfilePage(int portalId, bool setAsPortalUserProfile)
        {            
            var tabController = new TabController();
            var moduleController = new ModuleController();

            // Get Azure AD B2C User profile module
            var moduleFriendlyName = "AzureAD.B2C.UserProfile";
            var moduleDef = ModuleDefinitionController.GetModuleDefinitionByFriendlyName(moduleFriendlyName);

            //Add User Profile page under root
            var parentId = TabController.GetTabByTabPath(PortalId, "//", Null.NullString);
            var tabId = TabController.GetTabByTabPath(PortalId, "//UserProfile", Null.NullString);
            int moduleId;
            TabInfo tabInfo;
            

            if (tabId == Null.NullInteger)
            {
                tabInfo = new TabInfo()
                {
                    PortalID = PortalId,
                    TabName = "UserProfile",
                    Title = "User Profile",
                    Description = "Manage Azure AD B2C User profile",
                    KeyWords = "profile",
                    IsDeleted = false,
                    IsSuperTab = false,
                    IsVisible = false,
                    DisableLink = false,
                    IconFile = null,
                    SiteMapPriority = 0,                    
                    Url = null,
                    ParentId = parentId
                };
                tabInfo.TabSettings.Add("AllowIndex", "False");
                // Add User Profile page                
                tabId = tabController.AddTab(tabInfo);
                

                // Allow access to "Registered Users" role                
                var permissions = new TabPermissionInfo()
                {
                    AllowAccess = true,
                    RoleID = PortalSettings.RegisteredRoleId,
                    RoleName = PortalSettings.RegisteredRoleName,
                    TabID = tabId,
                    PermissionID = 3 // View
                };
                tabInfo.TabPermissions.Add(permissions, true);
                PermissionProvider.Instance().SaveTabPermissions(tabInfo);
           
            }
            else
            {
                tabInfo = tabController.GetTab(tabId, PortalId, false);

                foreach (var kvp in moduleController.GetTabModules(tabId))
                {
                    if (kvp.Value.DesktopModule.ModuleName == moduleFriendlyName)
                    {
                        // Preview module so hard delete
                        moduleController.DeleteTabModule(tabId, kvp.Value.ModuleID, false);
                        break;
                    }
                }
            }

            //Add module to page
            moduleId = Upgrade.AddModuleToPage(tabInfo, moduleDef.ModuleDefID, "User Profile", "", true);

            var moduleTab = moduleController.GetTabModules(tabInfo.TabID)
                .FirstOrDefault(x => x.Value.ModuleID == moduleId);
            if (moduleTab.Value != null)
            {
                moduleController.UpdateTabModuleSetting(moduleTab.Value.TabModuleID, "hideadminborder", "True");
            }

            // Change User profile page on the Portal settings
            var portalInfo = PortalController.Instance.GetPortal(PortalId);
            portalInfo.UserTabId = setAsPortalUserProfile ? tabId : Null.NullInteger;
            PortalController.Instance.UpdatePortalInfo(portalInfo);
        }
    }
}
