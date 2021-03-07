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

using DotNetNuke.Authentication.Azure.B2C.Common;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Services.Exceptions;
using DotNetNuke.Services.Localization;
using DotNetNuke.UI.Utilities;
using System;
using System.Globalization;

#endregion

namespace DotNetNuke.Authentication.Azure.B2C
{
    public partial class UserManagementSettings : ModuleSettingsBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {

            }
            catch (Exception exc) //Module failed to load
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        public override void LoadSettings()
        {
            try
            {
                if (!Page.IsPostBack)
                {
                    chkEnableAdd.Checked = bool.Parse(Utils.GetTabModuleSetting(TabModuleId, "EnableAdd", "True"));
                    chkEnableUpdate.Checked = bool.Parse(Utils.GetTabModuleSetting(TabModuleId, "EnableUpdate", "True"));
                    chkEnableDelete.Checked = bool.Parse(Utils.GetTabModuleSetting(TabModuleId, "EnableDelete", "True"));
                    chkEnableImpersonate.Checked = bool.Parse(Utils.GetTabModuleSetting(TabModuleId, "EnableImpersonate", "True"));
                    chkEnableExport.Checked = bool.Parse(Utils.GetTabModuleSetting(TabModuleId, "EnableExport", "True"));
                    txtCustomFields.Text = Utils.GetTabModuleSetting(TabModuleId, "CustomFields");
                }
            }
            catch (Exception e)
            {
                Exceptions.ProcessModuleLoadException(this, e);
            }
        }

        public override void UpdateSettings()
        {
            try
            {
                ModuleController.Instance.UpdateTabModuleSetting(TabModuleId, "EnableAdd", chkEnableAdd.Checked.ToString());
                ModuleController.Instance.UpdateTabModuleSetting(TabModuleId, "EnableUpdate", chkEnableUpdate.Checked.ToString());
                ModuleController.Instance.UpdateTabModuleSetting(TabModuleId, "EnableDelete", chkEnableDelete.Checked.ToString());
                ModuleController.Instance.UpdateTabModuleSetting(TabModuleId, "EnableImpersonate", chkEnableImpersonate.Checked.ToString());
                ModuleController.Instance.UpdateTabModuleSetting(TabModuleId, "EnableExport", chkEnableExport.Checked.ToString());
                ModuleController.Instance.UpdateTabModuleSetting(TabModuleId, "CustomFields", txtCustomFields.Text.Trim());
            }
            catch (Exception e)
            {
                Exceptions.ProcessModuleLoadException(this, e);
            }
        }

    }
}
