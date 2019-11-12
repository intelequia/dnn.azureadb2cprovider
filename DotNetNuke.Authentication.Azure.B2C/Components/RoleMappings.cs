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
///     <RoleMappings>
///         <RoleMapping dnnRoleName = "Registered Users"
///                         b2cRoleName="Users" />
///         <RoleMapping dnnRoleName = "Administrators"
///                         b2cRoleName="Administrators" />
///     </RoleMappings>
/// </summary>
namespace DotNetNuke.Authentication.Azure.B2C.Components
{
    [Serializable]
    [XmlRoot]
    [DataContract]
    public class RoleMappings
    {
        public const string DefaultRoleMappingsFilePath = "~/DesktopModules/AuthenticationServices/AzureB2C/DnnRoleMappings.config";

        [XmlElement("roleMapping")]
        [DataMember]
        public RoleMappingsRoleMapping[] RoleMapping { get; set; }

        public static RoleMappings GetRoleMappings()
        {
            return GetRoleMappings(HttpContext.Current.Server.MapPath(DefaultRoleMappingsFilePath));
        }

        public static RoleMappings GetRoleMappings(string filePath)
        {
            RoleMappings result;
            if (!File.Exists(filePath))
            {
                result = new RoleMappings();
            }
            else
            {
                var serializer = new XmlSerializer(typeof(RoleMappings));
                using (var fileStream = new FileStream(filePath, FileMode.Open))
                {
                    result = (RoleMappings)serializer.Deserialize(fileStream);
                }
            }

            if (result.RoleMapping == null)
            {
                result.RoleMapping = new RoleMappingsRoleMapping[0];
            }

            return result;
        }

        public static RoleMappingsRoleMapping GetFieldRoleMapping(string filePath, string fieldName)
        {
            return GetRoleMappings(filePath).RoleMapping.FirstOrDefault(x => x.DnnRoleName == fieldName);
        }

        public static void UpdateRoleMappings(RoleMappings roleMappings)
        {
            UpdateRoleMappings(HttpContext.Current.Server.MapPath(DefaultRoleMappingsFilePath), roleMappings);
        }
        public static void UpdateRoleMappings(string filePath, RoleMappings roleMappings)
        {
            var serializer = new XmlSerializer(typeof(RoleMappings));
            var ns = new XmlSerializerNamespaces();
            ns.Add("", "");  // This is to avoid the xmlns namespace in the xml elements

            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite))
            {
                serializer.Serialize(fileStream, roleMappings, ns);
            }
        }

    }

    [Serializable]
    [DataContract]
    public class RoleMappingsRoleMapping
    {
        [XmlAttribute("dnnRoleName")]
        [DataMember]
        public string DnnRoleName { get; set; }

        [XmlAttribute("b2cRoleName")]
        [DataMember]
        public string B2cRoleName { get; set; }
    }
}