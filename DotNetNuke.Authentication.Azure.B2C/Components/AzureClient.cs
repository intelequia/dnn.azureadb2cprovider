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

using DotNetNuke.Authentication.Azure.B2C.Common;
using DotNetNuke.Authentication.Azure.B2C.Components.Graph;
using DotNetNuke.Authentication.Azure.B2C.Components.Models;
using DotNetNuke.Authentication.Azure.B2C.Data;
using DotNetNuke.Authentication.Azure.B2C.Extensibility;
using DotNetNuke.Common;
using DotNetNuke.Common.Lists;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Profile;
using DotNetNuke.Entities.Users;
using DotNetNuke.Instrumentation;
using DotNetNuke.Security;
using DotNetNuke.Security.Roles;
using DotNetNuke.Services.Authentication;
using DotNetNuke.Services.Authentication.OAuth;
using DotNetNuke.Services.FileSystem;
using DotNetNuke.Services.Log.EventLog;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.Security;
using static DotNetNuke.Services.Authentication.AuthenticationLoginBase;

#endregion

namespace DotNetNuke.Authentication.Azure.B2C.Components
{
    public class AzureClient : OAuthClientBase
    {
        public enum PolicyEnum
        {
            SignUpPolicy,
            PasswordResetPolicy,
            ProfilePolicy,
            ImpersonatePolicy
        }

        public const string RoleSettingsB2cPropertyName = "IdentitySource";
        public const string RoleSettingsB2cPropertyValue = "Azure-B2C";

        private const string TokenEndpointPattern = "https://{0}/{1}/oauth2/v2.0/token";
        private const string LogoutEndpointPattern = "https://{0}/{1}/oauth2/v2.0/logout?p={2}&post_logout_redirect_uri={3}";
        internal const string AuthorizationEndpointPattern = "https://{0}/{1}/oauth2/v2.0/authorize";
        private const string GraphEndpointPattern = "https://graph.windows.net/{0}";

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


        private readonly AzureConfig Settings;

        private List<ProfileMapping> _customClaimsMappings;
        public List<ProfileMapping> CustomClaimsMappings {
            get {
                if (_customClaimsMappings == null)
                {
                    _customClaimsMappings = ProfileMappingsRepository.Instance.GetProfileMappings(GetCalculatedPortalId()).ToList();
                }                
                return _customClaimsMappings;
            }
        }

        private List<RoleMapping> _customRoleMappings;
        public List<RoleMapping> CustomRoleMappings
        {
            get
            {
                if (_customRoleMappings == null)
                {
                    _customRoleMappings = RoleMappingsRepository.Instance.GetRoleMappings(GetCalculatedPortalId()).ToList();
                }
                return _customRoleMappings;
            }
        }

        private List<UserMapping> _customUserMappings;
        public List<UserMapping> CustomUserMappings
        {
            get
            {
                if (_customUserMappings == null)
                {
                    _customUserMappings = UserMappingsRepository.Instance.GetUserMappings(GetCalculatedPortalId()).ToList();
                }
                return _customUserMappings;
            }
        }

        private ILoginValidation _loginValidationAddIn;
        public ILoginValidation LoginValidationAddIn
        {
            get
            {
                if (_loginValidationAddIn == null)
                {
                    var loginValidateTypeName = Utils.GetAppSetting("AzureADB2C.LoginValidationProvider");
                    if (!string.IsNullOrEmpty(loginValidateTypeName))
                    {
                        var type = Type.GetType(loginValidateTypeName, true);
                        if (type.GetInterfaces().Contains(typeof(ILoginValidation)))
                        {
                            _loginValidationAddIn = (ILoginValidation)Activator.CreateInstance(type);
                        }
                        else
                        {
                            throw new Exception($"Provider '{loginValidateTypeName}' does not implement ILoginValidation");
                        }
                    }
                }

                return _loginValidationAddIn;
            }
        }

        public string UnauthorizedReason { get; set; }

        private string _userIdClaim;
        private string UserIdClaim
        {
            get
            {
                if (_userIdClaim == null)
                {
                    
                    var usernameMapping = UserMappingsRepository.Instance.GetUserMapping("Id", GetCalculatedPortalId());
                    _userIdClaim = (usernameMapping != null) ? usernameMapping.B2cClaimName : "sub";
                }
                return _userIdClaim;
            }
        }

        private string _firstNameClaimName;
        private string FirstNameClaimName
        {
            get
            {
                if(_firstNameClaimName == null)
                {
                    var firstNameMapping = UserMappingsRepository.Instance.GetUserMapping("FirstName", GetCalculatedPortalId());
                    _firstNameClaimName = (firstNameMapping != null) ? firstNameMapping.B2cClaimName : JwtRegisteredClaimNames.GivenName;
                }
                return _firstNameClaimName;
            }
        }

