using Microsoft.Graph;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace DotNetNuke.Authentication.Azure.B2C.Components.Graph.Models
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class NewUser : User
    {
        public NewUser()
        {
            Initialize();
        }

        public NewUser(User user, bool initializeForAdd = true)
        {
            //Get the list of properties available in base class
            var properties = user.GetType().GetProperties();

            properties.ToList().ForEach(property =>
            {
                //Check whether that property is present in derived class
                var isPresent = this.GetType().GetProperty(property.Name);
                if (isPresent != null)
                {
                    //If present get the value and map it
                    var value = user.GetType().GetProperty(property.Name).GetValue(user, null);
                    this.GetType().GetProperty(property.Name).SetValue(this, value, null);
                }
            });

            Initialize(initializeForAdd);
        }

        private void Initialize(bool initializeForAdd = true)
        {
            if (AdditionalData == null)
            {
                AdditionalData = new Dictionary<string, object>();
            }
            if (initializeForAdd)
            {
                AccountEnabled = true;
                if (Identities == null)
                {
                    Identities = new List<ObjectIdentity>();
                }
            }
            PasswordPolicies = "DisablePasswordExpiration";
            PasswordProfile = new PasswordProfile()
            {
                ForceChangePasswordNextSignIn = false
            };
        }

    }

}
