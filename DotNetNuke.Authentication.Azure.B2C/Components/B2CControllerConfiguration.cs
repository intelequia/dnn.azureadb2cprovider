using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace DotNetNuke.Authentication.Azure.B2C.Components
{
    public class B2CControllerConfiguration
    {
        public string PolicyName { get; set; }
        public OpenIdConnectConfiguration OpenIdConfig { get; set; }

        public B2CControllerConfiguration(string policyName, OpenIdConnectConfiguration config)
        {
            PolicyName = policyName;
            OpenIdConfig = config;
        }

        /// <summary>
        /// This is to check if the current OpenIdConnectConfiguration is still valid. If the value of the ROPC
        /// policy has been changed by the user, this configuration is not valid anymore
        /// </summary>
        public bool IsValidSignInPolicy(AzureConfig azureB2cConfig, string defaultSignInPolicy)
        {
            if (PolicyName.ToLower() == azureB2cConfig.SignUpPolicy.ToLower())
                return true;

            if (PolicyName.ToLower() == defaultSignInPolicy.ToLower() && string.IsNullOrEmpty(azureB2cConfig.SignUpPolicy))
                return true;

            return false;
        }

        /// <summary>
        /// This is to check if the current OpenIdConnectConfiguration is still valid. If the value of the ROPC
        /// policy has been changed by the user, this configuration is not valid anymore
        /// </summary>
        public bool IsValidRopcPolicy(AzureConfig azureB2cConfig, string defaultRopcPolicy)
        {
            if (PolicyName.ToLower() == azureB2cConfig.RopcPolicy.ToLower())
                return true;

            if (PolicyName.ToLower() == defaultRopcPolicy.ToLower() && string.IsNullOrEmpty(azureB2cConfig.RopcPolicy))
                return true;

            return false;
        }
    }
}
