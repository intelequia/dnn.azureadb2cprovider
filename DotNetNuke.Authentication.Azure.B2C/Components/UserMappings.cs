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

using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;
using System.Xml.Serialization;

/// <summary>
/// XML example that this class can read:
///       <UserMappings>
///         <userMapping dnnPropertyName = "PortalId"
///                      b2cPropertyName = "PortalId" />
///         <userMapping dnnPropertyName = "username"
///                      b2cPropertyName = "sub" />
///       </UserMappings>
/// </summary>
namespace DotNetNuke.Authentication.Azure.B2C.Components
{
    [Serializable]
    [XmlRoot]
    [DataContract]
    public class UserMappings
    {
        public const string DefaultUserMappingsFilePath = "~/DesktopModules/AuthenticationServices/AzureB2C/DnnUserMappings.config";

        [XmlElement("userMapping")]
        [DataMember]
        public UserMappingsUserMapping[] UserMapping { get; set; }

        public static UserMappings GetUserMappings()
        {
            return GetUserMappings(HttpContext.Current.Server.MapPath(DefaultUserMappingsFilePath));
        }

        public static UserMappings GetUserMappings(string filePath)
        {
            UserMappings result;
            if (!File.Exists(filePath))
            {
                result = new UserMappings();
            }
            else
            {
                var serializer = new XmlSerializer(typeof(UserMappings));
                using (var fileStream = new FileStream(filePath, FileMode.Open))
                {
                    result = (UserMappings)serializer.Deserialize(fileStream);
                }
            }

            if (result.UserMapping == null)
            {
                result.UserMapping = new UserMappingsUserMapping[0];
            }

            return result;
        }

        public static UserMappingsUserMapping GetFieldUserMapping(string filePath, string fieldName)
        {
            return GetUserMappings(filePath).UserMapping.FirstOrDefault(x => x.DnnPropertyName == fieldName);
        }

        public static void UpdateUserMappings(UserMappings userMappings)
        {
            UpdateUserMappings(HttpContext.Current.Server.MapPath(DefaultUserMappingsFilePath), userMappings);
        }
        public static void UpdateUserMappings(string filePath, UserMappings userMappings)
        {
            var serializer = new XmlSerializer(typeof(UserMappings));
            var ns = new XmlSerializerNamespaces();
            ns.Add("", "");  // This is to avoid the xmlns namespace in the xml elements

            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite))
            {
                serializer.Serialize(fileStream, userMappings, ns);
            }
        }

    }

    [Serializable]
    [DataContract]
    public class UserMappingsUserMapping
    {
        [XmlAttribute("dnnPropertyName")]
        [DataMember]
        public string DnnPropertyName { get; set; }

        [XmlAttribute("b2cPropertyName")]
        [DataMember]
        public string B2cPropertyName { get; set; }
    }
}