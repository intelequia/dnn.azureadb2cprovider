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

using DotNetNuke.Authentication.Azure.B2C.Components.Graph;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Controllers;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Services.Authentication.OAuth;
using DotNetNuke.Services.Exceptions;
using DotNetNuke.UI.WebControls;
using System;
using System.Configuration;
using System.Linq;

namespace DotNetNuke.Authentication.Azure.B2C.Components
{
    public class AzureConfig : OAuthConfigBase
    {
        public const string ServiceName = "AzureB2C";

        private const string _cacheKey = "Authentication";

        private AzureConfig() : base("", 0)
        { }
        protected internal AzureConfig(string service, int portalId) : base(service, portalId)
        {
            // Gets the settings scope (global or per portal)
            UseGlobalSettings = bool.Parse(HostController.Instance.GetString(Service + "_UseGlobalSettings", "false"));

            // Loads the scoped settings
            APIKey = GetScopedSetting(Service + "_ApiKey", portalId, "");
            APISecret = GetScopedSetting(Service + "_ApiSecret", portalId, "");
            RedirectUri = GetScopedSetting(Service + "_RedirectUri", portalId, "");
            OnErrorUri = GetScopedSetting(Service + "_OnErrorUri", portalId, "");
            TenantName = GetScopedSetting(Service + "_TenantName", portalId, "");
            TenantId = GetScopedSetting(Service + "_TenantId", portalId, "");
            SignUpPolicy = GetScopedSetting(Service + "_SignUpPolicy", portalId, "");
            ProfilePolicy = GetScopedSetting(Service + "_ProfilePolicy", portalId, "");
            PasswordResetPolicy = GetScopedSetting(Service + "_PasswordResetPolicy", portalId, "");
            AutoRedirect = bool.Parse(GetScopedSetting(Service + "_AutoRedirect", portalId, "false"));
            Enabled = bool.Parse(GetScopedSetting(Service + "_Enabled", portalId, "false"));
            AADApplicationId = GetScopedSetting(Service + "_AADApplicationId", portalId, "");
            AADApplicationKey = GetScopedSetting(Service + "_AADApplicationKey", portalId, "");
            JwtAudiences = GetScopedSetting(Service + "_JwtAudiences", portalId, "");
            RoleSyncEnabled = bool.Parse(GetScopedSetting(Service + "_RoleSyncEnabled", portalId, "false"));
            UserSyncEnabled = bool.Parse(GetScopedSetting(Service + "_UserSyncEnabled", portalId, "false"));
            ProfileSyncEnabled = bool.Parse(GetScopedSetting(Service + "_ProfileSyncEnabled", portalId, "false"));
            JwtAuthEnabled = bool.Parse(GetScopedSetting(Service + "_JwtAuthEnabled", portalId, "false"));
            APIResource = GetScopedSetting(Service + "_APIResource", portalId, "");
            Scopes = GetScopedSetting(Service + "_Scopes", portalId, "");
            B2cApplicationId = GetScopedSetting(Service + "_B2CApplicationId", portalId, "");
            UsernamePrefixEnabled = bool.Parse(GetScopedSetting(Service + "_UsernamePrefixEnabled", portalId, "true"));
            GroupNamePrefixEnabled = bool.Parse(GetScopedSetting(Service + "_GroupNamePrefixEnabled", portalId, "true"));
            RopcPolicy = GetScopedSetting(Service + "_RopcPolicy", portalId, "");
            ImpersonatePolicy = GetScopedSetting(Service + "_ImpersonatePolicy", portalId, "");
            AutoAuthorize = bool.Parse(GetScopedSetting(Service + "_AutoAuthorize", portalId, "true"));
            AutoMatchExistingUsers = bool.Parse(GetScopedSetting(Service + "_AutoMatchExistingUsers", portalId, "false"));
            ExpiredRolesSyncEnabled = bool.Parse(ConfigurationManager.AppSettings["AzureB2C_ExpiredRolesSyncEnabled"] ?? "false");
        }

        public static string GetSetting(string service, string key, int portalId, string defaultValue)
        {
            var useGlobalSettings = bool.Parse(HostController.Instance.GetString(service + "_UseGlobalSettings", "false"));
            if (useGlobalSettings)
                return HostController.Instance.GetString($"{service}_{key}", defaultValue);
            return PortalController.GetPortalSetting($"{service}_{key}", portalId, defaultValue);
        }

