#region Copyright

// 
// Intelequia Software solutions - https://intelequia.com
// Copyright (c) 2019
// by Intelequia Software Solutions
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
// documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
// the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
// to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or substantial portions 
// of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
// TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
// CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE.

#endregion

using DotNetNuke.Authentication.Azure.B2C.Components.Graph;
using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;
using System.Xml.Serialization;

/// <summary>
/// XML example that this class can read:
///     <ProfileMappings>
///         <profileMapping dnnProfilePropertyName = "PreferredLocale"
///                         b2cClaimName="extension_Locale" />
///         <profileMapping dnnProfilePropertyName = "Telephone"
///                         b2cClaimName="extension_phoneNumber" />
///     </ProfileMappings>
/// </summary>
namespace DotNetNuke.Authentication.Azure.B2C.Components
{
    [Serializable]
    [XmlRoot]
    [DataContract]
    public class ProfileMappings
    {
        public const string DefaultProfileMappingsFilePath = "~/DesktopModules/AuthenticationServices/AzureB2C/DnnProfileMappings.config";

        [XmlElement("profileMapping")]
        [DataMember]
        public ProfileMappingsProfileMapping[] ProfileMapping { get; set; }

        public static ProfileMappings GetProfileMappings()
        {
            return GetProfileMappings(HttpContext.Current.Server.MapPath(DefaultProfileMappingsFilePath));
        }

        public static ProfileMappings GetProfileMappings(string filePath)
        {
            if (!File.Exists(filePath))
                return new ProfileMappings();

            var serializer = new XmlSerializer(typeof(ProfileMappings));
            using (var fileStream = new FileStream(filePath, FileMode.Open))
            {
                return (ProfileMappings)serializer.Deserialize(fileStream);
            }
        }


        public static ProfileMappingsProfileMapping GetFieldProfileMapping(string filePath, string fieldName)
        {
            return GetProfileMappings(filePath).ProfileMapping.FirstOrDefault(x => x.DnnProfilePropertyName == fieldName);
        }

        public static void UpdateProfileMappings(ProfileMappings profileMappings)
        {
            UpdateProfileMappings(HttpContext.Current.Server.MapPath(DefaultProfileMappingsFilePath), profileMappings);
        }
        public static void UpdateProfileMappings(string filePath, ProfileMappings profileMappings)
        {
            var serializer = new XmlSerializer(typeof(ProfileMappings));
            var ns = new XmlSerializerNamespaces();
            ns.Add("", "");  // This is to avoid the xmlns namespace in the xml elements

            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite))
            {
                serializer.Serialize(fileStream, profileMappings, ns);
            }
        }

        public static void UpdateProfileMappingsExtensionNames(string filePath, int portalId)
        {
            var config = new AzureConfig("AzureB2C", portalId);

            if (string.IsNullOrEmpty(config.AADApplicationId)
                || string.IsNullOrEmpty(config.AADApplicationKey)
                || string.IsNullOrEmpty(config.TenantId))
                return;

            var graphClient = new GraphClient(config.AADApplicationId, config.AADApplicationKey, config.TenantId);

            var extensionApp = graphClient.GetB2CExtensionApplication();
            var extensions = graphClient.GetExtensions(extensionApp.ObjectId);

            var profileMappings = GetProfileMappings(filePath);
            var changedFlag = false; ;
            foreach (var extension in extensions.Values)
            {
                var mapping = profileMappings.ProfileMapping.FirstOrDefault(x =>
                                    extension.Name.ToLowerInvariant().EndsWith("_" + x.B2cClaimName.ToLowerInvariant()));
                if (mapping != null)
                {
                    changedFlag = true;
                    mapping.B2cExtensionName = extension.Name;
                }
            }

            if (changedFlag)
            {
                UpdateProfileMappings(filePath, profileMappings);
            }
        }
    }

    [Serializable]
    [DataContract]
    public class ProfileMappingsProfileMapping
    {
        [XmlAttribute("dnnProfilePropertyName")]
        [DataMember]
        public string DnnProfilePropertyName { get; set; }

        [XmlAttribute("b2cClaimName")]
        [DataMember]
        public string B2cClaimName { get; set; }

        [XmlAttribute("b2cExtensionName")]
        [DataMember]
        public string B2cExtensionName { get; set; }
    }
}
