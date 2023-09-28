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
using DotNetNuke.Authentication.Azure.B2C.Auth;
using DotNetNuke.Authentication.Azure.B2C.Common;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Users;
using DotNetNuke.Framework;
using DotNetNuke.Instrumentation;
using DotNetNuke.Security.Membership;
using DotNetNuke.Services.Authentication;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Caching;
#endregion

namespace DotNetNuke.Authentication.Azure.B2C.Components
{
    internal class B2CController : ServiceLocator<IB2CController, B2CController>, IB2CController
    {
        #region constants, properties, etc.

        private static readonly ILog Logger = LoggerSource.Instance.GetLogger(typeof(B2CController));
        private static Dictionary<string, B2CControllerConfiguration> _config = null;
        private static readonly Encoding TextEncoder = Encoding.UTF8;
        public const string AuthScheme = "Bearer";
        public string SchemeType => "JWT";

        internal static Dictionary<string, B2CControllerConfiguration> Config
        {
            get
            {
                if (_config == null)
                {
                    _config = new Dictionary<string, B2CControllerConfiguration>();
                }
                return _config;
            }
        }

        internal static B2CControllerConfiguration GetConfigRopc(int portalId, AzureConfig azureB2cConfig)
        {
            const string DefaultRopcPolicy = "B2C_1_ROPC";

            var currentConfig = Config.ContainsKey($"{portalId}-ROPC") ? Config[$"{portalId}-ROPC"] : null;
            if (currentConfig != null && !currentConfig.IsValidRopcPolicy(azureB2cConfig, DefaultRopcPolicy))
            {
                Config.Remove($"{portalId}-ROPC");
                currentConfig = null;
            }

            if (currentConfig == null)
            {
                var ropcPolicyName = !string.IsNullOrEmpty(azureB2cConfig.RopcPolicy) ? azureB2cConfig.RopcPolicy : DefaultRopcPolicy;
                var tenantName = azureB2cConfig.TenantName;
                if (!tenantName.Contains("."))
                {
                    tenantName += ".b2clogin.com";
                }
                var tokenConfigurationUrl = $"https://{tenantName}/{azureB2cConfig.TenantId}/.well-known/openid-configuration?p={ropcPolicyName}";
                var _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(tokenConfigurationUrl, new OpenIdConnectConfigurationRetriever());
                var _config = _configManager.GetConfigurationAsync().Result;
                Config.Add($"{portalId}-ROPC", new B2CControllerConfiguration(ropcPolicyName, _config));
            }

            return Config[$"{portalId}-ROPC"]; 
        }

        internal static B2CControllerConfiguration GetConfigSignIn(int portalId, AzureConfig azureB2cConfig)
        {
            const string DefaultSignInPolicy = "B2C_1_LOGIN";

            var currentConfig = Config.ContainsKey($"{portalId}-LOGIN") ? Config[$"{portalId}-LOGIN"] : null;
            if (currentConfig != null && !currentConfig.IsValidSignInPolicy(azureB2cConfig, DefaultSignInPolicy))
            {
                Config.Remove($"{portalId}-LOGIN");
                currentConfig = null;
            }

            if (currentConfig == null)
            {
                var signInPolicyName = !string.IsNullOrEmpty(azureB2cConfig.SignUpPolicy) ? azureB2cConfig.SignUpPolicy : DefaultSignInPolicy;
                var tenantName = azureB2cConfig.TenantName;
                if (!tenantName.Contains("."))
                {
                    tenantName += ".b2clogin.com";
                }
                var tokenConfigurationUrl = $"https://{tenantName}/{azureB2cConfig.TenantId}/.well-known/openid-configuration?p={signInPolicyName}";
                var _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(tokenConfigurationUrl, new OpenIdConnectConfigurationRetriever());
                var _config = _configManager.GetConfigurationAsync().Result;
                Config.Add($"{portalId}-LOGIN", new B2CControllerConfiguration(signInPolicyName, _config));
            }

            return Config[$"{portalId}-LOGIN"];
        }

        #endregion

        #region constructors / instantiators

        protected override Func<IB2CController> GetFactory()
        {
            return () => new B2CController();
        }

        #endregion

        #region interface implementation

