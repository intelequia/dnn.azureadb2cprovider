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
using DotNetNuke.Authentication.Azure.B2C.Components;
using DotNetNuke.Common;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Security.Permissions;
using DotNetNuke.Services.Authentication;

#endregion

namespace DotNetNuke.Authentication.Azure.B2C
{
    public partial class Logoff : AuthenticationLogoffBase
    {
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            var oauthClient = new AzureClient(PortalId, AuthMode.Login);
            oauthClient.Logout();
            OnLogOff(e);    
            // Fix: Avoid redirecting to the B2C logout path and query
            if (Request.UrlReferrer != null 
                && Request.UrlReferrer.Host == oauthClient.LogoutEndpoint.Host
                && PortalSettings.Registration.RedirectAfterLogout == Null.NullInteger
                && TabPermissionController.CanViewPage()
                && !(PortalSettings.ActiveTab.TabID == PortalSettings.UserTabId 
                    || PortalSettings.ActiveTab.ParentId == PortalSettings.UserTabId))
            {
                Response.Redirect(Globals.NavigateURL(PortalSettings.ActiveTab.TabID));
            }
            else
            {
                OnRedirect(e);
            }
            
        }
    }
}