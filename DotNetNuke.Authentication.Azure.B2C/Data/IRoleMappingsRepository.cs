using DotNetNuke.Authentication.Azure.B2C.Components.Models;
using System.Linq;

namespace DotNetNuke.Authentication.Azure.B2C.Data
{
    public interface IRoleMappingsRepository
    {
        IQueryable<RoleMapping> GetRoleMappings(int portalId);
        RoleMapping GetRoleMapping(string dnnRoleName, int portalId);
        void UpdateRoleMapping(string dnnRoleName, string b2cRoleName, int portalId);
        void InsertRoleMapping(string dnnRoleName, string b2cRoleName, int portalId);
        void DeleteRoleMapping(string dnnRoleName, int portalId);
    }
}