        private string _lastNameClaimName;
        private string LastNameClaimName
        {
            get
            {
                if (_lastNameClaimName == null)
                {
                    var lastNameMapping = UserMappingsRepository.Instance.GetUserMapping("LastName", GetCalculatedPortalId());
                    _lastNameClaimName = (lastNameMapping != null) ? lastNameMapping.B2cClaimName : JwtRegisteredClaimNames.FamilyName;
                }
                return _lastNameClaimName;
            }
        }

        private string _displayNameClaimName;
        private string DisplayNameClaimName
        {
            get
            {
                if (_displayNameClaimName == null)
                {
                    var displayNameMapping = UserMappingsRepository.Instance.GetUserMapping("DisplayName", GetCalculatedPortalId());
                    _displayNameClaimName = (displayNameMapping != null) ? displayNameMapping.B2cClaimName : JwtRegisteredClaimNames.Name;
                }
                return _displayNameClaimName;
            }
        }

        private string _emailClaimName;
        private string EmailClaimName
        {
            get
            {
                if (_emailClaimName == null)
                {
                    var emailMapping = UserMappingsRepository.Instance.GetUserMapping("Email", GetCalculatedPortalId());
                    _emailClaimName = (emailMapping != null) ? emailMapping.B2cClaimName : "emails";
                }
                return _emailClaimName;
            }
        }

        private bool _prefixServiceToUserName;
        public override bool PrefixServiceToUserName
        {
            get
            {
                return _prefixServiceToUserName;
            }
        }

        private bool _prefixServiceToGroupName;
        public bool PrefixServiceToGroupName
        {
            get
            {
                return _prefixServiceToGroupName;
            }
        }

        public PolicyEnum Policy { get; set; }

        public string PolicyName
        {
            get
            {
                switch (Policy)
                {
                    case PolicyEnum.PasswordResetPolicy: return Settings.PasswordResetPolicy;
                    case PolicyEnum.ProfilePolicy: return Settings.ProfilePolicy;
                    case PolicyEnum.ImpersonatePolicy: return Settings.ImpersonatePolicy;
                    default: return Settings.SignUpPolicy;
                }
            }
        }

        #region Constructors

        internal JwtSecurityToken JwtIdToken { get; set; }
        public Uri LogoutEndpoint { get; }

        private bool _autoMatchExistingUsers = false;
        private bool _tokenAlreadyExchanged = false;
        public override bool AutoMatchExistingUsers
        { 
            get
            {
                // Will always return true if _autoMatchExistingUsers is true
                // Otherwise, it will return the value specified in the settings
                // This code would have to be changed if we wanted it to always return false whenever _autoMatchExistingUsers is false
                return _autoMatchExistingUsers || Settings.AutoMatchExistingUsers;
            }
        }

        public void SetAutoMatchExistingUsers(bool value)
        {
            _autoMatchExistingUsers = value;
        }

        private int GetCalculatedPortalId()
        {
            return Settings.UseGlobalSettings ? -1 : PortalSettings.Current.PortalId;
        }

