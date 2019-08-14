﻿#region Copyright

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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using DotNetNuke.Authentication.Azure.B2C.Common;
using DotNetNuke.Authentication.Azure.B2C.Components.Graph;
using DotNetNuke.Common;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Users;
using DotNetNuke.Instrumentation;
using DotNetNuke.Services.Authentication;
using DotNetNuke.Services.Authentication.OAuth;
using DotNetNuke.Services.FileSystem;

#endregion

namespace DotNetNuke.Authentication.Azure.B2C.Components
{
    public class AzureClient : OAuthClientBase
    {
        public enum PolicyEnum
        {
            SignUpPolicy,
            PasswordResetPolicy,
            ProfilePolicy
        }

        private const string TokenEndpointPattern = "https://{0}.b2clogin.com/{1}/oauth2/v2.0/token";
        private const string LogoutEndpointPattern = "https://{0}.b2clogin.com/{1}/oauth2/v2.0/logout?p={2}&post_logout_redirect_uri={3}";
        private const string AuthorizationEndpointPattern = "https://{0}.b2clogin.com/{1}/oauth2/v2.0/authorize";
        private const string GraphEndpointPattern = "https://graph.windows.net/{0}";
        private const string ProfileMappingsFilePath = "~/DesktopModules/AuthenticationServices/AzureB2C/DnnProfileMappings.config";
        private const string RoleMappingsFilePath = "~/DesktopModules/AuthenticationServices/AzureB2C/DnnRoleMappings.config";

        private static readonly ILog Logger = LoggerSource.Instance.GetLogger(typeof(AzureClient));
        private GraphClient _graphClient;
        private GraphClient GraphClient
        {
            get
            {
                if (_graphClient == null)
                {
                    if (string.IsNullOrEmpty(Settings.AADApplicationId) || string.IsNullOrEmpty(Settings.AADApplicationKey))
                    {
                        throw new Exception("AAD application ID or key are not valid");
                    }

                    _graphClient = new GraphClient(Settings.AADApplicationId, Settings.AADApplicationKey, Settings.TenantId);
                }
                return _graphClient;
            }
        }


        private string SignUpPolicyName => PortalController.GetPortalSetting($"{Service}_SignUpPolicy", PortalSettings.Current.PortalId, "");
        private string PasswordResetPolicyName => PortalController.GetPortalSetting($"{Service}_PasswordResetPolicy", PortalSettings.Current.PortalId, "");
        private string ProfilePolicyName => PortalController.GetPortalSetting($"{Service}_ProfilePolicy", PortalSettings.Current.PortalId, "");
        private readonly AzureConfig Settings;

        private ProfileMappings _customClaimsMappings;
        public ProfileMappings CustomClaimsMappings
        {
            get
            {
                if (_customClaimsMappings == null)
                {
                    _customClaimsMappings = ProfileMappings.GetProfileMappings(HttpContext.Current.Server.MapPath(ProfileMappingsFilePath));
                }

                return _customClaimsMappings;
            }
        }

        private RoleMappings _customRoleMappings;
        public RoleMappings CustomRoleMappings
        {
            get
            {
                if (_customRoleMappings == null)
                {
                    _customRoleMappings = RoleMappings.GetRoleMappings(HttpContext.Current.Server.MapPath(RoleMappingsFilePath));
                }

                return _customRoleMappings;
            }
        }

        public PolicyEnum Policy { get; set; }

        public string PolicyName
        {
            get
            {
                switch (Policy)
                {
                    case PolicyEnum.PasswordResetPolicy: return PasswordResetPolicyName;
                    case PolicyEnum.ProfilePolicy: return ProfilePolicyName;
                    default: return SignUpPolicyName;
                }
            }
        }

        #region Constructors

        private JwtSecurityToken JwtIdToken { get; set; }
        public Uri LogoutEndpoint { get; }