        internal string GetScopedSetting(string key, int portalId, string defaultValue)
        {
            if (UseGlobalSettings)
                return HostController.Instance.GetString(key, defaultValue);
            return PortalController.GetPortalSetting(key, portalId, defaultValue);
        }

        [SortOrder(1)]
        public string TenantName { get; set; }

        [SortOrder(2)]
        public string TenantId { get; set; }
        [SortOrder(3)]
        public bool AutoRedirect { get; set; }
        [SortOrder(4)]
        public string SignUpPolicy { get; set; }
        [SortOrder(5)]
        public string ProfilePolicy { get; set; }
        [SortOrder(6)]
        public string PasswordResetPolicy { get; set; }
        [SortOrder(7)]
        public string AADApplicationId { get; set; }
        [SortOrder(8)]
        public string AADApplicationKey { get; set; }
        [SortOrder(8)]
        public string JwtAudiences { get; set; }
        [SortOrder(9)]
        public bool RoleSyncEnabled { get; set; }
        [SortOrder(10)]
        public bool ProfileSyncEnabled { get; set; }
        [SortOrder(11)]
        public bool JwtAuthEnabled { get; set; }

        [SortOrder(12)]
        public string APIResource { get; set; }
        [SortOrder(13)]
        public string Scopes { get; set; }
        [SortOrder(14)]
        public bool UseGlobalSettings { get; set; }
        [SortOrder(15)]
        public string RedirectUri { get; set; }
        [SortOrder(16)]
        public string B2cApplicationId { get; set; }

        [SortOrder(17)]
        public bool UsernamePrefixEnabled { get; set; }
        [SortOrder(18)]
        public bool GroupNamePrefixEnabled { get; set; }
        [SortOrder(19)]
        public string RopcPolicy { get; set; }
        [SortOrder(20)]
        public string ImpersonatePolicy { get; set; }
        [SortOrder(21)]
        public bool AutoAuthorize { get; set; }
        [SortOrder(22)]
        public string OnErrorUri { get; set; }
        [SortOrder(23)]
        public bool UserSyncEnabled { get; set; }
        [SortOrder(24)]
        public bool AutoMatchExistingUsers { get; set; }
        [SortOrder(25)]
        public bool ExpiredRolesSyncEnabled { get; set; }

        private static string GetCacheKey(string service, int portalId)
        {
            return _cacheKey + "." + service + "_" + portalId;
        }

        public new static AzureConfig GetConfig(string service, int portalId)
        {
            string key = GetCacheKey(service, portalId);
            AzureConfig config = null;

            try
            {
                config = (AzureConfig)DataCache.GetCache(key);

            } catch(Exception ex)
            {
                Exceptions.LogException(ex);
            }
            
            if (config == null)
            {
                config = new AzureConfig(service, portalId);
                DataCache.SetCache(key, config);
            }
            return config;
        }

        public static void UpdateConfig(AzureConfig config)
        {
            HostController.Instance.Update(config.Service + "_UseGlobalSettings", config.UseGlobalSettings.ToString(),
                true);
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_ApiKey", config.APIKey);
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_ApiSecret", config.APISecret);
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_RedirectUri", config.RedirectUri);
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_OnErrorUri", config.OnErrorUri);
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_TenantName", config.TenantName);
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_TenantId", config.TenantId);
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_AutoRedirect", config.AutoRedirect.ToString());
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_Enabled", config.Enabled.ToString());
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_SignUpPolicy", config.SignUpPolicy);
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_ProfilePolicy", config.ProfilePolicy);
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_PasswordResetPolicy", config.PasswordResetPolicy);
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_AADApplicationId", config.AADApplicationId);
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_AADApplicationKey", config.AADApplicationKey);
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_JwtAudiences", config.JwtAudiences);
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_RoleSyncEnabled", config.RoleSyncEnabled.ToString());
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_UserSyncEnabled", config.UserSyncEnabled.ToString());
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_ProfileSyncEnabled", config.ProfileSyncEnabled.ToString());
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_JwtAuthEnabled", config.JwtAuthEnabled.ToString());
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_APIResource", config.APIResource);
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_Scopes", config.Scopes);
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_UsernamePrefixEnabled", config.UsernamePrefixEnabled.ToString());
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_GroupNamePrefixEnabled", config.GroupNamePrefixEnabled.ToString());
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_RopcPolicy", config.RopcPolicy);
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_ImpersonatePolicy", config.ImpersonatePolicy);
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_AutoAuthorize", config.AutoAuthorize.ToString());
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_AutoMatchExistingUsers", config.AutoMatchExistingUsers.ToString());

            config.B2cApplicationId = UpdateB2CApplicationId(config);

            UpdateConfig((OAuthConfigBase)config);

            // Clear config after update
            DataCache.RemoveCache(GetCacheKey(config.Service, config.PortalID));
        }