        public AzureClient(int portalId, AuthMode mode) 
            : base(portalId, mode, AzureConfig.ServiceName)
        {
            Settings = new AzureConfig(AzureConfig.ServiceName, portalId);

            TokenMethod = HttpMethod.POST;
            
            if (!string.IsNullOrEmpty(Settings.TenantName) && !string.IsNullOrEmpty(Settings.TenantId))
            {
                var tenantName = Settings.TenantName;
                if (!tenantName.Contains("."))
                {
                    tenantName += ".b2clogin.com";
                }
                TokenEndpoint = new Uri(string.Format(Utils.GetAppSetting("AzureADB2C.TokenEndpointPattern", TokenEndpointPattern), tenantName, Settings.TenantId));  
                LogoutEndpoint = new Uri(string.Format(Utils.GetAppSetting("AzureADB2C.LogoutEndpointPattern", LogoutEndpointPattern), tenantName, Settings.TenantId, Settings.SignUpPolicy, UrlEncode(HttpContext.Current.Request.Url.ToString())));
                AuthorizationEndpoint = new Uri(string.Format(Utils.GetAppSetting("AzureADB2C.AuthorizationEndpointPattern", AuthorizationEndpointPattern), tenantName, Settings.TenantId));
                MeGraphEndpoint = new Uri(string.Format(Utils.GetAppSetting("AzureADB2C.GraphEndpointPattern", GraphEndpointPattern), Settings.TenantId));
            }

            if (string.IsNullOrEmpty(Settings.APIResource) && string.IsNullOrEmpty(Settings.Scopes)) {
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

            if (!string.IsNullOrEmpty(Settings.RedirectUri))
            {
                CallbackUri = new Uri(Settings.RedirectUri);
            }

            APIKey = Settings.APIKey;
            APISecret = Settings.APISecret;
            AuthTokenName = "AzureB2CUserToken";
            OAuthVersion = "2.0";
            OAuthHeaderCode = "Basic";
            LoadTokenCookie(string.Empty);
            JwtIdToken = null;
            Policy = PolicyEnum.SignUpPolicy;

            _prefixServiceToUserName = Settings.UsernamePrefixEnabled;
            _prefixServiceToGroupName = Settings.GroupNamePrefixEnabled;
        }

        #endregion

        public new bool IsCurrentService()
        {
            var state = HttpContext.Current.Request.Params["state"];
            var oState = new State(state);
            return oState.Service == Service;
        }

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

        internal void SetAuthTokenInternal(string token)
        {
            AuthToken = token;
        }

        internal AzureUserData GetCurrentUserInternal(JwtSecurityToken pToken = null)
        {
            if (pToken == null && (!IsCurrentUserAuthorized() || JwtIdToken == null))
            {
                return null;
            }
            var claims = JwtIdToken.Claims.ToArray();
            EnsureClaimExists(claims, EmailClaimName);
            EnsureClaimExists(claims, UserIdClaim);
            EnsureClaimExists(claims, "sub");       // we need this claim to make calls to AAD Graph

            var user = new AzureUserData()
            {
                AzureFirstName = claims.FirstOrDefault(x => x.Type == FirstNameClaimName)?.Value,
                AzureLastName = claims.FirstOrDefault(x => x.Type == LastNameClaimName)?.Value,
                AzureDisplayName = claims.FirstOrDefault(x => x.Type == DisplayNameClaimName)?.Value,
                Email = claims.FirstOrDefault(x => x.Type == EmailClaimName)?.Value,
                Id = claims.FirstOrDefault(x => x.Type == UserIdClaim).Value
            };

            // Store checks in variables to increase readability and avoid executing the same logic more than once.
            bool noFirstName = string.IsNullOrEmpty(user.AzureFirstName);
            bool noLastName = string.IsNullOrEmpty(user.AzureLastName);

            // If no display name, try to get it from the first and last name.
            if (string.IsNullOrEmpty(user.AzureDisplayName))
            {
                user.AzureDisplayName = !noFirstName ? user.AzureFirstName + (!noLastName ? " " + user.AzureLastName : "") : "";
                return user; // We don't need to run the rest of the code if there's no display name.
            }

            // If no first name, try and get it from the display name.
            if (noFirstName)
            {
                user.AzureFirstName = Utils.GetFirstName(user.AzureDisplayName);
            }

            // If no last name, try and get it from the display name.
            if (noLastName)
            {
                user.AzureLastName = Utils.GetLastName(user.AzureDisplayName);
            }

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
                switch (claim.Type) {
                    case "emails":
                        if (properties["Email"] == null)
                            properties.Set("Email", claim.Value);
                        break;
                    case "city":
                        properties.Set("City", claim.Value);
                        break;
                    case "country":
                        properties.Set("Country", claim.Value);
                        break;
                    case "name":
                        properties.Set("DisplayName", claim.Value);
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
                        var mapping = CustomClaimsMappings.FirstOrDefault(c => c.GetB2cCustomClaimName().ToLower() == claim.Type.ToLower());
                        if (mapping != null)
                        {
                            properties.Add(mapping.DnnProfilePropertyName, claim.Value);
                        }
                        break;
                }
            }
        }


        private void SetCurrentPrincipal(IPrincipal principal, HttpContext httpContext)
        {
            Thread.CurrentPrincipal = principal;
            httpContext.User = principal;            
        }

