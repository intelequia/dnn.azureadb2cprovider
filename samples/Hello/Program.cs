using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace Hello
{
    class Program
    {
        public class GetTokenResponse
        {
            public string access_token { get; set; }
            public string token_type { get; set; }
            public int expires_in { get; set; }
            public string refresh_token { get; set; }
            public string id_token { get; set; }
        }

        static async Task Main(string[] args)
        {
            // This is an example application that allows the user to login into Azure AD B2C directory
            // to obtain a JWT token, and then call a DNN Website that has been setup with the 
            // DNN Azure AD B2C Auth provider
            // You need to:
            // 1. Create a ROPC policy in B2C and ensure you specify the "emails" claim on the policy
            // 2. Register an application
            // 3. Setup the DNN portal to enable the JWT auth through the advanced settings, and add the applicationId 
            //    to the list of valid audiences
            // More info at https://docs.microsoft.com/es-es/azure/active-directory-b2c/configure-ropc

            const string tenantName = "yourtenantname";
            const string policyName = "b2c_1_ropc";
            const string applicationId = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxx";
            const string helloDnnEndpoint = "https://yoursiteurl/DesktopModules/DotNetNuke.Authentication.Azure.B2C.Services/api/Hello/Test";

            var tokenEndpoint = $"https://{tenantName}.b2clogin.com/{tenantName}.onmicrosoft.com/oauth2/v2.0/token?p={policyName}" 
                                + $"&scope=openid+{applicationId}+offline_access&client_id={applicationId}&response_type=token+id_token&grant_type=password";

            var user = ReadUsername();
            var password = ReadPassword();

            try
            {
                var tokenResponse = await GetTokenAsync(user, password, tokenEndpoint);

                Console.WriteLine("Login successfull. Obtaining user info from DNN instance...");

                var whoAmI = await GetHello(helloDnnEndpoint, tokenResponse.access_token);
                Console.WriteLine(whoAmI);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
            }

        }

        static string ReadUsername()
        {
            string user;
            do
            {
                Console.Write("User: ");
                user = Console.ReadLine().Trim();
                if (!string.IsNullOrEmpty(user))
                {
                    break;
                }
                Console.WriteLine("Bad username, please type your username");
            } while (true);

            return user;
        }

        static string ReadPassword()
        {
            string pass = "";
            Console.Write("Password: ");
            do
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    pass += key.KeyChar;
                    Console.Write("*");
                }
                else
                {
                    if (key.Key == ConsoleKey.Backspace && pass.Length > 0)
                    {
                        pass = pass.Substring(0, (pass.Length - 1));
                        Console.Write("\b \b");
                    }
                    else if (key.Key == ConsoleKey.Enter)
                    {
                        break;
                    }
                }
            } while (true);
            Console.WriteLine();
            return pass;
        }


        static async Task<GetTokenResponse> GetTokenAsync(string userName, string password, string tokenEndpoint)
        {
            tokenEndpoint += $"&username={HttpUtility.UrlEncode(userName)}";
            tokenEndpoint += $"&password={HttpUtility.UrlEncode(password)}";

            var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
            using (var client = new HttpClient())
            {
                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var getTokenResult = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<GetTokenResponse>(getTokenResult);
                }
                else
                {
                    var errorDesc = await response.Content.ReadAsStringAsync();
                    throw new ApplicationException(errorDesc);
                }
            }
        }

        static async Task<string> GetHello(string endpoint, string authToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("Authorization", $"Bearer {authToken}");
            using (var client = new HttpClient())
            {
                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    var errorDesc = await response.Content.ReadAsStringAsync();
                    throw new ApplicationException(errorDesc);
                }
            }
        }
    }
}