        public AzureClient(int portalId, AuthMode mode)
            : base(portalId, mode, "AzureB2C")
        {
            Settings = new AzureConfig("AzureB2C", portalId);

            TokenMethod = HttpMethod.POST;

            if (!string.IsNullOrEmpty(Settings.TenantName) && !string.IsNullOrEmpty(Settings.TenantId))
            {
                TokenEndpoint = new Uri(string.Format(Utils.GetAppSetting("AzureADB2C.TokenEndpointPattern", TokenEndpointPattern), Settings.TenantName, Settings.TenantId));
                LogoutEndpoint = new Uri(string.Format(Utils.GetAppSetting("AzureADB2C.LogoutEndpointPattern", LogoutEndpointPattern), Settings.TenantName, Settings.TenantId, Settings.SignUpPolicy, UrlEncode(HttpContext.Current.Request.Url.ToString())));
                AuthorizationEndpoint = new Uri(string.Format(Utils.GetAppSetting("AzureADB2C.AuthorizationEndpointPattern", AuthorizationEndpointPattern), Settings.TenantName, Settings.TenantId));
                MeGraphEndpoint = new Uri(string.Format(Utils.GetAppSetting("AzureADB2C.GraphEndpointPattern", GraphEndpointPattern), Settings.TenantId));
            }

            if (string.IsNullOrEmpty(Settings.APIResource) && string.IsNullOrEmpty(Settings.Scopes))
            {
                Scope = Settings.APIKey;
                APIResource = Settings.APIKey;
            }
            else
            {
                Scope = string.Join(" ", Settings.Scopes
                    .Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => $"{Settings.APIResource}{x.Trim()}"));
                APIResource = Settings.APIResource;
            }
            APIKey = Settings.APIKey;
            AuthTokenName = "AzureB2CUserToken";
            OAuthVersion = "2.0";
            OAuthHeaderCode = "Basic";
            LoadTokenCookie(string.Empty);
            JwtIdToken = null;
            Policy = PolicyEnum.SignUpPolicy;
        }

        #endregion

        protected override TimeSpan GetExpiry(string responseText)
        {
            var jsonSerializer = new JavaScriptSerializer();
            var tokenDictionary = jsonSerializer.DeserializeObject(responseText) as Dictionary<string, object>;

            return new TimeSpan(0, 0, Convert.ToInt32(tokenDictionary["expires_in"]));
        }

        protected override string GetToken(string responseText)
        {
            if (string.IsNullOrEmpty(responseText))
            {
                throw new Exception("There was an error processing the credentials. Contact your system administrator.");
            }
            var jsonSerializer = new JavaScriptSerializer();
            var tokenDictionary = jsonSerializer.DeserializeObject(responseText) as Dictionary<string, object>;
            var token = Convert.ToString(tokenDictionary["access_token"]);
            JwtIdToken = new JwtSecurityToken(Convert.ToString(tokenDictionary["access_token"]));
            return token;
        }

        public override TUserData GetCurrentUser<TUserData>()
        {
            LoadTokenCookie(String.Empty);
            return GetCurrentUserInternal() as TUserData;
        }