        public string Impersonate(JwtSecurityToken pToken = null)
        {
            if (pToken == null && (!IsCurrentUserAuthorized() || JwtIdToken == null))
            {
                return string.Empty;
            }
            if (pToken != null)
            {
                JwtIdToken = pToken;
            }

            //Remove user from cache
            var userData = GetCurrentUserInternal(pToken);            
            
            if (userData != null)
            {
                var impersonatorUsername = HttpContext.Current.User.Identity.Name;
                DataCache.ClearUserCache(Settings.PortalID, HttpContext.Current.User.Identity.Name);
                var objPortalSecurity = PortalSecurity.Instance;
                objPortalSecurity.SignOut();

                var _b2cController = (B2CController)B2CController.Instance;
                var user = _b2cController.TryGetUser(JwtIdToken, true);
                var username = user?.Username;
                if (!string.IsNullOrEmpty(username))
                {
                    DataCache.ClearUserCache(Settings.PortalID, user.Username);
                    objPortalSecurity.SignIn(user, true);
                    SaveTokenCookie(string.IsNullOrEmpty(AuthToken));

                    if (Logger.IsInfoEnabled) Logger.Info($"User {impersonatorUsername} has impersonated as {username}");
                    SetCurrentPrincipal(new GenericPrincipal(new GenericIdentity(user.Username, _b2cController.SchemeType), null), HttpContext.Current);

                    EventLogController.Instance.AddLog("User Impersonation", $"User {impersonatorUsername} has impersonated as {username}", 
                        PortalSettings.Current, user.UserID, EventLogController.EventLogType.USER_IMPERSONATED);

                    var portal = PortalController.Instance.GetPortal(user.PortalID);
                    //var portalSettings = new PortalSettings(portal.PortalID);
                    var httpAlias = PortalAliasController.Instance.GetPortalAliasesByPortalId(portal.PortalID).ToList().FirstOrDefault(x => x.IsPrimary).HTTPAlias;
                    return $"{HttpContext.Current.Request.Url.Scheme}://{httpAlias}";
                }
            }
            return string.Empty;
        }


        public void UpdateUserProfile(JwtSecurityToken pToken = null, PortalSettings portalSettings = null, bool updateProfilePicture = true, bool updateUserRoles = true)
        {
            if (pToken == null && (!IsCurrentUserAuthorized() || JwtIdToken == null))
            {
                return;
            }
            if (pToken != null) {
                JwtIdToken = pToken;
            }
            if (portalSettings == null)
            {
                portalSettings = PortalSettings.Current;
            }
            var user = GetCurrentUserInternal(pToken).ToUserInfo(Settings.UsernamePrefixEnabled);
            // Update user
            var userInfo = UserController.GetUserByName(portalSettings.PortalId, user.Username);

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
                if (updateProfilePicture)
                {
                    UpdateUserProfilePicture(JwtIdToken.Claims.First(c => c.Type == "sub").Value, userInfo);
                }
            }
            UserController.UpdateUser(portalSettings.PortalId, userInfo);

            // Update user roles
            if (updateUserRoles)
            {
                UpdateUserRoles(JwtIdToken.Claims.First(c => c.Type == "sub").Value, userInfo);
            }
            

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
                    new QueryParameter("redirect_uri", string.IsNullOrEmpty(Settings.RedirectUri) 
                        ? HttpContext.Current.Server.UrlEncode($"{CallbackUri.Scheme}://{CallbackUri.Host}/UserProfile")
                        : HttpContext.Current.Server.UrlEncode(CallbackUri.ToString())),
                    new QueryParameter("state", HttpContext.Current.Server.UrlEncode(new State() { 
                        PortalId = PortalSettings.Current.PortalId, 
                        Culture = PortalSettings.Current.CultureCode,
                        RedirectUrl = redirectAfterEditUri?.ToString(),
                        IsUserProfile = true
                    }.ToString())),
                    new QueryParameter("response_type", "code"),
                    new QueryParameter("response_mode", "query"),
                    new QueryParameter("p", Settings.ProfilePolicy)
                };

