// Enter here the user flows and custom policies for your B2C application
// To learn more about user flows, visit https://docs.microsoft.com/en-us/azure/active-directory-b2c/user-flow-overview
// To learn more about custom policies, visit https://docs.microsoft.com/en-us/azure/active-directory-b2c/custom-policy-overview

const b2cPolicies = {
    names: {
        signUpSignIn: "b2c_1_signup",
        forgotPassword: "b2c_1_passwordreset",
        editProfile: "b2c_1_profile"
    },
    authorities: {
        signUpSignIn: {
            authority: "https://mytenant.b2clogin.com/mytenant.onmicrosoft.com/b2c_1_signup",
        },
        forgotPassword: {
            authority: "https://mytenant.b2clogin.com/mytenant.onmicrosoft.com/b2c_1_passwordreset",
        },
        editProfile: {
            authority: "https://mytenant.b2clogin.com/mytenant.onmicrosoft.com/b2c_1_profile"
        }
    },
}