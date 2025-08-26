using DotNetNuke.Authentication.Azure.B2C.Components.Models;
using DotNetNuke.Framework;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace DotNetNuke.Authentication.Azure.B2C.Data
{
    public class RolesRepository : ServiceLocator<IRolesRepository, RolesRepository>, IRolesRepository
    {
        private static readonly string connectionString = ConfigurationManager.ConnectionStrings["SiteSqlServer"].ConnectionString;

        protected override Func<IRolesRepository> GetFactory()
        {
            return () => new RolesRepository();
        }

        public List<ExpiredUserRoleInfo> GetExpiredUserRoles(int portalId)
        {
            List<ExpiredUserRoleInfo> expiredRoles = new List<ExpiredUserRoleInfo>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand command = new SqlCommand("AzureB2C_GetExpiredUserRoles", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add(new SqlParameter("@PortalId", portalId));

                    SqlDataReader reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        ExpiredUserRoleInfo roleInfo = new ExpiredUserRoleInfo
                        {
                            UserID = reader.GetInt32(reader.GetOrdinal("UserID")),
                            Email = reader.GetString(reader.GetOrdinal("Email")),
                            RoleID = reader.GetInt32(reader.GetOrdinal("RoleID")),
                            RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
                            ExpiryDate = reader.GetDateTime(reader.GetOrdinal("ExpiryDate")).ToString("yyyy-MM-dd")
                        };

                        expiredRoles.Add(roleInfo);
                    }
                }

                connection.Close();
            }

            return expiredRoles;
        }
    }

}