            HttpContext.Current.Response.Redirect(AuthorizationEndpoint + "?" + parameters.ToNormalizedString(), false);
        }

        public Uri NavigateImpersonation(Uri redirectAfterImpersonateUri = null, string loginHint = "")
        {
            redirectAfterImpersonateUri = new Uri($"{CallbackUri.Scheme}://{PortalSettings.Current.PortalAlias.HTTPAlias}/Impersonate");
            var parameters = new List<QueryParameter>
                {
                    new QueryParameter("scope", Scope),
                    new QueryParameter("client_id", APIKey),
                    //new QueryParameter("redirect_uri", HttpContext.Current.Server.UrlEncode($"{CallbackUri.Scheme}://{CallbackUri.Host}/Impersonate")),
                    new QueryParameter("redirect_uri", string.IsNullOrEmpty(Settings.RedirectUri)
                        ? HttpContext.Current.Server.UrlEncode($"{CallbackUri.Scheme}://{CallbackUri.Host}/Impersonate")
                        : HttpContext.Current.Server.UrlEncode(CallbackUri.ToString())),
                    new QueryParameter("state", HttpContext.Current.Server.UrlEncode(new State() {
                        PortalId = PortalSettings.Current.PortalId,
                        Culture = PortalSettings.Current.CultureCode,
                        RedirectUrl = redirectAfterImpersonateUri?.ToString(),
                        IsImpersonate = true
                    }.ToString())),
                    new QueryParameter("response_type", "code"),
                    new QueryParameter("response_mode", "query"),
                    new QueryParameter("p", Settings.ImpersonatePolicy)
                };
            if (!string.IsNullOrEmpty(loginHint))
            {
                parameters.Add(new QueryParameter("login_hint", loginHint));
            }

            return new Uri(AuthorizationEndpoint + "?" + parameters.ToNormalizedString());
        }

        public override void AuthenticateUser(UserData user, PortalSettings settings, string IPAddress, Action<NameValueCollection> addCustomProperties, Action<UserAuthenticatedEventArgs> onAuthenticated)
        {
            var portalSettings = settings;
            if (IsCurrentUserAuthorized() && JwtIdToken != null)
            {
                // Check if portalId profile mapping exists
                var portalUserMapping = UserMappingsRepository.Instance.GetUserMapping("PortalId", GetCalculatedPortalId());
                if (!string.IsNullOrEmpty(portalUserMapping?.B2cClaimName))
                {
                    var claimName = portalUserMapping?.GetB2cCustomClaimName();
                    // Get PortalId from claim
                    var portalIdClaim = JwtIdToken.Claims.FirstOrDefault(x => x.Type.ToLowerInvariant() == claimName.ToLowerInvariant())?.Value;
                    if (string.IsNullOrEmpty(portalIdClaim))
                    {
                        throw new SecurityTokenException("The user has no portalId claim and portalId profile mapping is setup. The B2C user can't login to any portal until the portalId attribute has been setup for the user. Ensure that the PortalId claim has been setup and included on the policy being used.");
                    }
                    if (int.TryParse(portalIdClaim, out int portalId) && portalId != portalSettings.PortalId)
                    {
                        // Redirect to the user portal
                        var request = HttpContext.Current.Request;
                        if (!string.IsNullOrEmpty(request.Headers["Authorization"]) && request.Headers["Authorization"].StartsWith("Bearer"))
                        {
                            throw new SecurityTokenException($"The user portalId claim ({portalId}) is different from current portalId ({portalSettings.PortalId}). Portal redirection flow is not supported on native apps. Please call the API from the corresponding portal URL");
                        }
                        var state = new State(request["state"]);
                        HttpContext.Current.Response.Redirect(Utils.GetLoginUrl(portalId, state.Culture, request));
                        return;
                    }
                }
            }
            
            var userIdClaim = Utils.GetUserIdClaim(GetCalculatedPortalId());
            var userClaim = JwtIdToken.Claims.FirstOrDefault(x => x.Type == userIdClaim);
            if (userClaim == null)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Error($"Can't find '{userIdClaim}' claim on token");
                }
                throw new MissingFieldException($"Can't find '{userIdClaim}' claim on token, needed to identify the user");
            }

            var usernamePrefixEnabled = bool.Parse(AzureConfig.GetSetting(AzureConfig.ServiceName, "UsernamePrefixEnabled", portalSettings.PortalId, "true"));
            var usernameToFind = usernamePrefixEnabled ? $"{AzureConfig.ServiceName}-{userClaim.Value}" : userClaim.Value;
            var userInfo = UserController.GetUserByName(portalSettings.PortalId, usernameToFind);
            // If user doesn't exist on current portal, AuthenticateUser() will create it. 
            // Otherwise, AuthenticateUser will perform a Response.Redirect, so we have to sinchronize the roles before that, to avoid the ThreadAbortException caused by the Response.Redirect
            if (userInfo == null)
            {
                base.AuthenticateUser(user, portalSettings, IPAddress, addCustomProperties, onAuthenticated);
                if (IsCurrentUserAuthorized())
                {
                    userInfo = UserController.GetUserByName(portalSettings.PortalId, usernameToFind);
                    if (userInfo == null)
                    {
                        throw new SecurityTokenException($"The logged in user {usernameToFind} does not belong to PortalId {portalSettings.PortalId}");
                    }
                    UpdateUserAndRoles(userInfo);
                    MarkUserAsB2c(userInfo);
                }
            }
            else
            {
                if (IsCurrentUserAuthorized())
                {
                    UpdateUserAndRoles(userInfo);
                    MarkUserAsB2c(userInfo);
                }
                base.AuthenticateUser(user, portalSettings, IPAddress, addCustomProperties, onAuthenticated);
            }
        }

        private void MarkUserAsB2c(UserInfo user)
        {
            EnsureIdentitySourceProfilePropertyDefinitionExists(-1); //  Ensure profile property exists for superusers
            EnsureIdentitySourceProfilePropertyDefinitionExists(user.PortalID);

            user.Profile.SetProfileProperty("IdentitySource", "Azure-B2C");
            Security.Profile.ProfileProvider.Instance().UpdateUserProfile(user);
        }

        private static void EnsureIdentitySourceProfilePropertyDefinitionExists(int portalId)
        {
            var def = ProfileController.GetPropertyDefinitionByName(portalId, "IdentitySource");
            if (def == null)
            {
                var dataTypes = (new ListController()).GetListEntryInfoDictionary("DataType");
                var definition = new ProfilePropertyDefinition(portalId)
                {
                    DataType = dataTypes["DataType:Text"].EntryID,
                    DefaultValue = "Azure-B2C",
                    DefaultVisibility = UserVisibilityMode.AdminOnly,
                    PortalId = portalId,
                    ModuleDefId = Null.NullInteger,
                    PropertyCategory = "Security",
                    PropertyName = "IdentitySource",
                    Required = false,
                    Visible = false,
                    ViewOrder = -1
                };
                ProfileController.AddPropertyDefinition(definition);
            }
        }

        private void UpdateUserAndRoles(UserInfo userInfo)
        {
            // Reset user password with a new one to avoid password expiration errors on DNN for Azure AD users
            MembershipUser aspnetUser = Membership.GetUser(userInfo.Username);
            aspnetUser.ResetPassword();

            // Last login date not being updated by DNN on OAuth login, so we have to do it manually
            aspnetUser = Membership.GetUser(userInfo.Username);
            aspnetUser.LastLoginDate = DateTime.Now;
            Membership.UpdateUser(aspnetUser);

            // Updates the user in DNN
            userInfo.Membership.LastLoginDate = aspnetUser.LastLoginDate;
            userInfo.Membership.UpdatePassword = false;
            if (Settings.AutoAuthorize && !userInfo.Membership.Approved && IsCurrentUserAuthorized())
            {
                userInfo.Membership.Approved = true; // Delegate approval on Auth Provider
            }
            UserController.UpdateUser(userInfo.PortalID, userInfo);

            UpdateUserRoles(JwtIdToken.Claims.First(c => c.Type == "sub").Value, userInfo);
            UpdateUserProfilePicture(JwtIdToken.Claims.First(c => c.Type == "sub").Value, userInfo, true);
        }

        public override AuthorisationResult Authorize()
        {
            string errorReason = HttpContext.Current.Request.Params["error_reason"];
            bool userDenied = (errorReason != null);
            if (userDenied)
            {
                return AuthorisationResult.Denied;
            }
            var state = new State(HttpContext.Current.Request["state"]);
            if (state.IsResetPassword || (!string.IsNullOrEmpty(HttpContext.Current.Request.UrlReferrer?.Query)
                && HttpContext.Current.Request.UrlReferrer.Query.IndexOf("p=" + Settings.PasswordResetPolicy + "&") > -1))
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
                    new QueryParameter("state", HttpContext.Current.Server.UrlEncode(new State() {
                        PortalId = Settings.PortalID,
                        Culture = PortalSettings.Current.CultureCode,
                        IsResetPassword = Policy == PolicyEnum.PasswordResetPolicy
                    }.ToString())),
                    new QueryParameter("response_type", "code"),
                    new QueryParameter("response_mode", "query"),
                    new QueryParameter("p", PolicyName)
                };

                string authorizationUrl = AuthorizationEndpoint + "?" + parameters.ToNormalizedString();
                Logger.Debug($"Authorizing. Redirecting to {authorizationUrl}");
                HttpContext.Current.Response.Redirect(authorizationUrl, false);
                return AuthorisationResult.RequestingCode;
            }

            ExchangeCodeForToken();

            var authResult = string.IsNullOrEmpty(AuthToken) ? AuthorisationResult.Denied : AuthorisationResult.Authorized;

            if (authResult == AuthorisationResult.Authorized)
            {
                if (LoginValidationAddIn != null)
                {
                    Logger.Debug("Calling external ILoginValidate.OnTokenReceived method");
                    try
                    {
                        UnauthorizedReason = null;
                        LoginValidationAddIn.OnTokenReceived(AuthToken, HttpContext.Current);
                    }
                    catch (SecurityTokenException e)
                    {
                        Logger.Error("ILoginValidate.OnTokenReceived unauthorized this login", e);
                        UnauthorizedReason = e.Message;
                        authResult = AuthorisationResult.Denied;
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"Error while calling ILoginValidate.OnTokenReceived: {e.Message}", e);
                        authResult = AuthorisationResult.Denied;
                    }
                }
            }

            SaveTokenCookie(authResult != AuthorisationResult.Authorized);
            return authResult;
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
            HttpContext.Current.Response.Cookies.Add(authTokenCookie);
        }

        private List<string> GetDnnB2cRoles()
        {
            // This is the list of DNN roles coming from B2C
            var result = RoleController.Instance.GetRoles(PortalSettings.Current.PortalId)
                .Where(r => r.Settings.ContainsKey(RoleSettingsB2cPropertyName) && r.Settings[RoleSettingsB2cPropertyName] == RoleSettingsB2cPropertyValue)
                .Select((roleInfo) => roleInfo.RoleName)
                .ToList();

            // We have to add also to this list the list of mapped roles, because those roles are also synchronized with B2C
            result.AddRange(CustomRoleMappings.Select((role) => role.DnnRoleName).ToList());

            return result;
        }

        private int AddRole(string roleName, string roleDescription, bool isFromB2c)
        {
            var roleId = RoleController.Instance.AddRole(new RoleInfo
            {
                RoleName = roleName,
                Description = roleDescription,
                PortalID = PortalSettings.Current.PortalId,
                Status = RoleStatus.Approved,
                RoleGroupID = -1,
                AutoAssignment = false,
                IsPublic = false
            });

            if (isFromB2c)
            {
                var role = RoleController.Instance.GetRoleById(PortalSettings.Current.PortalId, roleId);
                role.Settings.Add(RoleSettingsB2cPropertyName, RoleSettingsB2cPropertyValue);
                RoleController.Instance.UpdateRoleSettings(role, true);
            }

            return roleId;
        }

        private void UpdateUserRoles(string aadUserId, UserInfo userInfo)
        {
            if (!Settings.RoleSyncEnabled)
            {
                return;
            }

            try
            {
                var syncOnlyMappedRoles = (CustomRoleMappings != null && CustomRoleMappings.Count > 0);
                var groupPrefix = PrefixServiceToGroupName ? $"{Service}-" : "";

                var aadGroups = GraphClient.GetUserGroups(aadUserId);
                var groups = new List<Microsoft.Graph.Group>();
                while (aadGroups != null && aadGroups.Count > 0)
                {
                    groups.AddRange(aadGroups.CurrentPage.OfType<Microsoft.Graph.Group>().ToList());
                    aadGroups = aadGroups.NextPageRequest?.GetSync();
                }

                var filter = ConfigurationManager.AppSettings["AzureADB2C.GetUserGroups.Filter"];
                if (!string.IsNullOrEmpty(filter))
                {
                    var onlyGroups = filter.Split(';');
                    var g = new List<Microsoft.Graph.Group>();
                    foreach (var f in onlyGroups)
                    {
                        var r = groups.Where(x => x.DisplayName.StartsWith(f));
                        if (r.Count() > 0)
                            g.AddRange(r);
                    }
                    groups = g;
                }

                if (syncOnlyMappedRoles)
                {
                    groupPrefix = "";
                    var b2cRoles = CustomRoleMappings.Select(rm => rm.B2cRoleName);
                    groups.RemoveAll(x => !b2cRoles.Contains(x.DisplayName));
                }

                var dnnB2cRoles = GetDnnB2cRoles();
                // In DNN, remove user from roles where the user doesn't belong to in AAD (we'll take care only the roles that we are synchronizing with B2C)
                foreach (var dnnUserRole in userInfo.Roles.Where(role => dnnB2cRoles.Contains(role)))
                {
                    var aadGroupName = dnnUserRole;
                    var roleName = dnnUserRole;
                    var mapping = CustomRoleMappings?.FirstOrDefault(x => x.DnnRoleName == dnnUserRole);
                    if (mapping != null)
                    {
                        aadGroupName = mapping.B2cRoleName;
                        roleName = mapping.DnnRoleName;
                    }
                    if (groups.FirstOrDefault(aadGroup => $"{groupPrefix}{aadGroup.DisplayName}" == aadGroupName) == null)
                    {
                        var role = RoleController.Instance.GetRoleByName(PortalSettings.Current.PortalId, roleName);
                        RoleController.DeleteUserRole(userInfo, role, PortalSettings.Current, false);
                    }
                }

                foreach (var group in groups)
                {
                    var roleToAssign = syncOnlyMappedRoles ? CustomRoleMappings.Find(r => r.B2cRoleName == group.DisplayName).DnnRoleName : $"{groupPrefix}{group.DisplayName}";
                    var dnnRole = RoleController.Instance.GetRoleByName(PortalSettings.Current.PortalId, roleToAssign);

                    if (dnnRole == null)
                    {
                        // Create role
                        var roleId = AddRole($"{groupPrefix}{group.DisplayName}", group.Description, true);
                        dnnRole = RoleController.Instance.GetRoleById(PortalSettings.Current.PortalId, roleId);
                        // Add user to Role
                        RoleController.Instance.AddUserRole(PortalSettings.Current.PortalId,
                                                            userInfo.UserID,
                                                            roleId,
                                                            RoleStatus.Approved,
                                                            false,
                                                            group.CreatedDateTime.HasValue ? group.CreatedDateTime.Value.DateTime : DotNetNuke.Common.Utilities.Null.NullDate,
                                                            DotNetNuke.Common.Utilities.Null.NullDate);
                    }
                    else
                    {
                        // If user doesn't belong to that DNN role, let's add it
                        if (!userInfo.Roles.Contains(roleToAssign))
                        {
                            RoleController.Instance.AddUserRole(PortalSettings.Current.PortalId,
                                                                userInfo.UserID,
                                                                dnnRole.RoleID,
                                                                Security.Roles.RoleStatus.Approved,
                                                                false,
                                                                group.CreatedDateTime.HasValue ? group.CreatedDateTime.Value.DateTime : DateTime.Today,
                                                                DotNetNuke.Common.Utilities.Null.NullDate);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Warn($"Error while synchronizing the user roles from user {aadUserId}", e);
            }
        }

        private void UpdateUserProfilePicture(string aadUserId, UserInfo userInfo, bool saveUserInfo = false)
        {
            if (!Settings.ProfileSyncEnabled)
            {
                return;
            }
            try
            {
                if (!string.IsNullOrEmpty(aadUserId) && userInfo != null)
                {
                    var profilePictureMetadata = GraphClient.GetUserProfilePictureMetadata(aadUserId);
                    if (profilePictureMetadata != null && profilePictureMetadata.AdditionalData.ContainsKey("@odata.mediaContentType"))
                    {
                        var pictureBytes = GraphClient.GetUserProfilePicture(aadUserId);
                        var userFolder = FolderManager.Instance.GetUserFolder(userInfo);
                        var stream = new MemoryStream(pictureBytes);
                        var profilePictureInfo = FileManager.Instance.AddFile(userFolder,
                            $"{aadUserId}.{GetExtensionFromMediaContentType(profilePictureMetadata.AdditionalData["@odata.mediaContentType"].ToString())}",
                            stream, true);

                        userInfo.Profile.Photo = profilePictureInfo.FileId.ToString();
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
        internal static string GetExtensionFromMediaContentType(string contentType)
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
            if (_tokenAlreadyExchanged)
                return;            
            var parameters = new List<QueryParameter>
            {
                new QueryParameter("grant_type", "authorization_code"),
                new QueryParameter("client_id", APIKey),
                new QueryParameter("scope", Scope),
                new QueryParameter("code", VerificationCode),
                new QueryParameter("redirect_uri", HttpContext.Current.Server.UrlEncode(CallbackUri.ToString()))
            };
            Logger.Debug($"Exchanging code for token");
            var responseText = ExecuteWebRequest(TokenMethod, new Uri($"{TokenEndpoint.AbsoluteUri}?p={PolicyName}"), parameters.ToNormalizedString(), string.Empty);
            Logger.Debug($"Exchange token response: {responseText}");
            AuthToken = GetToken(responseText);
            AuthTokenExpiry = GetExpiry(responseText);
            _tokenAlreadyExchanged = true;
        }

        private string ExecuteWebRequest(HttpMethod method, Uri uri, string contentParameters, string authHeader)
        {
            WebRequest request;

            if (method == HttpMethod.POST)
            {
                Logger.Debug($"Executing webrequest: POST, {uri}, body={contentParameters}");
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
                Uri requestUri = GenerateRequestUri(uri.ToString(), contentParameters);
                Logger.Debug($"Executing webrequest: GET, {requestUri}");
                request = WebRequest.CreateDefault(requestUri);
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

        public event UserAuthenticatedEventHandler UserAuthenticated;
        public void OnUserAuthenticated(UserAuthenticatedEventArgs ea)
        {
            if (UserAuthenticated != null)
            {
                UserAuthenticated(null, ea);
            }
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
