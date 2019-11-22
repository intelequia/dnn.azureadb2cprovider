using DotNetNuke.Authentication.Azure.B2C.Components.Models;
using DotNetNuke.Common;
using DotNetNuke.Data;
using DotNetNuke.Entities.Users;
using DotNetNuke.Framework;
using System;
using System.Linq;

namespace DotNetNuke.Authentication.Azure.B2C.Data
{
    public class UserMappingsRepository : ServiceLocator<IUserMappingsRepository, UserMappingsRepository>, IUserMappingsRepository
    {
        protected override Func<IUserMappingsRepository> GetFactory()
        {
            return () => new UserMappingsRepository();
        }

        public IQueryable<UserMapping> GetUserMappings(int portalId)
        {
            IQueryable<UserMapping> result = null;

            using (IDataContext ctx = DataContext.Instance())
            {
                var rep = ctx.GetRepository<UserMapping>();
                result = rep.Find("WHERE PortalId = @0", portalId).AsQueryable();
            }

            return result;
        }

        public UserMapping GetUserMapping(string dnnPropertyName, int portalId)
        {
            Requires.NotNullOrEmpty("DnnPropertyName", dnnPropertyName);

            UserMapping result;
            using (IDataContext ctx = DataContext.Instance())
            {
                var rep = ctx.GetRepository<UserMapping>();
                result = rep.Find("WHERE DnnPropertyName = @0 AND PortalId = @1", dnnPropertyName, portalId).FirstOrDefault();
            }
            return result;
        }

        public void UpdateUserMapping(string dnnPropertyName, string b2cClaimName, int portalId)
        {
            Requires.NotNullOrEmpty("DnnPropertyName", dnnPropertyName);
            Requires.NotNullOrEmpty("B2cClaimName", b2cClaimName);

            var userMapping = GetUserMapping(dnnPropertyName, portalId);
            if (userMapping == null)
            {
                throw new ArgumentException($"User mapping '{dnnPropertyName}' not found in portal {portalId}");
            }

            userMapping.B2cClaimName = b2cClaimName;
            userMapping.PortalId = portalId;
            userMapping.LastModifiedOnDate = DateTime.UtcNow;
            userMapping.LastModifiedByUserId = UserController.Instance.GetCurrentUserInfo().UserID;
            using (IDataContext ctx = DataContext.Instance())
            {
                var rep = ctx.GetRepository<UserMapping>();
                rep.Update(userMapping);
            }
        }

        public void InsertUserMapping(string dnnPropertyName, string b2cClaimName, int portalId)
        {
            Requires.NotNullOrEmpty("DnnPropertyName", dnnPropertyName);
            Requires.NotNullOrEmpty("B2cClaimName", b2cClaimName);

            var userMapping = new UserMapping
            {
                DnnPropertyName = dnnPropertyName,
                CreatedByUserId = UserController.Instance.GetCurrentUserInfo().UserID,
                CreatedOnDate = DateTime.UtcNow,
                B2cClaimName = b2cClaimName,
                PortalId = portalId,
                LastModifiedOnDate = DateTime.UtcNow,
                LastModifiedByUserId = UserController.Instance.GetCurrentUserInfo().UserID
            };
            using (IDataContext ctx = DataContext.Instance())
            {
                var rep = ctx.GetRepository<UserMapping>();
                rep.Insert(userMapping);
            }

        }
    }
}
