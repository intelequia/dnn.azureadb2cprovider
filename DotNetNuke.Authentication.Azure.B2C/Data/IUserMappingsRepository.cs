using DotNetNuke.Authentication.Azure.B2C.Components.Models;
using System.Linq;

namespace DotNetNuke.Authentication.Azure.B2C.Data
{
    public interface IUserMappingsRepository
    {
        IQueryable<UserMapping> GetUserMappings(int portalId);
        UserMapping GetUserMapping(string dnnPropertyName, int portalId);
        void UpdateUserMapping(string dnnPropertyName, string b2cClaimName, int portalId);
        void InsertUserMapping(string dnnPropertyName, string b2cClaimName, int portalId);
    }
}
