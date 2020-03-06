# How to setup this ROPC example

This is an example application that allows the user to login into Azure AD B2C directory
to obtain a JWT token, and then call a DNN Website that has been setup with the 
DNN Azure AD B2C Auth provider
You need to:
1. Create a ROPC policy in B2C and ensure you specify the "emails" claim on the policy
2. Register an application
3. Setup the DNN portal to enable the JWT auth through the advanced settings, and add the applicationId 
to the list of valid audiences

More info at https://docs.microsoft.com/es-es/azure/active-directory-b2c/configure-ropc
