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

using System.Runtime.Serialization;
using DotNetNuke.Authentication.Azure.B2C.Components;

namespace DotNetNuke.Authentication.Azure.B2C.Services
{
    [DataContract]
    public class AzureADB2CProviderSettings
    {
        [DataMember(Name = "tenantName")]
        public string TenantName { get; set; }
        [DataMember(Name = "tenantId")]
        public string TenantId { get; set; }
        [DataMember(Name = "apiKey")]
        public string ApiKey { get; set; }
        [DataMember(Name = "apiSecret")]
        public string ApiSecret { get; set; }
        [DataMember(Name = "redirectUri")]
        public string RedirectUri { get; set; }
        [DataMember(Name = "autoRedirect")]
        public bool AutoRedirect { get; set; }
        [DataMember(Name = "enabled")]
        public bool Enabled { get; set; }
        [DataMember(Name = "useGlobalSettings")]
        public bool UseGlobalSettings { get; set; }
        [DataMember(Name = "signUpPolicy")]
        public string SignUpPolicy { get; set; }
        [DataMember(Name = "profilePolicy")]
        public string ProfilePolicy { get; set; }
        [DataMember(Name = "passwordResetPolicy")]
        public string PasswordResetPolicy { get; set; }
        [DataMember(Name = "aadAppClientId")]
        public string AadAppClientId { get; set; }
        [DataMember(Name = "aadAppSecret")]
        public string AadAppSecret { get; set; }
        [DataMember(Name = "jwtAudiences")]
        public string JwtAudiences { get; set; }
        [DataMember(Name = "roleSyncEnabled")]
        public bool RoleSyncEnabled { get; set; }
        [DataMember(Name = "profileSyncEnabled")]
        public bool ProfileSyncEnabled { get; set; }
        [DataMember(Name = "jwtAuthEnabled")]
        public bool JwtAuthEnabled { get; set; }
        [DataMember(Name = "apiResource")]
        public string ApiResource { get; set; }
        [DataMember(Name = "scopes")]
        public string Scopes { get; set; }
        [DataMember(Name = "usernamePrefixEnabled")]
        public bool UsernamePrefixEnabled { get; set; }
        [DataMember(Name = "groupNamePrefixEnabled")]
        public bool GroupNamePrefixEnabled { get; set; }
        [DataMember(Name = "ropcPolicy")]
        public string RopcPolicy { get; set; }
        [DataMember(Name = "impersonatePolicy")]
        public string ImpersonatePolicy { get; set; }


        public static AzureADB2CProviderSettings LoadSettings(string service, int portalId)
        {
            var config = new AzureConfig(service, portalId);
            return new AzureADB2CProviderSettings
            {
                TenantName = config.TenantName,
                TenantId = config.TenantId,
                ApiKey = config.APIKey,
                ApiSecret = config.APISecret,
                RedirectUri = config.RedirectUri,
                AutoRedirect = config.AutoRedirect,
                SignUpPolicy = config.SignUpPolicy,
                ProfilePolicy = config.ProfilePolicy,
                PasswordResetPolicy = config.PasswordResetPolicy,
                AadAppClientId = config.AADApplicationId,
                AadAppSecret = config.AADApplicationKey,
                Enabled = config.Enabled,
                UseGlobalSettings = config.UseGlobalSettings,
                JwtAudiences = config.JwtAudiences,
                RoleSyncEnabled = config.RoleSyncEnabled,
                ProfileSyncEnabled = config.ProfileSyncEnabled,
                JwtAuthEnabled = config.JwtAuthEnabled,
                ApiResource = config.APIResource,
                Scopes = config.Scopes,
                UsernamePrefixEnabled = config.UsernamePrefixEnabled,
                GroupNamePrefixEnabled = config.GroupNamePrefixEnabled,
                RopcPolicy = config.RopcPolicy,
                ImpersonatePolicy = config.ImpersonatePolicy
            };
        }

        public static void SaveGeneralSettings(string service, int portalId, AzureADB2CProviderSettings settings)
        {
            var config = new AzureConfig(service, portalId)
            {
                TenantName = settings.TenantName.ToLowerInvariant().Trim().Replace(".onmicrosoft.com", ""),
                TenantId = settings.TenantId,
                APIKey = settings.ApiKey,
                APISecret = settings.ApiSecret,
                RedirectUri = settings.RedirectUri,
                AutoRedirect = settings.AutoRedirect,
                SignUpPolicy = settings.SignUpPolicy,
                ProfilePolicy = settings.ProfilePolicy,
                PasswordResetPolicy = settings.PasswordResetPolicy,
                Enabled = settings.Enabled,
                UseGlobalSettings = settings.UseGlobalSettings,
                RopcPolicy = settings.RopcPolicy,
                ImpersonatePolicy = settings.ImpersonatePolicy
            };

            AzureConfig.UpdateConfig(config);
        }

        public static void SaveAdvancedSettings(string service, int portalId, AzureADB2CProviderSettings settings)
        {
            var config = new AzureConfig(service, portalId)
            {
                AADApplicationId = settings.AadAppClientId,
                AADApplicationKey = settings.AadAppSecret,
                JwtAudiences = settings.JwtAudiences,
                RoleSyncEnabled = settings.RoleSyncEnabled,
                ProfileSyncEnabled = settings.ProfileSyncEnabled,
                JwtAuthEnabled = settings.JwtAuthEnabled,
                APIResource = settings.ApiResource + (!string.IsNullOrEmpty(settings.ApiResource.Trim()) && !settings.ApiResource.EndsWith("/") ? "/" : ""),
                Scopes = settings.Scopes,
                UsernamePrefixEnabled = settings.UsernamePrefixEnabled,
                GroupNamePrefixEnabled = settings.GroupNamePrefixEnabled
            };

            AzureConfig.UpdateConfig(config);
        }
    }
}
