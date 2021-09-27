using System.Web;

namespace DotNetNuke.Authentication.Azure.B2C.Extensibility
{
    public interface ILoginValidation
    {
        void OnTokenReceived(string authToken, HttpContext context);
    }
}
