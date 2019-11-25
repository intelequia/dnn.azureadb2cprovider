using System;
using System.Web.Caching;
using DotNetNuke.ComponentModel.DataAnnotations;

namespace DotNetNuke.Authentication.Azure.B2C.Components.Models
{
    [TableName("AzureB2C_ProfileMappings")]
    //setup the primary key for table
    [PrimaryKey("ProfileMappingId", AutoIncrement = true)]
    //configure caching using PetaPoco
    [Cacheable("ProfileMapping", CacheItemPriority.Default, 20)]
    public class ProfileMapping
    {
        public int ProfileMappingId { get; set; }

        public string DnnProfilePropertyName { get; set; }
        public string B2cClaimName { get; set; }
        public int PortalId { get; set; }
        public int CreatedByUserId { get; set; }
        public DateTime CreatedOnDate { get; set; }
        public int LastModifiedByUserId { get; set; }
        public DateTime LastModifiedOnDate { get; set; }
    }
}
