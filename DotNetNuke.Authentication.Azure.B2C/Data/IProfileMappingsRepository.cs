using DotNetNuke.Authentication.Azure.B2C.Components.Models;
using System.Linq;

namespace DotNetNuke.Authentication.Azure.B2C.Data
{
    public interface IProfileMappingsRepository
    {
        IQueryable<ProfileMapping> GetProfileMappings(int portalId);
        ProfileMapping GetProfileMapping(string dnnProfilePropertyName, int portalId);
        void UpdateProfileMapping(string dnnProfilePropertyName, string b2cClaimName, int portalId);
        void InsertProfileMapping(string dnnProfilePropertyName, string b2cClaimName, int portalId);
        void DeleteProfileMapping(string dnnProfilePropertyName, int portalId);
    }
}
