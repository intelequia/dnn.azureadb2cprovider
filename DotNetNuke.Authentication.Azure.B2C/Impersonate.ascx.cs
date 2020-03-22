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
using System.Web;
using DotNetNuke.Authentication.Azure.B2C.Components;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Services.Authentication;
using DotNetNuke.Services.Authentication.OAuth;
using DotNetNuke.Services.Localization;
using DotNetNuke.UI.Skins.Controls;
using log4net;
using System.Linq;

#endregion

namespace DotNetNuke.Authentication.Azure.B2C
{
    public partial class Impersonate : UserModuleBase
    {
        private ILog _logger = LogManager.GetLogger(typeof(Impersonate));

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            if (!string.IsNullOrEmpty(Request["error"])
                && !string.IsNullOrEmpty(Request["error_description"]))
            {
                // AADB2C90091: The user has cancelled entering self-asserted information. 
                // User clicked on Cancel when resetting the password => Redirect to the original page
                if (Request["error_description"]?.IndexOf("AADB2C90091") > -1)
                {
                    if (!string.IsNullOrEmpty(Request["state"])) {
                        var state = new State(Request["state"]);
                        if (!string.IsNullOrEmpty(state.RedirectUrl))
                        {
                            Response.Redirect(state.RedirectUrl, true);
                        }
                        else
                        {
                            Response.Redirect("/", true);
                        }
                    }
                    else
                    {
                        Response.Redirect("/", true);
                    }
                }
                else
                {
                    var errorMessage = Localization.GetString("LoginError", LocalResourceFile);
                    errorMessage = string.Format(errorMessage, Request["error"], Request["error_description"]);
                    _logger.Error(errorMessage);
                    UI.Skins.Skin.AddModuleMessage(this, errorMessage, ModuleMessage.ModuleMessageType.RedError);
                }
            }
            else
            {
                var identityProvider = UserInfo.Profile.ProfileProperties.GetByName("IdentitySource");
                if (identityProvider != null && identityProvider.PropertyValue == "Azure-B2C")
                {
                    var oauthClient = new AzureClient(PortalId, AuthMode.Login);
                    if (HttpContext.Current.Request.Cookies.AllKeys.Contains("AzureB2CUserToken"))
                    {
                        // Logout on B2C to clear the cached B2C login. This will redirect back to here after the logout
                        oauthClient.Logout();
                        return;
                    }

                    // Is returning after running the impersonation on B2C?
                    var state = new State(Request["state"]);
                    if (Request.UrlReferrer?.Host == oauthClient.LogoutEndpoint.Host
                        && !string.IsNullOrEmpty(Request["state"])
                        && state.Service == oauthClient.Service
                        && !string.IsNullOrEmpty(state.RedirectUrl)
                        && oauthClient.HaveVerificationCode())
                    {
                        oauthClient.Policy = AzureClient.PolicyEnum.ImpersonatePolicy;
                        AuthorisationResult result = oauthClient.Authorize();
                        if (result != AuthorisationResult.Denied)
                        {
                            if (User != null)
                            {
                                oauthClient.Impersonate();
                                Response.Redirect("/"); //  Redirect to homepage after impersonation
                            }
                        }
                    }
                    else
                    {
                        var uri = oauthClient.NavigateImpersonation(Request.UrlReferrer, UserInfo.Email);
                        Response.Redirect(uri.ToString(), false);
                    }
                }
            }
        }
    }
}