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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Dnn.PersonaBar.Library;
using Dnn.PersonaBar.Library.Attributes;
using DotNetNuke.Authentication.Azure.B2C.Components;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Modules.Definitions;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Tabs;
using DotNetNuke.Instrumentation;
using DotNetNuke.Security.Permissions;
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
