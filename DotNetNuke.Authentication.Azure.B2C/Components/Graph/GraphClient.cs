using DotNetNuke.Authentication.Azure.B2C.Components.Graph.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace DotNetNuke.Authentication.Azure.B2C.Components.Graph
{
    public class GraphClient
    {
        #region Enums
        private enum GraphApiVersion
        {
            beta,
            v1_0
        }
        #endregion

        #region Properties
        private string ClientId { get; set; }
        private string ClientSecret { get; set; }
        private string Tenant { get; set; }
        private AuthenticationContext AuthContext { get; set; }
        private ClientCredential Credential { get; set; }
        #endregion

        #region Constructors
        public GraphClient(string clientId, string clientSecret, string tenant)
        {
            // The client_id, client_secret, and tenant are pulled in from the App.config file
            ClientId = clientId;
            ClientSecret = clientSecret;
            Tenant = tenant;

            // The AuthenticationContext is ADAL's primary class, in which you indicate the direcotry to use.
            AuthContext = new AuthenticationContext("https://login.microsoftonline.com/" + tenant);

            // The ClientCredential is where you pass in your client_id and client_secret, which are 
            // provided to Azure AD in order to receive an access_token using the app's identity.
            Credential = new ClientCredential(clientId, clientSecret);
        }
        #endregion
        
        public User GetUserByObjectId(string objectId)
        {
            var result = SendGraphGetRequest("/users/" + objectId, null);
            return JsonConvert.DeserializeObject<User>(result);
        }

        public GraphList<User> GetAllUsers(string query)
        {
            var result = SendGraphGetRequest("/users", query);
            return JsonConvert.DeserializeObject<GraphList<User>>(result);
        }

        public GraphList<Group> GetAllGroups(string query)
        {
            var result = SendGraphGetRequest("/groups", query);
            return JsonConvert.DeserializeObject<GraphList<Group>>(result);
        }

        public GraphList<Group> GetUserGroups(string userId)
        {
            var result = SendGraphGetRequest($"/users/{userId}/memberOf", null);
            //var result = await SendGraphGetRequest($"/users/{userId}/memberOf?$select=displayName,description", null);
            return JsonConvert.DeserializeObject<GraphList<Group>>(result);
        }

        public ProfilePictureMetadata GetUserProfilePictureMetadata(string userId)
        {
            try
            {
                var result = SendGraphGetRequest("/users/" + userId + "/photo", null, GraphApiVersion.beta);
                return JsonConvert.DeserializeObject<ProfilePictureMetadata>(result);
            }
            catch (WebException)
            {
                // When the user doesn't have profile picture, the request throws a WebException
                return null;
            }
        }

        public byte[] GetUserProfilePicture(string userId)
        {
            try
            {
                var metadata = GetUserProfilePictureMetadata(userId);
                return SendGraphGetBinaryRequest("/users/" + userId + "/photo/$value", null, GraphApiVersion.beta);
            }
            catch (WebException)
            {
                // When the user doesn't have profile picture, the request throws a WebException
                return null;
            }
        }

        private string SendGraphGetRequest(string api, string query, GraphApiVersion apiVersion = GraphApiVersion.v1_0)
        {
            // First, use ADAL to acquire a token using the app's identity (the credential)
            // The first parameter is the resource we want an access_token for; in this case, the Graph API.
            var result = AuthContext.AcquireTokenAsync("https://graph.microsoft.com", Credential).Result;

            // For B2C user managment, be sure to use the 1.6 Graph API version.
            var http = new HttpClient();
            var url = "https://graph.microsoft.com/" + (apiVersion == GraphApiVersion.v1_0 ? "v1.0" : "beta") + api;
            if (!string.IsNullOrEmpty(query))
            {
                url += "&" + query;
            }

            // Append the access token for the Graph API to the Authorization header of the request, using the Bearer scheme.
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
            var response = http.SendAsync(request).Result;

            if (!response.IsSuccessStatusCode)
            {
                var error = response.Content.ReadAsStringAsync().Result;
                var formatted = JsonConvert.DeserializeObject(error);
                throw new WebException("Error Calling the Graph API: \n" + JsonConvert.SerializeObject(formatted, Formatting.Indented));
            }

            return response.Content.ReadAsStringAsync().Result;
        }

        private byte[] SendGraphGetBinaryRequest(string api, string query, GraphApiVersion apiVersion = GraphApiVersion.v1_0)
        {
            // First, use ADAL to acquire a token using the app's identity (the credential)
            // The first parameter is the resource we want an access_token for; in this case, the Graph API.
            var result = AuthContext.AcquireTokenAsync("https://graph.microsoft.com", Credential).Result;

            // For B2C user managment, be sure to use the 1.6 Graph API version.
            HttpClient http = new HttpClient();
            var url = "https://graph.microsoft.com/" + (apiVersion == GraphApiVersion.v1_0 ? "v1.0" : "beta") + api;
            if (!string.IsNullOrEmpty(query))
            {
                url += "&" + query;
            }

            // Append the access token for the Graph API to the Authorization header of the request, using the Bearer scheme.
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
            var response = http.SendAsync(request).Result;

            if (!response.IsSuccessStatusCode)
            {
                var error = response.Content.ReadAsStringAsync().Result;
                var formatted = JsonConvert.DeserializeObject(error);
                throw new WebException("Error Calling the Graph API: \n" + JsonConvert.SerializeObject(formatted, Formatting.Indented));
            }

            return response.Content.ReadAsByteArrayAsync().Result;
        }
    }
}
