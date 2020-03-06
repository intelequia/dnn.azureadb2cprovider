using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace DotNetNuke.Authentication.Azure.B2C.Components
{
    public class B2CControllerConfiguration
    {
        public string RopcPolicyName { get; set; }
        public OpenIdConnectConfiguration OpenIdConfig { get; set; }

        public B2CControllerConfiguration(string ropcPolicyName, OpenIdConnectConfiguration config)
        {
            RopcPolicyName = ropcPolicyName;
            OpenIdConfig = config;
        }

        /// <summary>
        /// This is to check if the current OpenIdConnectConfiguration is still valid. If the value of the ROPC
        /// policy has been changed by the user, this configuration is not valid anymore
        /// </summary>
        public bool IsValid(AzureConfig azureB2cConfig, string defaultRopcPolicy)
        {
            if (RopcPolicyName.ToLower() == azureB2cConfig.RopcPolicy.ToLower())
                return true;

            if (RopcPolicyName.ToLower() == defaultRopcPolicy.ToLower() && string.IsNullOrEmpty(azureB2cConfig.RopcPolicy))
                return true;

            return false;
        }
    }
}
