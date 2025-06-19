namespace DotNetNuke.Authentication.Azure.B2C.Components.Models
{
    public class ExpiredUserRoleInfo
    {
        public int UserID { get; set; }
        public string Email { get; set; }
        public int RoleID { get; set; }
        public string RoleName { get; set; }
        public string ExpiryDate { get; set; }
    }
}