        /// <summary>
        /// Validates the received JWT against the databas eand returns username when successful.
        /// </summary>
        public string ValidateToken(HttpRequestMessage request)
        {
            if (!B2CAuthMessageHandler.IsEnabled)
            {
                Logger.Debug(SchemeType + " is not registered/enabled in web.config file");
                return null;
            }

            var authorization = ValidateAuthHeader(request?.Headers.Authorization);
            return string.IsNullOrEmpty(authorization) ? null : ValidateAuthorizationValue(authorization);
        }

        /// <summary>
        /// Checks for Authorization header and validates it is B2C scheme. If successful, it returns the token string.
        /// </summary>
        /// <param name="authHdr">The request auhorization header.</param>
        /// <returns>The B2C passed in the request; otherwise, it returns null.</returns>
        internal string ValidateAuthHeader(AuthenticationHeaderValue authHdr)
        {
            if (authHdr == null)
            {
                //if (Logger.IsDebugEnabled) Logger.Debug("Authorization header not present in the request"); // too verbose; shows in all web requests
                return null;
            }

            if (!string.Equals(authHdr.Scheme, AuthScheme, StringComparison.CurrentCultureIgnoreCase))
            {
                if (Logger.IsDebugEnabled) Logger.Debug("Authorization header scheme in the request is not equal to " + SchemeType);
                return null;
            }

            var authorization = authHdr.Parameter;
            if (string.IsNullOrEmpty(authorization))
            {
                if (Logger.IsDebugEnabled) Logger.Debug("Missing authorization header value in the request");
                return null;
            }

            return authorization;
        }

        /// <summary>
        /// Checks for Authorization header and validates it is B2C scheme. If successful, it returns the token string.
        /// </summary>
        /// <param name="authHdr">The request auhorization header.</param>
        /// <returns>The B2C passed in the request; otherwise, it returns null.</returns>
        internal string ValidateAuthHeader(string authHdr)
        {
            if (string.IsNullOrEmpty(authHdr) || authHdr.Split(' ').Length != 2)
            {
                //if (Logger.IsDebugEnabled) Logger.Debug("Authorization header not present in the request"); // too verbose; shows in all web requests
                return null;
            }

            
            string scheme = authHdr.Split(' ')[0];
            string parameter = authHdr.Split(' ')[1];

            if (!string.Equals(scheme, AuthScheme, StringComparison.CurrentCultureIgnoreCase))
            {
                if (Logger.IsDebugEnabled) Logger.Debug("Authorization header scheme in the request is not equal to " + SchemeType);
                return null;
            }

            var authorization = parameter;
            if (string.IsNullOrEmpty(authorization))
            {
                if (Logger.IsDebugEnabled) Logger.Debug("Missing authorization header value in the request");
                return null;
            }

            return authorization;
        }

        /// <summary>
        /// Validates the authorization header for the token (for signin or for ropc policies) and returns the username.
        /// </summary>
        internal string ValidateAuthorizationValue(string authorization, bool validateForSignInPolicy = false)
        {
            var cache = DotNetNuke.Services.Cache.CachingProvider.Instance();
            // Calculate a hash of a string
            var hash = authorization.GetHashCode().ToString();
            var cacheKey = "TokenValidation-" + (validateForSignInPolicy ? "S" : "R") + hash;
            if (cache.GetItem(cacheKey) != null)
            {
                return cache.GetItem(cacheKey).ToString();
            }


            if (authorization.Contains("oauth_token="))
            {
                authorization = authorization.Split('&').FirstOrDefault(x => x.Contains("oauth_token=")).Substring("oauth_token=".Length);
            }
            var parts = authorization.Split('.');
            if (parts.Length < 3)
            {
                if (Logger.IsDebugEnabled) Logger.Debug("Token must have [header:claims:signature] parts at least");
                return null;
            }

            var decoded = DecodeBase64(parts[0]);
            if (decoded.IndexOf("\"" + SchemeType + "\"", StringComparison.InvariantCultureIgnoreCase) < 0)            
            {
                if (Logger.IsDebugEnabled) Logger.Debug($"This is not a {SchemeType} authentication scheme.");
                return null;
            }

            var header = JsonConvert.DeserializeObject<JwtHeader>(decoded);
            if (!IsValidSchemeType(header))
                return null;

            var jwt = GetAndValidateJwt(authorization, true, validateForSignInPolicy);
            if (jwt == null)
                return null;

            var userInfo = TryGetUser(jwt, false, validateForSignInPolicy);
            var userName = userInfo?.Username;

            cache.Insert(cacheKey, userName, null, jwt.ValidTo, TimeSpan.Zero);

            return userName;
        }

