using DotNetNuke.Authentication.Azure.B2C.Components;
using DotNetNuke.Authentication.Azure.B2C.Components.Graph;
using DotNetNuke.Common;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Security;
using DotNetNuke.Web.Api;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace DotNetNuke.Authentication.Azure.B2C.Services
{
    public class UserManagementController: DnnApiController
    {
        [HttpGet]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        public HttpResponseMessage GetAllUsers()
        {
            try
            {
                var settings = new AzureConfig("AzureB2C", PortalSettings.PortalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);

                var users = graphClient.GetAllUsers("");
                return Request.CreateResponse(HttpStatusCode.OK, users.Values);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        public class RemoveParameters
        {
            public string id { get; set; }
        }
        [HttpPost]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.View)]
        public HttpResponseMessage Remove(RemoveParameters parameters)
        {
            try
            {
                var settings = new AzureConfig("AzureB2C", PortalSettings.PortalId);
                var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);

                graphClient.DeleteUser(parameters.id);
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }
    }
}
