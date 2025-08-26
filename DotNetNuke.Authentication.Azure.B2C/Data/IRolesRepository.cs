using DotNetNuke.Authentication.Azure.B2C.Components.Models;
using System.Collections.Generic;

namespace DotNetNuke.Authentication.Azure.B2C.Data
{
    public interface IRolesRepository
    {
        List<ExpiredUserRoleInfo> GetExpiredUserRoles(int portalId);
    }
}
