using DotNetNuke.Authentication.Azure.B2C.Components;
using DotNetNuke.Common;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Instrumentation;
using DotNetNuke.Web.Api;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace DotNetNuke.Authentication.Azure.B2C.Services
{
    public class AuthorizationController : DnnApiController
    {
        private static readonly ILog _logger = LoggerSource.Instance.GetLogger(typeof(AuthorizationController));

        [HttpGet]
        [AllowAnonymous]
        public HttpResponseMessage RedirectToPortal()
        {
            try
            {
                var stateKey = Request.GetQueryNameValuePairs()?.FirstOrDefault(x => x.Key == "state");
                if (stateKey == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "No state was specified");
                }
                var state = new State(stateKey.Value.Value.ToString());
                if (state.Service != AzureConfig.ServiceName)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "State not build for Azure AD B2C");
                }
                var portalId = state.PortalId;
                if (portalId < 0)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Not valid portal Id");
                }
                var portalAliases = PortalAliasController.Instance.GetPortalAliasesByPortalId(portalId).Where(x => x.IsPrimary);
                var portalAlias = portalAliases.FirstOrDefault();
                if (!string.IsNullOrEmpty(state.Culture) && portalAliases.Any(x => x.CultureCode == state.Culture))
                {
                    portalAlias = portalAliases.FirstOrDefault(x => x.CultureCode == state.Culture);
                }

                var portalSettings = new PortalSettings(portalId);
                portalSettings.PortalAlias = portalAlias;
                UriBuilder uriBuilder;
                if (state.IsUserProfile)
                {
                    uriBuilder = new UriBuilder($"{Request.RequestUri.Scheme}://{portalAlias.HTTPAlias}/UserProfile");
                }
                else if (state.IsImpersonate)
                {
                    uriBuilder = new UriBuilder($"{Request.RequestUri.Scheme}://{portalAlias.HTTPAlias}/Impersonate");
                }
                else
                {
                    uriBuilder = new UriBuilder(Globals.LoginURL(state.RedirectUrl, false, portalSettings));
                    if (!Request.RequestUri.IsDefaultPort)
                    {
                        uriBuilder.Port = Request.RequestUri.Port;
                    }
                }
                uriBuilder.Query = !string.IsNullOrEmpty(Request.RequestUri.Query) ? Request.RequestUri.Query.Substring(1) : "";

                var response = Request.CreateResponse(HttpStatusCode.Redirect);
                response.Headers.Location = uriBuilder.Uri;
                return response;
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }
    }
}
