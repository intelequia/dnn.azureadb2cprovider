# Impersonation custom policy
This custom policy is based on the policy available at https://github.com/azure-ad-b2c/samples/tree/master/policies/impersonation, adding portalId claim and some more options. 

This solution demonstrates how to solve the impersonation scenario, where you as an admin using the User Management DNN module, want to impersonate as another user by using an Azure AD B2C custom policy.

# Prerequisites
Before deploying a custom policy, you must be familiarized with the custom policies building blocks and the rest of the Identity Experience Framework (IEF). Be sure to complete the steps described at: https://docs.microsoft.com/en-us/azure/active-directory-b2c/active-directory-b2c-get-started-custom?tabs=applications
1. Add signing and encryption keys
2. Register Identity Experience Framework applications


# Setup
To use the policy:
1. Create a new Azure AD B2C attribute through the Azure portal called "canImpersonate" of type boolean. The attribute will be automatically updated when using the new B2C User Management DNN module option "Impersonate as..." in the toolbar to initiate the impersonation flow
2. Optionally, if you are going to use the Azure AD B2C tenant with multiple DNN portals, create another attribute called "portalId" of type Integer. If this attribute exists and is mapped through the extension mapping settings, it will be automatically filled up and consumed by the DNN Azure AD B2C provider.
3. Download the files on this folder so you can customize the contents:
- TrustFrameworkBase.xml
- Impersonation_TrustFrameworkExtensions.xml
- Impersonation.xml
4. Change "YOURTENANTNAME.onmicrosoft.com" with the name of your B2C tenant on TrustFrameworkBase.xml, Impersonation_TrustFrameworkExtensions.xml and Impersonation.xml files
5. Replace the 00000000-0000-0000-0000-000000000000 with the Application Insights instrumentation key in the Impersonation.xml. If you don't want to use Application Insights tracing, you can disable the user journey recorder by deleting the following attributes on the same file:
	DeploymentMode="Development"
	UserJourneyRecorderEndpoint="urn:journeyrecorder:applicationinsights"
and completely removing the node "UserJourneyBehaviors". 
6. Replace the 11111111-1111-1111-1111-111111111111 on the Impersonation_TrustFrameworkExtensions.xml file with the Application Object Id of the application that will be used for reading the attributes using the graph API (the app called "b2c-extensions-app. Do not modify. Used by AADB2C for storing user data.")
7. Replace the 22222222-2222-2222-2222-222222222222 on the Impersonation_TrustFrameworkExtensions.xml file with the Client ID of the application that will be used for reading the attributes using the graph API (the app called "b2c-extensions-app. Do not modify. Used by AADB2C for storing user data.")
8. Replace the 33333333-3333-3333-3333-333333333333 on the Impersonation_TrustFrameworkExtensions.xml file with the Client ID of the ProxyIdentityExperienceFramework application
9. Replace the 44444444-4444-4444-4444-444444444444 on the Impersonation_TrustFrameworkExtensions.xml file with the Client ID of the IdentityExperienceFramework application
10. If you want to enable the "signup" link, on the TrustFrameworkBase.xml file look for the technical profile "SelfAsserted-LocalAccountSignin-Email" and change the metadata child element value "setting.showSignupLink" to "true"
11. Upload the custom policy files to the Azure AD B2C tenant. You can upload the custom policies through the Azure portal, selecting the Azure AD B2C tenant, and then on the "Identity Experience Framework" section click on the "Upload custom policy" button. Note that you will need to upload them in order:
- TrustFrameworkBase.xml
- Impersonation_TrustFrameworkExtensions.xml
- Impersonation.xml

# Test the policy
1. Login to your website with an Azure B2C user with permissions to browse a page with the User Management module.
2. Using the User Management DNN module, click on the link "Impersonate as..." to initiate the impersonation flow
3. Re-enter your the B2C credentials if needed
4. Introduce the B2C email address of the user you want to impersonate