        internal static void UpdateScopedSetting(bool useGlobalSettings, int portalId, string key, string value)
        {
            if (useGlobalSettings)
                HostController.Instance.Update(key, value, true);
            else
                PortalController.UpdatePortalSetting(portalId, key, value, true); // BUG: DNN 9.3.2 not storing IsSecure column on DB (see UpdatePortalSetting stored procedure) https://github.com/dnnsoftware/Dnn.Platform/issues/2874
        }

        internal static string GetScopedSetting(bool useGlobalSettings, int portalId, string key, string defaultValue)
        {
            return useGlobalSettings
                ? HostController.Instance.GetString(key, defaultValue)
                : PortalController.GetPortalSetting(key, portalId, defaultValue); // BUG: DNN 9.3.2 not storing IsSecure column on DB (see UpdatePortalSetting stored procedure) https://github.com/dnnsoftware/Dnn.Platform/issues/2874
        }

        private static string UpdateB2CApplicationId(AzureConfig config)
        {
            var b2cApplicationId = "";
            if (!string.IsNullOrEmpty(config.AADApplicationId)
                && !string.IsNullOrEmpty(config.AADApplicationKey)
                && !string.IsNullOrEmpty(config.TenantId))
            {
                var graphClient = new Graph.GraphClient(config.AADApplicationId, config.AADApplicationKey, config.TenantId);
                var extensionApp = graphClient.GetB2CExtensionApplication();
                b2cApplicationId = extensionApp?.AppId;
                if (string.IsNullOrEmpty(b2cApplicationId))
                {
                    throw new ConfigurationErrorsException("Can't find B2C Application on current tenant. Ensure the application 'b2c-extensions-app' exists.");
                }

                /*var extensions = graphClient.GetExtensions(extensionApp?.ObjectId);                
                var portalIdExtension = extensions.Values.FirstOrDefault(x => x.Name.ToLowerInvariant() == $"extension_{b2cApplicationId.Replace("-", "")}_portalid");
                //if (portalIdExtension != null)
                //    graphClient.UnregisterExtension(extensionApp?.ObjectId, portalIdExtension.ObjectId);
                if (portalIdExtension == null)
                {
                    portalIdExtension = new Graph.Models.Extension()
                    {
                        ODataType = "Microsoft.DirectoryServices.ExtensionProperty",
                        ObjectType = "ExtensionProperty",
                        Name = "portalId",
                        DataType = "Integer",
                        TargetObjects = new string[] { "User" }
                    };
                    graphClient.RegisterExtension(extensionApp?.ObjectId, portalIdExtension);

                    var users = graphClient.GetAllUsers("").Values;
                    var extensionName = $"extension_{b2cApplicationId.Replace("-", "")}_portalId";
                    foreach (var user in users)
                    {
                        if (!user.AdditionalData.ContainsKey(extensionName))
                        {
                            user.AdditionalData.Clear();
                            user.AdditionalData.Add(extensionName, config.PortalID);
                            graphClient.UpdateUser(user);
                        }
                    }
                }    */
            }
            UpdateScopedSetting(config.UseGlobalSettings, config.PortalID, config.Service + "_B2CApplicationId", b2cApplicationId);
            return b2cApplicationId;

        }

        public GraphClient GetGraphClient()
        {
            if (string.IsNullOrEmpty(AADApplicationId) || string.IsNullOrEmpty(AADApplicationKey) || string.IsNullOrEmpty(TenantId))
            {
                throw new ConfigurationErrorsException("Azure B2C configuration is not complete. Please check the settings.");
            }
            return new GraphClient(AADApplicationId, AADApplicationKey, TenantId);
        }
    }
}
