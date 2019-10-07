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
    public class RoleMappings
    {
        public const string DefaultRoleMappingsFilePath = "~/DesktopModules/AuthenticationServices/AzureB2C/DnnRoleMappings.config";

        [XmlElement("roleMapping")]
        public RoleMappingsRoleMapping[] RoleMapping { get; set; }

        public static RoleMappings GetRoleMappings(string filePath)
        {
            if (!File.Exists(filePath))
                return new RoleMappings();

            var serializer = new XmlSerializer(typeof(RoleMappings));
            using (var fileStream = new FileStream(filePath, FileMode.Open))
            {
                return (RoleMappings)serializer.Deserialize(fileStream);
            }
        }

        public static RoleMappingsRoleMapping GetFieldRoleMapping(string filePath, string fieldName)
        {
            return GetRoleMappings(filePath).RoleMapping.FirstOrDefault(x => x.DnnRoleName == fieldName);
        }
    }

    [Serializable]
    public class RoleMappingsRoleMapping
    {
        [XmlAttribute("dnnRoleName")]
        public string DnnRoleName { get; set; }

        [XmlAttribute("b2cRoleName")]
        public string B2cRoleName { get; set; }
    }
}