        private AzureUserData GetCurrentUserInternal(JwtSecurityToken pToken = null)
        {
            if (pToken == null && (!IsCurrentUserAuthorized() || JwtIdToken == null))
            {
                return null;
            }
            var claims = JwtIdToken.Claims.ToArray();
            EnsureClaimExists(claims, JwtRegisteredClaimNames.GivenName);
            EnsureClaimExists(claims, JwtRegisteredClaimNames.FamilyName);
            EnsureClaimExists(claims, "emails");
            EnsureClaimExists(claims, "sub");

            var user = new AzureUserData()
            {
                AzureFirstName = claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.GivenName)?.Value,
                AzureLastName = claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.FamilyName)?.Value,
                Email = claims.FirstOrDefault(x => x.Type == "emails")?.Value,
                Id = claims.FirstOrDefault(x => x.Type == "sub").Value
            };
            user.AzureDisplayName = $"{user.AzureFirstName} {user.AzureLastName}";
            return user;
        }

        private void EnsureClaimExists(System.Security.Claims.Claim[] claims, string claimName)
        {
            var claim = claims.FirstOrDefault(x => x.Type == claimName)?.Value;
            if (string.IsNullOrEmpty(claim))
            {
                throw new ApplicationException($"Claim '{claimName}' was not found on the token. Ensure you have add it to the user flow (policy) application claims in the Azure Portal");
            }
        }

        public void AddCustomProperties(NameValueCollection properties)
        {
            if (!Settings.ProfileSyncEnabled)
            {
                return;
            }

            var claims = JwtIdToken.Claims.ToArray();

            foreach (var claim in claims)
            {
                switch (claim.Type)
                {
                    case "emails":
                        properties.Set("Email", claim.Value);
                        break;
                    case "city":
                        properties.Set("City", claim.Value);
                        break;
                    case "country":
                        properties.Set("Country", claim.Value);
                        break;
                    case "name":
                        properties.Set("FirstName", claim.Value);
                        break;
                    case "given_name":
                        properties.Set("FirstName", claim.Value);
                        break;
                    case "family_name":
                        properties.Set("LastName", claim.Value);
                        break;
                    case "postalCode":
                        properties.Set("PostalCode", claim.Value);
                        break;
                    case "state":
                        properties.Set("Region", claim.Value);
                        break;
                    case "streetAddress":
                        properties.Set("Street", claim.Value);
                        break;
                    case "exp":
                    case "nbf":
                    case "ver":
                    case "iss":
                    case "sub":
                    case "aud":
                    case "iat":
                    case "auth_time":
                    case "oid":
                    case "tfp":
                    case "at_hash":
                        break;
                    default:
                        // If we're here, "claim" is not a B2C built-in claim
                        // So, we have to map this custom claim to a DNN profile property
                        var mapping = CustomClaimsMappings.ProfileMapping.FirstOrDefault(c => $"extension_{c.B2cClaimName.ToLower()}" == claim.Type.ToLower());
                        if (mapping != null)
                        {
                            properties.Add(mapping.DnnProfilePropertyName, claim.Value);
                        }
                        break;
                }
            }
        }


        public void UpdateUserProfile(JwtSecurityToken pToken = null)
        {
            if (pToken == null && (!IsCurrentUserAuthorized() || JwtIdToken == null))
            {
                return;
            }
            if (pToken != null)
            {
                JwtIdToken = pToken;
            }
            var user = GetCurrentUserInternal(pToken);
            // Update user
            var userInfo = UserController.GetUserByEmail(PortalSettings.Current.PortalId, user.Email);

            userInfo.FirstName = user.FirstName;
            userInfo.LastName = user.LastName;
            userInfo.DisplayName = user.DisplayName;

            if (Settings.ProfileSyncEnabled)
            {
                var properties = new NameValueCollection();
                AddCustomProperties(properties);
                foreach (var prop in properties.AllKeys)
                {
                    if (userInfo.Profile.GetPropertyValue(prop) != properties[prop])
                    {
                        userInfo.Profile.SetProfileProperty(prop, properties[prop]);
                    }
                }
                UpdateUserProfilePicture(JwtIdToken.Claims.First(c => c.Type == "sub").Value, userInfo);
            }
            UserController.UpdateUser(PortalSettings.Current.PortalId, userInfo);

            // Update user roles
            UpdateUserRoles(JwtIdToken.Claims.First(c => c.Type == "sub").Value, userInfo);

        }

        public void Logout()
        {
            if (HttpContext.Current.Request.Cookies.AllKeys.Contains(AuthTokenName)
                && (!(HttpContext.Current.Request.Cookies[AuthTokenName].Expires < DateTime.UtcNow.AddDays(-1))
                    || HttpContext.Current.Request.Cookies[AuthTokenName].Expires == DateTime.MinValue))
            {
                RemoveToken();
                HttpContext.Current.Response.Redirect(LogoutEndpoint.ToString(), true);
                //HttpContext.Current.ApplicationInstance.CompleteRequest();
            }
        }

        public void NavigateUserProfile(Uri redirectAfterEditUri = null)
        {
            var parameters = new List<QueryParameter>
                {
                    new QueryParameter("scope", Scope),
                    new QueryParameter("client_id", APIKey),
                    new QueryParameter("redirect_uri", HttpContext.Current.Server.UrlEncode($"{CallbackUri.Scheme}://{CallbackUri.Host}/UserProfile")),
                    new QueryParameter("state", HttpContext.Current.Server.UrlEncode($"{Service}-{redirectAfterEditUri}")),
                    new QueryParameter("response_type", "code"),
                    new QueryParameter("response_mode", "query"),
                    new QueryParameter("p", ProfilePolicyName)
                };

            HttpContext.Current.Response.Redirect(AuthorizationEndpoint + "?" + parameters.ToNormalizedString(), false);
        }

        public override void AuthenticateUser(UserData user, PortalSettings settings, string IPAddress, Action<NameValueCollection> addCustomProperties, Action<UserAuthenticatedEventArgs> onAuthenticated)
        {
            var userInfo = UserController.GetUserByEmail(PortalSettings.Current.PortalId, user.Email);
            // If user doesn't exist, AuthenticateUser() will create it. Otherwise, AuthenticateUser will perform a Response.Redirect, so we have to sincronize the roles before that, to avoid the ThreadAbortException caused by the Response.Redirect
            if (userInfo == null)
            {
                base.AuthenticateUser(user, settings, IPAddress, addCustomProperties, onAuthenticated);
                if (IsCurrentUserAuthorized())
                {
                    userInfo = UserController.GetUserByEmail(PortalSettings.Current.PortalId, user.Email);
                    UpdateUserRoles(JwtIdToken.Claims.First(c => c.Type == "sub").Value, userInfo);
                    UpdateUserProfilePicture(JwtIdToken.Claims.First(c => c.Type == "sub").Value, userInfo, true);
                }
            }
            else
            {
                if (IsCurrentUserAuthorized())
                {
                    UpdateUserRoles(JwtIdToken.Claims.First(c => c.Type == "sub").Value, userInfo);
                    UpdateUserProfilePicture(JwtIdToken.Claims.First(c => c.Type == "sub").Value, userInfo, true);
                }
                base.AuthenticateUser(user, settings, IPAddress, addCustomProperties, onAuthenticated);
            }
        }

        public override AuthorisationResult Authorize()
        {
            string errorReason = HttpContext.Current.Request.Params["error_reason"];
            bool userDenied = (errorReason != null);
            if (userDenied)
            {
                return AuthorisationResult.Denied;
            }

            if (HttpContext.Current.Request.UrlReferrer.Query.IndexOf("p=" + PasswordResetPolicyName + "&") > -1)
            {
                Policy = PolicyEnum.PasswordResetPolicy;
            }

            if (!HaveVerificationCode())
            {
                var parameters = new List<QueryParameter>
                {
                    new QueryParameter("scope", Scope),
                    new QueryParameter("client_id", APIKey),
                    new QueryParameter("redirect_uri", HttpContext.Current.Server.UrlEncode(CallbackUri.ToString())),
                    new QueryParameter("state", Service),
                    new QueryParameter("response_type", "code"),
                    new QueryParameter("response_mode", "query"),
                    new QueryParameter("p", PolicyName)
                };

                HttpContext.Current.Response.Redirect(AuthorizationEndpoint + "?" + parameters.ToNormalizedString(), false);
                return AuthorisationResult.RequestingCode;
            }

            ExchangeCodeForToken();

            SaveTokenCookie(string.IsNullOrEmpty(AuthToken));
            return string.IsNullOrEmpty(AuthToken) ? AuthorisationResult.Denied : AuthorisationResult.Authorized;
        }

        private void SaveTokenCookie(bool expireCookie = false)
        {
            var authTokenCookie = HttpContext.Current.Response.Cookies[$"{Service}UserToken"];
            if (authTokenCookie == null)
            {
                authTokenCookie = new HttpCookie(AuthTokenName);
            }
            authTokenCookie.Path = (!string.IsNullOrEmpty(Globals.ApplicationPath) ? Globals.ApplicationPath : "/");
            if (expireCookie)
            {
                authTokenCookie.Value = null;
            }
            else
            {
                authTokenCookie.Values[OAuthTokenKey] = AuthToken;
            }
            authTokenCookie.Expires = expireCookie ? DateTime.Now.AddYears(-30) : DateTime.Now.Add(AuthTokenExpiry);
            HttpContext.Current.Response.SetCookie(authTokenCookie);
        }

        private void UpdateUserRoles(string aadUserId, UserInfo userInfo)
        {
            //Skip if RoleSync is not enabled, or if no role mappings are defined.
            if (!Settings.RoleSyncEnabled || CustomRoleMappings == null || CustomRoleMappings.RoleMapping == null || CustomRoleMappings.RoleMapping.Length == 0)
            {
                return;
            }

            try
            {
                var aadGroups = GraphClient.GetUserGroups(aadUserId);

                if (aadGroups != null && aadGroups.Values != null)
                {

                    //Dictionary<string, RoleMappingsRoleMapping> dnnRoleMap = CustomRoleMappings.RoleMapping.ToDictionary(s => s.DnnRoleName);
                    //Dictionary<string, RoleMappingsRoleMapping> b2cRoleMap = CustomRoleMappings.RoleMapping.ToDictionary(s => s.B2cRoleName);

                    //Iterate all mapped roles/groups
                    foreach (var mappedRole in CustomRoleMappings.RoleMapping)
                    {
                        var aadCurrentGroup = aadGroups.Values.FirstOrDefault(s => s.DisplayName == mappedRole.B2cRoleName);

                        //If user does not have group in b2c, remove it fron DNN.
                        if (aadCurrentGroup == null && userInfo.Roles.Contains(mappedRole.DnnRoleName))
                        {

                            var role = Security.Roles.RoleController.Instance.GetRoleByName(PortalSettings.Current.PortalId, mappedRole.DnnRoleName);
                            Security.Roles.RoleController.DeleteUserRole(userInfo, role, PortalSettings.Current, false);
                        }
                        //If user has b2c group but not dnn, add it.
                        else if (userInfo.Roles.Contains(mappedRole.DnnRoleName) == false)
                        {
                            var dnnRole = Security.Roles.RoleController.Instance.GetRoleByName(PortalSettings.Current.PortalId, mappedRole.DnnRoleName);

                            if (dnnRole == null)
                            {
                                var dnnRoleId = Security.Roles.RoleController.Instance.AddRole(new Security.Roles.RoleInfo
                                {
                                    RoleName = mappedRole.DnnRoleName,
                                    Description = aadGroups.Values.First(s => s.DisplayName == mappedRole.B2cRoleName).Description,
                                    PortalID = PortalSettings.Current.PortalId,
                                    Status = Security.Roles.RoleStatus.Approved,
                                    RoleGroupID = -1,
                                    AutoAssignment = false,
                                    IsPublic = false,
                                });

                                dnnRole = Security.Roles.RoleController.Instance.GetRoleById(PortalSettings.Current.PortalId, dnnRoleId);
                            }
                            Security.Roles.RoleController.Instance.AddUserRole(
                                PortalSettings.Current.PortalId,
                                userInfo.UserID,
                                dnnRole.RoleID,
                                Security.Roles.RoleStatus.Approved,
                                false,
                                aadCurrentGroup.CreatedDateTime.HasValue ? aadCurrentGroup.CreatedDateTime.Value.DateTime.ToLocalTime() : DateTime.Today,   //NOTE: CreatedDateTime is always UTC, so convert to local time zone to prevent future dated role membership.
                                DateTime.MaxValue);
                        }

                    }

                }

                ////Check if there are any mapped roles that user has in DNN, that aren't in their B2C groups, and should be removed from DNN user roles.
                //foreach (var dnnCurrentMappedRole in userInfo.Roles.Where(r => dnnRoleMap.Keys.Contains(r)))
                //{
                //    if (aadGroups.Values.FirstOrDefault(g => g.DisplayName == dnnRoleMap[dnnCurrentMappedRole].B2cRoleName) == null)
                //    {
                //        var role = Security.Roles.RoleController.Instance.GetRoleByName(PortalSettings.Current.PortalId, dnnCurrentMappedRole);
                //        Security.Roles.RoleController.DeleteUserRole(userInfo, role, PortalSettings.Current, false);
                //    }

                //}

                ////Check for any mapped roles that user does not have in DNN, that are in B2C groups, and should be added to DNN user roles.
                //foreach (var b2cCurrentMappedGroup in aadGroups.Values.Where(r => b2cRoleMap.Keys.Contains(r.DisplayName)))
                //{
                //    if (userInfo.Roles.Contains(b2cRoleMap[b2cCurrentMappedGroup.DisplayName].DnnRoleName) == false)
                //    {
                //        var dnnRole = Security.Roles.RoleController.Instance.GetRoleByName(PortalSettings.Current.PortalId, b2cRoleMap[b2cCurrentMappedGroup.DisplayName].DnnRoleName);

                //        if (dnnRole == null)
                //        {
                //            var dnnRoleId = Security.Roles.RoleController.Instance.AddRole(new Security.Roles.RoleInfo
                //            {
                //                RoleName = b2cRoleMap[b2cCurrentMappedGroup.DisplayName].DnnRoleName,
                //                Description = b2cCurrentMappedGroup.Description,
                //                PortalID = PortalSettings.Current.PortalId,
                //                Status = Security.Roles.RoleStatus.Approved,
                //                RoleGroupID = -1,
                //                AutoAssignment = false,
                //                IsPublic = false
                //            });

                //            dnnRole = Security.Roles.RoleController.Instance.GetRoleById(PortalSettings.Current.PortalId, dnnRoleId);
                //        }

                //        Security.Roles.RoleController.Instance.AddUserRole(PortalSettings.Current.PortalId, userInfo.UserID, dnnRole.RoleID, Security.Roles.RoleStatus.Approved, false, b2cCurrentMappedGroup.CreatedDateTime.HasValue ? b2cCurrentMappedGroup.CreatedDateTime.Value.DateTime.ToLocalTime() : DateTime.Today, DateTime.MaxValue);
                //    }
                //}

            }
            catch (Exception e)
            {
                Logger.Warn($"Error while synchronizing the user roles from user {aadUserId}", e);
            }
        }

        private void UpdateUserProfilePicture(string aadUserId, UserInfo userInfo, bool saveUserInfo = false)
        {
            try
            {
                if (!string.IsNullOrEmpty(aadUserId) && userInfo != null)
                {
                    var profilePictureMetadata = GraphClient.GetUserProfilePictureMetadata(aadUserId);
                    if (profilePictureMetadata != null && !string.IsNullOrEmpty(profilePictureMetadata.ODataMediaContentType))
                    {
                        var pictureBytes = GraphClient.GetUserProfilePicture(aadUserId);
                        var userFolder = FolderManager.Instance.GetUserFolder(userInfo);
                        var stream = new MemoryStream(pictureBytes);
                        var profilePictureInfo = FileManager.Instance.AddFile(userFolder,
                            $"{aadUserId}.{GetExtensionFromMediaContentType(profilePictureMetadata.ODataMediaContentType)}",
                            stream, true);

                        userInfo.Profile.Photo = profilePictureInfo.FileId.ToString();
                    }
                    else
                    {
                        userInfo.Profile.Photo = "";
                    }
                    if (saveUserInfo)
                    {
                        UserController.UpdateUser(userInfo.PortalID, userInfo);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Warn($"Error while synchronizing user profile picture from user {aadUserId}", e);
            }
        }

        private static string GetExtensionFromMediaContentType(string contentType)
        {
            switch (contentType)
            {
                case "image/png": return "png";
                case "image/gif": return "gif";
                case "image/bmp": return "bmp";
                case "image/jpg": return "jpg";
                default: return contentType.ToLowerInvariant().Replace("image/", "");
            }
        }

        private void ExchangeCodeForToken()
        {
            var parameters = new List<QueryParameter>
            {
                new QueryParameter("grant_type", "authorization_code"),
                new QueryParameter("client_id", APIKey),
                new QueryParameter("scope", Scope),
                new QueryParameter("code", VerificationCode),
                new QueryParameter("redirect_uri", HttpContext.Current.Server.UrlEncode(CallbackUri.ToString()))
            };

            var responseText = ExecuteWebRequest(TokenMethod, new Uri($"{TokenEndpoint.AbsoluteUri}?p={PolicyName}"), parameters.ToNormalizedString(), string.Empty);

            AuthToken = GetToken(responseText);
            AuthTokenExpiry = GetExpiry(responseText);
        }

        private string ExecuteWebRequest(HttpMethod method, Uri uri, string contentParameters, string authHeader)
        {
            WebRequest request;

            if (method == HttpMethod.POST)
            {
                byte[] byteArray = Encoding.UTF8.GetBytes(contentParameters);

                request = WebRequest.CreateDefault(uri);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = byteArray.Length;

                if (!String.IsNullOrEmpty(OAuthHeaderCode))
                {
                    byte[] API64 = Encoding.UTF8.GetBytes(APIKey + ":" + APISecret);
                    string Api64Encoded = System.Convert.ToBase64String(API64);
                    //Authentication providers needing an "Authorization: Basic/bearer base64(clientID:clientSecret)" header. OAuthHeaderCode might be: Basic/Bearer/empty.
                    request.Headers.Add("Authorization: " + OAuthHeaderCode + " " + Api64Encoded);
                }

                if (!String.IsNullOrEmpty(contentParameters))
                {
                    Stream dataStream = request.GetRequestStream();
                    dataStream.Write(byteArray, 0, byteArray.Length);
                    dataStream.Close();
                }
            }
            else
            {
                request = WebRequest.CreateDefault(GenerateRequestUri(uri.ToString(), contentParameters));
            }

            //Add Headers
            if (!String.IsNullOrEmpty(authHeader))
            {
                request.Headers.Add(HttpRequestHeader.Authorization, authHeader);
            }

            try
            {
                using (WebResponse response = request.GetResponse())
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        if (responseStream != null)
                        {
                            using (var responseReader = new StreamReader(responseStream))
                            {
                                return responseReader.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                using (Stream responseStream = ex.Response.GetResponseStream())
                {
                    if (responseStream != null)
                    {
                        using (var responseReader = new StreamReader(responseStream))
                        {
                            Logger.ErrorFormat("WebResponse exception: {0}", responseReader.ReadToEnd());
                        }
                    }
                }
            }
            return null;
        }

        private Uri GenerateRequestUri(string url, string parameters)
        {
            if (string.IsNullOrEmpty(parameters))
            {
                return new Uri(url);
            }

            return new Uri(string.Format("{0}{1}{2}", url, url.Contains("?") ? "&" : "?", parameters));
        }

    }


    internal static class AuthExtensions
    {
        public static string ToAuthorizationString(this IList<QueryParameter> parameters)
        {
            var sb = new StringBuilder();
            sb.Append("OAuth ");

            for (int i = 0; i < parameters.Count; i++)
            {
                string format = "{0}=\"{1}\"";

                QueryParameter p = parameters[i];
                sb.AppendFormat(format, OAuthClientBase.UrlEncode(p.Name), OAuthClientBase.UrlEncode(p.Value));

                if (i < parameters.Count - 1)
                {
                    sb.Append(", ");
                }
            }

            return sb.ToString();
        }

        public static string ToNormalizedString(this IList<QueryParameter> parameters)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < parameters.Count; i++)
            {
                QueryParameter p = parameters[i];
                sb.AppendFormat("{0}={1}", p.Name, p.Value);

                if (i < parameters.Count - 1)
                {
                    sb.Append("&");
                }
            }

            return sb.ToString();
        }
    }
}