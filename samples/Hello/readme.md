# How to setup this ROPC example

This is an example application that allows the user to login into Azure AD B2C directory
to obtain a JWT token, and then call a DNN Website that has been setup with the 
DNN Azure AD B2C Auth provider
You need to:
1. [Create a ROPC policy in B2C](https://docs.microsoft.com/es-es/azure/active-directory-b2c/configure-ropc?tabs=applications#create-a-resource-owner-user-flow) and ensure you specify the "emails" claim on the policy
2. [Register an application in B2C](https://docs.microsoft.com/es-es/azure/active-directory-b2c/configure-ropc?tabs=applications#register-an-application)
3. Setup the DNN portal to enable the JWT Authorization through the advanced settings of the module, and add the APPLICATION ID to the list of valid audiences

More info at https://docs.microsoft.com/es-es/azure/active-directory-b2c/configure-ropc
