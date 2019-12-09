# Signin custom policy with force password reset option
This custom policy is based on the policy available at https://github.com/azure-ad-b2c/samples/tree/master/policies/force-password-reset-first-logon, adding portalId claim and some more options. 

This solution demonstrates how to solve the force password reset scenario, where you want to force the user to reset their password on the next logon by using an Azure AD B2C custom policy.

# Prerequisites
Before deploying a custom policy, you must be familiarized with the custom policies building blocks and the rest of the Identity Experience Framework (IEF). Be sure to complete the steps described at: https://docs.microsoft.com/en-us/azure/active-directory-b2c/active-directory-b2c-get-started-custom?tabs=applications
1. Add signing and encryption keys
2. Register Identity Experience Framework applications


# Setup
To use the policy:
1. Create a new Azure AD B2C attribute through the Azure portal called "mustResetPassword" of type boolean. You will need to update that attribute by using the Graph API or any other method (like using the new B2C User Management DNN module) to force the password reset.
2. Optionally, if you are going to use the Azure AD B2C tenant with multiple DNN portals, create another attribute called "portalId" of type Integer. If this attribute exists and is mapped through the extension mapping settings, it will be automatically filled up and consumed by the DNN Azure AD B2C provider.
3. Download the files on this folder so you can customize the contents:
- TrustFrameworkBase.xml
- ForcePasswordReset_TrustFrameworkExtensions.xml
- ForcePasswordReset_Signin.xml
4. Change "YOURTENANTNAME.onmicrosoft.com" with the name of your B2C tenant on TrustFrameworkBase.xml, ForcePasswordReset_TrustFrameworkExtensions.xml and ForcePasswordReset_Signin.xml files
5. Replace the 00000000-0000-0000-0000-000000000000 with the Application Insights instrumentation key in the ForcePasswordReset_Signin.xml. If you don't want to use Application Insights tracing, you can disable the user journey recorder by deleting the following attributes on the same file:
	DeploymentMode="Development"
	UserJourneyRecorderEndpoint="urn:journeyrecorder:applicationinsights"
and completely removing the node "UserJourneyBehaviors". 
6. Replace the 11111111-1111-1111-1111-111111111111 on the ForcePasswordReset_TrustFrameworkExtensions.xml file with the Application Object Id of the application that will be used for reading the attributes using the graph API
7. Replace the 22222222-2222-2222-2222-222222222222 on the ForcePasswordReset_TrustFrameworkExtensions.xml file with the Client ID of the application that will be used for reading the attributes using the graph API
8. Replace the 33333333-3333-3333-3333-333333333333 on the ForcePasswordReset_TrustFrameworkExtensions.xml file with the Client ID of the ProxyIdentityExperienceFramework application
9. Replace the 44444444-4444-4444-4444-444444444444 on the ForcePasswordReset_TrustFrameworkExtensions.xml file with the Client ID of the IdentityExperienceFramework application
10. If you want to enable the "signup" link, on the TrustFrameworkBase.xml file look for the technical profile "SelfAsserted-LocalAccountSignin-Email" and change the metadata child element value "setting.showSignupLink" to "true"
11. Upload the custom policy files to the Azure AD B2C tenant. You can upload the custom policies through the Azure portal, selecting the Azure AD B2C tenant, and then on the "Identity Experience Framework" section click on the "Upload custom policy" button. Note that you will need to upload them in order:
- TrustFrameworkBase.xml
- ForcePasswordReset_TrustFrameworkExtensions.xml
- ForcePasswordReset_Signin.xml

# Test the policy
1. Change the "mustResetPassword" attribute to a user (i.e. by using the B2CGraphAPI tool, https://docs.microsoft.com/es-es/azure/active-directory-b2c/active-directory-b2c-devquickstarts-graph-dotnet?tabs=applications).
2. Test the policy following the steps described at https://docs.microsoft.com/en-us/azure/active-directory-b2c/active-directory-b2c-get-started-custom?tabs=applications#test-the-custom-policy





