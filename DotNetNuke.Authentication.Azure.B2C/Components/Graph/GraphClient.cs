using DotNetNuke.Instrumentation;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace DotNetNuke.Authentication.Azure.B2C.Components.Graph
{
    public class GraphClient
    {
        private static string[] Scopes = new[] { "https://graph.microsoft.com/.default" };
        private const string UserMembersToRetrieve = "id,displayName,surname,givenName,mail,mailNickname,otherMails,signInNames,userIdentities,identities,issuer,userPrincipalName,country,city,userType,accountEnabled,telephoneNumber,additionalData";
        private static readonly ILog Logger = LoggerSource.Instance.GetLogger(typeof(GraphClient));

        private readonly IConfidentialClientApplication _app;

        // Required for Advanced Queries
        private readonly QueryOption OdataCount = new QueryOption("$count", "true");
        // Required for Advanced Queries
        private readonly HeaderOption EventualConsistency = new HeaderOption("ConsistencyLevel", "eventual");

        public string CustomUserAttributes { get; set; }
        public string B2CApplicationId { get; set; }

        public GraphClient(string clientId, string clientSecret, string tenant, string customUserAttributes = "", string b2cApplicationId = "")
        {
            CustomUserAttributes = customUserAttributes;
            B2CApplicationId = b2cApplicationId;
            _app = ConfidentialClientApplicationBuilder
                    .Create(clientId)
                    .WithClientSecret(clientSecret)
                    .WithAuthority(new Uri("https://login.microsoftonline.com/" + tenant))
                    .Build();

        }

        // Gets a Graph client configured with
        // the specified scopes
        private GraphServiceClient GetGraphClient()
        {
            return GraphServiceClientFactory.GetAuthenticatedGraphClient(async () =>
            {
                var token = await GetTokenAsync(_app);
                return token;
            }
            );
        }

        private async Task<string> GetTokenAsync(IConfidentialClientApplication app)
        {
            string[] ResourceIds = Scopes;
            try
            {
                var result = app.AcquireTokenForClient(ResourceIds).ExecuteSync();
                await Task.CompletedTask;
                return result.AccessToken;
            }
            catch (MsalClientException ex)
            {
                Logger.Error(ex);
                throw;
            }
        }

        private void AddAdvancedOptions(IBaseRequest request)
        {
            request.QueryOptions.Add(OdataCount);
            request.Headers.Add(EventualConsistency);

            // Add extra options
            var options = new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                WriteIndented = true
            };

            Serializer serializer = new Serializer(options);
            IResponseHandler responseHandler = new ResponseHandler(serializer); // Our Response Handler with custom Serializer
            ((BaseRequest) request).WithResponseHandler(responseHandler);
        }

        public string GetExtensionAttributeName(string customAttributeName)
        {
            return $"extension_{B2CApplicationId.Replace("-", "")}_{customAttributeName.Replace(" ", "")}";
        }

        private string GetCustomUserExtensions()
        {
            var c = CustomUserAttributes;
            if (string.IsNullOrEmpty(c))
            {
                c = ConfigurationManager.AppSettings["AzureADB2C.CustomUserExtensions"] ?? "";
            }
            return string.Join(",", c.Split(',').Select(x => GetExtensionAttributeName(x)));
        }

        public User GetUser(string objectId)
        {
            var graphClient = GetGraphClient();
            return graphClient.Users[objectId]
                .Request()
                .Select($"{UserMembersToRetrieve},{GetCustomUserExtensions()}")
                .GetSync();
        }

        public IGraphServiceUsersCollectionPage GetAllUsers(string search = "")
        {
            var graphClient = GetGraphClient();
            var request = graphClient.Users.Request()
                .Select($"{UserMembersToRetrieve},{GetCustomUserExtensions()}")
                .Filter(search);
            if (string.IsNullOrEmpty(search))
            {
                request.OrderBy("displayName");
            }
                
            AddAdvancedOptions(request);

            return FixExtensionDataValues<IGraphServiceUsersCollectionPage>(request.GetSync());
        }

        // Workaround for "ValueKind" issue when deserializing from Microsoft Graph. Revisit this 
        private T FixExtensionDataValues<T>(dynamic obs)
        {
            foreach (var o in obs)
            {
                var data = o.AdditionalData;
                if (data != null)
                {
                    o.AdditionalData = new Dictionary<string, object>();
                    foreach (var d in data)
                    {
                        if (d.Key.StartsWith("extension_"))
                        {
                            o.AdditionalData.Add(d.Key, d.Value.ToString());
                        }
                    }
                }
            }
            return obs;
        }

        public void DeleteUser(string objectId)
        {
            var graphClient = GetGraphClient();
            graphClient.Users[objectId].Request().DeleteSync();
        }

        public User AddUser(User newUser)
        {
            if (newUser.AdditionalData == null)
            {
                newUser.AdditionalData = new Dictionary<string, object>();
            }

            var graphClient = GetGraphClient();
            return graphClient.Users.Request()
                .AddSync(newUser);
        }

        public User UpdateUser(User user)
        {
            var graphClient = GetGraphClient();
            return graphClient.Users[user.Id].Request()
                .UpdateSync(user);
        }

        public User UpdateUserPassword(User user)
        {
            // Downgrading the MS Graph Client from 4.x to 3.x, the user must be a new object instead of 
            // retrieving the current one
            var tmpGraphUser = GetUser(user.Id);
            //var tmpGraphUser = new User();
            tmpGraphUser.Id = user.Id;
            tmpGraphUser.PasswordProfile = new PasswordProfile()
            {
                ForceChangePasswordNextSignIn = user.PasswordProfile.ForceChangePasswordNextSignIn,
                Password = user.PasswordProfile.Password
            };

            // Note: if you get a "Insufficient privileges to complete the operation." error, ensure the service principal 
            // accessing graph has the "User administrator" or "Global Administrator" role.
            // See https://docs.microsoft.com/en-us/answers/questions/9024/error-while-updating-the-password-profile.html
            return UpdateUser(tmpGraphUser);
        }

        public IGraphServiceGroupsCollectionPage GetAllGroups(string search = "")
        {
            string filter = ConfigurationManager.AppSettings["AzureADB2C.GetAllGroups.Filter"];
            if (!string.IsNullOrEmpty(search))
            {
                if (!string.IsNullOrEmpty(filter))
                {
                    filter += " and ";
                }
                filter += $"startswith(displayName, '{search}')";
            }
            var graphClient = GetGraphClient();
            var request = graphClient.Groups.Request()
                .Filter(filter)
                .OrderBy("displayName");
            AddAdvancedOptions(request);
            return request.GetSync();
        }

        public IUserMemberOfCollectionWithReferencesPage GetUserGroups(string userId)
        {
            var graphClient = GetGraphClient();
            return graphClient
                .Users[userId]
                .MemberOf
                .Request()
                .GetSync();
        }

        //public IGroupTransitiveMembersCollectionWithReferencesPage GetGroupMembers(string groupId)
        //{
        //    var graphClient = GetGraphClient();

        //    return graphClient.Groups[groupId].TransitiveMembers.Request()
        //        .Select($"{UserMembersToRetrieve},,{GetCustomUserExtensions()}")
        //        .OrderBy("displayName")
        //        .GetSync();
        //}

        public void UpdateGroupMembers(User user, List<Group> groups)
        {
            var graphClient = GetGraphClient();
            var usersGroups = graphClient.Users[user.Id].MemberOf.Request().GetSync();
            foreach (var group in usersGroups)
            {
                if (!groups.Any(g => g.Id == group.Id))
                {
                    // User is no longer a member of the group, remove it.
                    graphClient.Groups[group.Id].Members[user.Id].Reference.Request().DeleteSync();
                }
            }
            foreach (var group in groups)
            {
                if (!usersGroups.Any(g => g.Id == group.Id))
                {
                    // User is not a member of the group, add them.
                    graphClient.Groups[group.Id].Members.References.Request().AddSync(user);
                }
            }
        }

        public void AddGroupMember(string groupId, string userId)
        {
            var user = GetUser(userId);
            AddGroupMember(groupId, user);
        }
        public void AddGroupMember(string groupId, User user)
        {
            var graphClient = GetGraphClient();
            graphClient.Groups[groupId].Members.References.Request().AddSync(user);
        }

        public void RemoveGroupMember(string groupId, string userId)
        {
            var graphClient = GetGraphClient();
            graphClient.Groups[groupId].Members[userId].Reference.Request().DeleteSync();
        }

        public ProfilePhoto GetUserProfilePictureMetadata(string userId)
        {
            try
            {
                var graphClient = GetGraphClient();
                return graphClient.Users[userId].Photo.Request().GetSync();
            }
            catch (Exception ex)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug(ex.Message, ex);
                }
                // When the user doesn't have profile picture, the request throws a WebException
                return null;
            }
        }

        public byte[] GetUserProfilePicture(string userId)
        {
            try
            {
                var graphClient = GetGraphClient();
                var stream = graphClient.Users[userId].Photo.Content.Request().GetSync();
                using (var memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);
                    return memoryStream.ToArray();
                }
            }
            catch (WebException ex)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug(ex.Message, ex);
                }
                // When the user doesn't have profile picture or the API permission
                // User.Read.All for Type Application has not been consent
                return null;
            }
        }

        internal Microsoft.Graph.Application GetB2CExtensionApplication()
        {
            var graphClient = GetGraphClient();
            var apps = graphClient.Applications.Request().Filter("startswith(displayName, 'b2c-extensions-app')").GetSync();
            return apps?.FirstOrDefault();
        }
    }
}