        internal UserInfo TryGetUser(JwtSecurityToken jwt, bool isImpersonation, bool validateForSignInPolicy)
        {
            try
            {
                var portalSettings = PortalController.Instance.GetCurrentPortalSettings();
                var portalIds = jwt?.Claims.FirstOrDefault(x => x.Type == "extension_portalId")?.Value;
                if (!string.IsNullOrEmpty(portalIds) && int.Parse(portalIds) != portalSettings.PortalId)
                {
                    var portal = PortalController.Instance.GetPortal(int.Parse(portalIds));
                    portalSettings = new PortalSettings(portal);
                }
                var azureB2cConfig = new AzureConfig(AzureConfig.ServiceName, portalSettings.PortalId);
                if (portalSettings == null)
                {
                    if (Logger.IsDebugEnabled) Logger.Debug("Unable to retrieve portal settings");
                    return null;
                }
                if (!azureB2cConfig.Enabled || (!validateForSignInPolicy && !azureB2cConfig.JwtAuthEnabled && !isImpersonation))
                {
                    if (Logger.IsDebugEnabled) Logger.Debug($"Azure B2C JWT auth is not enabled for portal {portalSettings.PortalId}");
                    return null;
                }

                var userIdClaim = Utils.GetUserIdClaim(azureB2cConfig.UseGlobalSettings ? -1 : portalSettings.PortalId);
                var userClaim = jwt.Claims.FirstOrDefault(x => x.Type == userIdClaim);
                if (userClaim == null)
                {
                    if (Logger.IsDebugEnabled) Logger.Debug($"Can't find '{userIdClaim}' claim on token");
                }

                var userInfo = GetOrCreateCachedUserInfo(jwt, portalSettings, userClaim);
                if (userInfo == null)
                {
                    if (Logger.IsDebugEnabled) Logger.Debug("Invalid user");
                    return null;
                }

                var status = UserController.ValidateUser(userInfo, portalSettings.PortalId, false);
                var valid =
                    status == UserValidStatus.VALID ||
                    status == UserValidStatus.UPDATEPROFILE ||
                    status == UserValidStatus.UPDATEPASSWORD;

                if (!valid && Logger.IsDebugEnabled)
                {
                    Logger.Debug("Inactive user status: " + status);
                    return null;
                }

                return userInfo;
            }
            catch (Exception ex)
            {
                Logger.Error("Error while login in: " + ex.Message);
            }
            return null;

        }

        private static UserInfo GetOrCreateCachedUserInfo(JwtSecurityToken jwt, PortalSettings portalSettings, System.Security.Claims.Claim userClaim)
        {
            var usernamePrefixEnabled = bool.Parse(AzureConfig.GetSetting(AzureConfig.ServiceName, "UsernamePrefixEnabled", portalSettings.PortalId, "true"));
            var usernameToFind = usernamePrefixEnabled ? $"{AzureConfig.ServiceName}-{userClaim.Value}" : userClaim.Value;
            var userInfo = UserController.GetUserByName(portalSettings.PortalId, usernameToFind);
            var tokenKey = ComputeSha256Hash(jwt.RawData);
            var cache = DotNetNuke.Services.Cache.CachingProvider.Instance();
            if (string.IsNullOrEmpty((string)cache.GetItem($"SyncB2CToken|{tokenKey}")))
            {
                var azureClient = new AzureClient(portalSettings.PortalId, AuthMode.Login)
                {
                    JwtIdToken = jwt
                };
                azureClient.SetAuthTokenInternal(jwt.RawData);
                azureClient.SetAutoMatchExistingUsers(true);
                var userData = azureClient.GetCurrentUserInternal(jwt);
                if (userInfo == null)
                {
                    // If user doesn't exist, create the user
                    userInfo = userData.ToUserInfo(usernamePrefixEnabled);
                    userInfo.PortalID = portalSettings.PortalId;
                    userInfo.Membership.Password = UserController.GeneratePassword();
                    var result = UserController.CreateUser(ref userInfo);
                }

                azureClient.AuthenticateUser(userData, portalSettings, HttpContext.Current.Request["REMOTE_ADDR"], azureClient.AddCustomProperties, azureClient.OnUserAuthenticated);
                azureClient.UpdateUserProfile(jwt, portalSettings, false, false);
                cache.Insert($"SyncB2CToken|{tokenKey}", "OK", null, jwt.ValidTo, TimeSpan.Zero);
            }

            return userInfo;
        }

