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
using DotNetNuke.Entities.Modules;
using DotNetNuke.Services.Authentication;
using DotNetNuke.Services.Authentication.OAuth;
using DotNetNuke.Services.Localization;
using DotNetNuke.UI.Skins.Controls;
using log4net;

#endregion

namespace DotNetNuke.Authentication.Azure.B2C
{
    public partial class UserProfile : UserModuleBase
    {
        private ILog _logger = LogManager.GetLogger(typeof(UserProfile));

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
                    var url = Request["state"].Split('-');
                    if (url.Length > 1)
                    {
                        Response.Redirect(url[1], true);
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
                if (UserInfo != null && UserInfo.Username.ToLowerInvariant().StartsWith("azureb2c-"))
                {
                    var oauthClient = new AzureClient(PortalId, AuthMode.Login);
                    // Is returning after editing the user profile?
                    if (Request.UrlReferrer?.Host == oauthClient.LogoutEndpoint.Host
                        && !string.IsNullOrEmpty(Request["state"])
                        && Request["state"].StartsWith(oauthClient.Service)
                        && Request["state"].Length > oauthClient.Service.Length
                        && oauthClient.HaveVerificationCode())
                    {
                        oauthClient.Policy = AzureClient.PolicyEnum.ProfilePolicy;
                        AuthorisationResult result = oauthClient.Authorize();
                        if (result != AuthorisationResult.Denied)
                        {
                            oauthClient.UpdateUserProfile();
                            var url = Request["state"].Split('-');
                            if (url.Length > 1)
                                Response.Redirect(url[1]);
                        }
                    }
                    else
                    {
                        oauthClient.NavigateUserProfile(Request.UrlReferrer);
                    }
                }
            }
        }
    }
}