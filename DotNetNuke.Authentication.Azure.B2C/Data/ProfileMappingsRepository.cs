using DotNetNuke.Authentication.Azure.B2C.Components.Models;
using DotNetNuke.Common;
using DotNetNuke.Data;
using DotNetNuke.Entities.Users;
using DotNetNuke.Framework;
using System;
using System.Linq;

namespace DotNetNuke.Authentication.Azure.B2C.Data
{
    public class ProfileMappingsRepository : ServiceLocator<IProfileMappingsRepository, ProfileMappingsRepository>, IProfileMappingsRepository
    {
        protected override Func<IProfileMappingsRepository> GetFactory()
        {
            return () => new ProfileMappingsRepository();
        }

        public IQueryable<ProfileMapping> GetProfileMappings(int portalId)
        {
            IQueryable<ProfileMapping> result = null;

            using (IDataContext ctx = DataContext.Instance())
            {
                var rep = ctx.GetRepository<ProfileMapping>();
                result = rep.Find("WHERE PortalId = @0", portalId).AsQueryable();
            }

            return result;
        }

        public ProfileMapping GetProfileMapping(string dnnProfilePropertyName, int portalId)
        {
            Requires.NotNullOrEmpty("DnnProfilePropertyName", dnnProfilePropertyName);

            ProfileMapping result;
            using (IDataContext ctx = DataContext.Instance())
            {
                var rep = ctx.GetRepository<ProfileMapping>();
                result = rep.Find("WHERE DnnProfilePropertyName = @0 AND PortalId = @1", dnnProfilePropertyName, portalId).FirstOrDefault();
            }
            return result;
        }

        public void UpdateProfileMapping(string dnnProfilePropertyName, string b2cClaimName, int portalId)
        {
            Requires.NotNullOrEmpty("DnnProfilePropertyName", dnnProfilePropertyName);
            Requires.NotNullOrEmpty("B2cClaimName", b2cClaimName);

            var profileMapping = GetProfileMapping(dnnProfilePropertyName, portalId);
            if (profileMapping == null)
            {
                throw new ArgumentException($"Profile mapping '{dnnProfilePropertyName}' not found in portal {portalId}");
            }

            profileMapping.B2cClaimName = b2cClaimName;
            profileMapping.PortalId = portalId;
            profileMapping.LastModifiedOnDate = DateTime.UtcNow;
            profileMapping.LastModifiedByUserId = UserController.Instance.GetCurrentUserInfo().UserID;
            using (IDataContext ctx = DataContext.Instance())
            {
                var rep = ctx.GetRepository<ProfileMapping>();
                rep.Update(profileMapping);
            }
        }

        public void InsertProfileMapping(string dnnProfilePropertyName, string b2cClaimName, int portalId)
        {
            Requires.NotNullOrEmpty("DnnProfilePropertyName", dnnProfilePropertyName);
            Requires.NotNullOrEmpty("B2cClaimName", b2cClaimName);

            var profileMapping = new ProfileMapping
            {
                DnnProfilePropertyName = dnnProfilePropertyName,
                CreatedByUserId = UserController.Instance.GetCurrentUserInfo().UserID,
                CreatedOnDate = DateTime.UtcNow,
                B2cClaimName = b2cClaimName,
                PortalId = portalId,
                LastModifiedOnDate = DateTime.UtcNow,
                LastModifiedByUserId = UserController.Instance.GetCurrentUserInfo().UserID
            };
            using (IDataContext ctx = DataContext.Instance())
            {
                var rep = ctx.GetRepository<ProfileMapping>();
                rep.Insert(profileMapping);
            }
        }

        public void DeleteProfileMapping(string dnnProfilePropertyName, int portalId)
        {
            Requires.NotNullOrEmpty("DnnProfilePropertyname", dnnProfilePropertyName);

            var profileMapping = GetProfileMapping(dnnProfilePropertyName, portalId);
            if (profileMapping != null)
            {
                using (IDataContext ctx = DataContext.Instance())
                {
                    var rep = ctx.GetRepository<ProfileMapping>();
                    rep.Delete(profileMapping);
                }
            }
        }
    }
}
