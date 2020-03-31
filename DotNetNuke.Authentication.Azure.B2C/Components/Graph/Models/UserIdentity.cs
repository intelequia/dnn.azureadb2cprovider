using Newtonsoft.Json;

namespace DotNetNuke.Authentication.Azure.B2C.Components.Graph.Models
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class UserIdentity
    {
        /// <summary>
        /// Gets or sets display name.
        /// The display name for the group. This property is required when a group is created and cannot be cleared during updates. Returned by default. Supports $filter and $orderby.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "issuer", Required = Newtonsoft.Json.Required.Default)]
        public string Issuer { get; set; }
    }
}
