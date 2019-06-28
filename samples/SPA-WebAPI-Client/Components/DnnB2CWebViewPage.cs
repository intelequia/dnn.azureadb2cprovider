using System.Linq;

namespace Dnn.Modules.B2CTasksSPA_WebAPI_Client.Components
{
    public class DnnB2CWebViewPage : DotNetNuke.Web.Mvc.Framework.DnnWebViewPage
    {
        public string AuthToken {
            get
            {
                var token = Request?.Cookies["AzureB2CUserToken"]?.Value;
                if (token != null && token.Contains("oauth_token="))
                {
                    token = token.Split('&').FirstOrDefault(x => x.Contains("oauth_token="))?.Substring("oauth_token=".Length);
                }
                return token;
            }
        }
        public override void Execute()
        {            
            base.ExecutePageHierarchy();
        }
    }
}