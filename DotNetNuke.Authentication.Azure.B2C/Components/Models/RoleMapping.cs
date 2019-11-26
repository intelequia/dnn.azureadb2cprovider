using System;
using System.Web.Caching;
using DotNetNuke.ComponentModel.DataAnnotations;

namespace DotNetNuke.Authentication.Azure.B2C.Components.Models
{
    [TableName("AzureB2C_RoleMappings")]
    //setup the primary key for table
    [PrimaryKey("RoleMappingId", AutoIncrement = true)]
    //configure caching using PetaPoco
    [Cacheable("RoleMapping", CacheItemPriority.Default, 20)]
    public class RoleMapping
    {
        public int RoleMappingId { get; set; }

        public string DnnRoleName { get; set; }
        public string B2cRoleName { get; set; }
        public int PortalId { get; set; }
        public int CreatedByUserId { get; set; }
        public DateTime CreatedOnDate { get; set; }
        public int LastModifiedByUserId { get; set; }
        public DateTime LastModifiedOnDate { get; set; }
    }
}
