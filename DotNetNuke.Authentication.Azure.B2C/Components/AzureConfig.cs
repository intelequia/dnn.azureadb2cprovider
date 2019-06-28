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

using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Services.Authentication.OAuth;
using DotNetNuke.UI.WebControls;

namespace DotNetNuke.Authentication.Azure.B2C.Components
{
    public class AzureConfig : OAuthConfigBase
    {
        private const string _cacheKey = "Authentication";
        
        private AzureConfig() : base("", 0)
        { }
        protected internal AzureConfig(string service, int portalId) : base(service, portalId)
        {
            APIKey = PortalController.GetPortalSetting(Service + "_ApiKey", portalId, "");
            APISecret = PortalController.GetPortalSetting(Service + "_ApiSecret", portalId, "");
            TenantName = PortalController.GetPortalSetting(Service + "_TenantName", portalId, "");
            TenantId = PortalController.GetPortalSetting(Service + "_TenantId", portalId, "");
            SignUpPolicy = PortalController.GetPortalSetting(Service + "_SignUpPolicy", portalId, "");
            ProfilePolicy = PortalController.GetPortalSetting(Service + "_ProfilePolicy", portalId, "");
            PasswordResetPolicy = PortalController.GetPortalSetting(Service + "_PasswordResetPolicy", portalId, "");
            AutoRedirect = bool.Parse(PortalController.GetPortalSetting(Service + "_AutoRedirect", portalId, "false"));
            Enabled = bool.Parse(PortalController.GetPortalSetting(Service + "_Enabled", portalId, "false"));
            AADApplicationId = PortalController.GetPortalSetting(Service + "_AADApplicationId", portalId, "");
            AADApplicationKey = PortalController.GetPortalSetting(Service + "_AADApplicationKey", portalId, "");
            JwtAudiences = PortalController.GetPortalSetting(Service + "_JwtAudiences", portalId, "");
            RoleSyncEnabled = bool.Parse(PortalController.GetPortalSetting(Service + "_RoleSyncEnabled", portalId, "false"));
            ProfileSyncEnabled = bool.Parse(PortalController.GetPortalSetting(Service + "_ProfileSyncEnabled", portalId, "false"));
            JwtAuthEnabled = bool.Parse(PortalController.GetPortalSetting(Service + "_JwtAuthEnabled", portalId, "false"));
            APIResource = PortalController.GetPortalSetting(Service + "_APIResource", portalId, "");
            Scopes = PortalController.GetPortalSetting(Service + "_Scopes", portalId, "");
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



        private static string GetCacheKey(string service, int portalId)
        {
            return _cacheKey + "." + service + "_" + portalId;
        }

        public new static AzureConfig GetConfig(string service, int portalId)
        {
            string key = GetCacheKey(service, portalId);
            var config = (AzureConfig)DataCache.GetCache(key);
            if (config == null)
            {
                config = new AzureConfig(service, portalId);
                DataCache.SetCache(key, config);
            }
            return config;
        }

        public static void UpdateConfig(AzureConfig config)
        {
            PortalController.UpdatePortalSetting(config.PortalID, config.Service + "_ApiKey", config.APIKey, true, Null.NullString, false);
            PortalController.UpdatePortalSetting(config.PortalID, config.Service + "_ApiSecret", config.APISecret, true, Null.NullString, false); // BUG: DNN 9.3.2 not storing IsSecure column on DB (see UpdatePortalSetting stored procedure) https://github.com/dnnsoftware/Dnn.Platform/issues/2874
            PortalController.UpdatePortalSetting(config.PortalID, config.Service + "_TenantName", config.TenantName, true, Null.NullString, false);
            PortalController.UpdatePortalSetting(config.PortalID, config.Service + "_TenantId", config.TenantId, true, Null.NullString, false);
            PortalController.UpdatePortalSetting(config.PortalID, config.Service + "_AutoRedirect", config.AutoRedirect.ToString(), true, Null.NullString, false);
            PortalController.UpdatePortalSetting(config.PortalID, config.Service + "_Enabled", config.Enabled.ToString(), true, Null.NullString, false);
            PortalController.UpdatePortalSetting(config.PortalID, config.Service + "_SignUpPolicy", config.SignUpPolicy, true, Null.NullString, false);
            PortalController.UpdatePortalSetting(config.PortalID, config.Service + "_ProfilePolicy", config.ProfilePolicy, true, Null.NullString, false);
            PortalController.UpdatePortalSetting(config.PortalID, config.Service + "_PasswordResetPolicy", config.PasswordResetPolicy, true, Null.NullString, false);
            PortalController.UpdatePortalSetting(config.PortalID, config.Service + "_AADApplicationId", config.AADApplicationId, true, Null.NullString, false);
            PortalController.UpdatePortalSetting(config.PortalID, config.Service + "_AADApplicationKey", config.AADApplicationKey, true, Null.NullString, false);  // BUG: DNN 9.3.2 not storing IsSecure column on DB (see UpdatePortalSetting stored procedure) https://github.com/dnnsoftware/Dnn.Platform/issues/2874
            PortalController.UpdatePortalSetting(config.PortalID, config.Service + "_JwtAudiences", config.JwtAudiences, true, Null.NullString, false);
            PortalController.UpdatePortalSetting(config.PortalID, config.Service + "_RoleSyncEnabled", config.RoleSyncEnabled.ToString(), true, Null.NullString, false);
            PortalController.UpdatePortalSetting(config.PortalID, config.Service + "_ProfileSyncEnabled", config.ProfileSyncEnabled.ToString(), true, Null.NullString, false);
            PortalController.UpdatePortalSetting(config.PortalID, config.Service + "_JwtAuthEnabled", config.JwtAuthEnabled.ToString(), true, Null.NullString, false);
            PortalController.UpdatePortalSetting(config.PortalID, config.Service + "_APIResource", config.APIResource, true, Null.NullString, false);
            PortalController.UpdatePortalSetting(config.PortalID, config.Service + "_Scopes", config.Scopes, true, Null.NullString, false);

            UpdateConfig((OAuthConfigBase)config);
        }
    }
}