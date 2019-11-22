using System;
using System.Web.Caching;
using DotNetNuke.ComponentModel.DataAnnotations;

namespace DotNetNuke.Authentication.Azure.B2C.Components.Models
{
    [TableName("AzureB2C_UserMappings")]
    //setup the primary key for table
    [PrimaryKey("UserMappingId", AutoIncrement = true)]
    //configure caching using PetaPoco
    [Cacheable("UserMapping", CacheItemPriority.Default, 20)]
    public class UserMapping
    {
        public int UserMappingId { get; set; }

        public string DnnPropertyName { get; set; }
        public string B2cClaimName { get; set; }
        public int PortalId { get; set; }
        public int CreatedByUserId { get; set; }
        public DateTime CreatedOnDate { get; set; }
        public int LastModifiedByUserId { get; set; }
        public DateTime LastModifiedOnDate { get; set; }

        public string GetB2cExtensionName(int portalId)
        {
            var settings = new AzureConfig(AzureConfig.ServiceName, portalId);
            return string.IsNullOrEmpty(settings.B2cApplicationId) || string.IsNullOrEmpty(B2cClaimName)
                ? ""
                : $"extension_{settings.B2cApplicationId.Replace("-", "")}_{B2cClaimName}";
        }
    }
}
