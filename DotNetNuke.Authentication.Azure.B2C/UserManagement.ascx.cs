#region Copyright

// 
// Intelequia Software solutions - https://intelequia.com
// Copyright (c) 2019
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

#region Usings

using System;
using System.Globalization;
using DotNetNuke.Authentication.Azure.B2C.Common;
using DotNetNuke.Authentication.Azure.B2C.Components;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Framework;
using DotNetNuke.Framework.JavaScriptLibraries;
using DotNetNuke.Services.Authentication;
using DotNetNuke.Services.Authentication.OAuth;
using DotNetNuke.Services.Exceptions;
using DotNetNuke.Services.Localization;
using DotNetNuke.UI.Skins.Controls;
using DotNetNuke.UI.Utilities;
using log4net;

#endregion

namespace DotNetNuke.Authentication.Azure.B2C
{
    public partial class UserManagement : PortalModuleBase
    {
        public AzureConfig AzureConfig { get; set; }

        public bool CanImpersonate
        {
            get
            {
                var user = UserInfo;
                Security.Profile.ProfileProvider.Instance().GetUserProfile(ref user);
                var identitySource = user.Profile.GetPropertyValue("IdentitySource");
                return identitySource == "Azure-B2C" && !string.IsNullOrEmpty(AzureConfig.ImpersonatePolicy);
            }
        }

        public bool EnableAdd
        {
            get
            {
                return bool.Parse(Utils.GetTabModuleSetting(TabModuleId, "EnableAdd", "True"));
            }
        }
        public bool EnableUpdate
        {
            get
            {
                return bool.Parse(Utils.GetTabModuleSetting(TabModuleId, "EnableUpdate", "True"));
            }
        }
        public bool EnableDelete
        {
            get
            {
                return bool.Parse(Utils.GetTabModuleSetting(TabModuleId, "EnableDelete", "True"));
            }
        }
        public bool EnableImpersonate
        {
            get
            {
                return bool.Parse(Utils.GetTabModuleSetting(TabModuleId, "EnableImpersonate", "True"));
            }
        }
        public bool EnableExport
        {
            get
            {
                return bool.Parse(Utils.GetTabModuleSetting(TabModuleId, "EnableExport", "True"));
            }
        }

        public string[] CustomFields
        {
            get
            {
                return Utils.GetTabModuleSetting(TabModuleId, "CustomFields").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        #region "Event Handlers"

        protected override void OnInit(EventArgs e)
        {
            InitializeComponent();
            base.OnInit(e);
        }

        private void InitializeComponent()
        {
            Load += Page_Load;
            AzureConfig = new AzureConfig(AzureConfig.ServiceName, this.PortalId);
            JavaScript.RequestRegistration(CommonJs.DnnPlugins);
            ServicesFramework.Instance.RequestAjaxScriptSupport();
            ServicesFramework.Instance.RequestAjaxAntiForgerySupport();
        }
        /// ----------------------------------------------------------------------------- 
        /// <summary> 
        /// Page_Load runs when the control is loaded 
        /// </summary> 
        /// ----------------------------------------------------------------------------- 
        protected void Page_Load(object sender, System.EventArgs e)
        {
            try
            {
                ClientAPI.RegisterClientVariable(Page, "portalId", PortalId.ToString(CultureInfo.InvariantCulture), true);
                ClientAPI.RegisterClientVariable(Page, "moduleId", ModuleId.ToString(CultureInfo.InvariantCulture), true);

                ClientAPI.RegisterClientVariable(Page, "AreYouSure", LocalizeString("AreYouSure"), true);
                ClientAPI.RegisterClientVariable(Page, "DeleteMessage", LocalizeString("DeleteMessage"), true);
                ClientAPI.RegisterClientVariable(Page, "ExportMessage", LocalizeString("ExportMessage"), true);
                ClientAPI.RegisterClientVariable(Page, "YesDownload", LocalizeString("YesDownload"), true);
                ClientAPI.RegisterClientVariable(Page, "Cancel", LocalizeString("Cancel"), true);
                ClientAPI.RegisterClientVariable(Page, "customAttributes", Utils.GetTabModuleSetting(TabModuleId, "CustomFields").Replace(" ", ""), true);

                var settings = new AzureConfig(AzureConfig.ServiceName, PortalId);
                ClientAPI.RegisterClientVariable(Page, "customAttributesPrefix", $"extension_{settings.B2cApplicationId.Replace("-", "")}_", true);

            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        #endregion

    }
}