        private static string ComputeSha256Hash(string rawData)
        {
            // Create a SHA256   
            using (var sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string   
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }


        private bool IsValidSchemeType(JwtHeader header)
        {
            //if (!SchemeType.Equals(header["typ"] as string, StringComparison.OrdinalIgnoreCase))
            if (!"JWT".Equals(header["typ"] as string, StringComparison.OrdinalIgnoreCase))
            {
                if (Logger.IsDebugEnabled) Logger.Debug("Unsupported authentication scheme type " + header.Typ);
                return false;
            }

            return true;
        }

        private static string DecodeBase64(string b64Str)
        {
            // fix Base64 string padding
            var mod = b64Str.Length % 4;
            if (mod != 0) b64Str += new string('=', 4 - mod);
            return TextEncoder.GetString(Convert.FromBase64String(b64Str));
        }

        private static JwtSecurityToken GetAndValidateJwt(string rawToken, bool checkExpiry, bool validateForSignInPolicy)
        {
            JwtSecurityToken jwt;
            try
            {
                jwt = new JwtSecurityToken(rawToken);
            }
            catch (Exception ex)
            {
                Logger.Error("Unable to construct JWT object from authorization value. " + ex.Message);
                return null;
            }

            if (checkExpiry)
            {
                var now = DateTime.UtcNow;
                if (now < jwt.ValidFrom.AddMinutes(-5) || now > jwt.ValidTo)
                {
                    if (Logger.IsDebugEnabled) Logger.Debug("Token is expired");
                    return null;
                }
            }

            var portalSettings = PortalController.Instance.GetCurrentPortalSettings();
            var azureB2cConfig = new AzureConfig(AzureConfig.ServiceName, portalSettings.PortalId);
            if (portalSettings == null)
            {
                if (Logger.IsDebugEnabled) Logger.Debug("Unable to retrieve portal settings");
                return null;
            }
            
            if (!azureB2cConfig.Enabled || (!validateForSignInPolicy && !azureB2cConfig.JwtAuthEnabled))
            {
                if (Logger.IsDebugEnabled) Logger.Debug($"Azure B2C JWT auth is not enabled for portal {portalSettings.PortalId}");
                return null;
            }
            
            var _config = validateForSignInPolicy 
                ? GetConfigSignIn(portalSettings.PortalId, azureB2cConfig)
                : GetConfigRopc(portalSettings.PortalId, azureB2cConfig);
            var validAudiences = azureB2cConfig.JwtAudiences.Split(',').Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToArray();
            if (validAudiences.Length == 0)
            {
                validAudiences = new[] { azureB2cConfig.APIKey };
            }
            
            try
            {
                // Validate token.
                var _tokenValidator = new JwtSecurityTokenHandler();
                var validationParameters = new TokenValidationParameters
                {
                    // App Id URI and AppId of this service application are both valid audiences.
                    ValidAudiences = validAudiences,
                    // Support Azure AD V1 and V2 endpoints.
                    ValidIssuers = new[] { _config.OpenIdConfig.Issuer, $"{_config.OpenIdConfig.Issuer}v2.0/" },
                    IssuerSigningKeys = _config.OpenIdConfig.SigningKeys
                };

                var claimsPrincipal = _tokenValidator.ValidateToken(rawToken, validationParameters, out SecurityToken _);
            }
            catch (Exception ex)
            {
                if (Logger.IsDebugEnabled) Logger.Debug($"Error validating token: {ex}");
                return null;
            }

            return jwt;
        }

        #endregion
    }
}
