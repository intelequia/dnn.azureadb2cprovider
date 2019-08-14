using DotNetNuke.Authentication.Azure.B2C.Components;
using DotNetNuke.Authentication.Azure.B2C.Components.Graph;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Services.Scheduling;
using System;
using System.Linq;

namespace DotNetNuke.Authentication.Azure.B2C.ScheduledTasks
{
    public class SyncSchedule : SchedulerClient
    {
        private const string RoleMappingsFilePath = "~/DesktopModules/AuthenticationServices/AzureB2C/DnnRoleMappings.config";

        public SyncSchedule(ScheduleHistoryItem oItem) : base()
        {
            ScheduleHistoryItem = oItem;
        }

        private RoleMappings _customRoleMappings;
        public RoleMappings CustomRoleMappings
        {
            get
            {
                if (_customRoleMappings == null)
                {
                    
                    _customRoleMappings = RoleMappings.GetRoleMappings(System.Web.Hosting.HostingEnvironment.MapPath(RoleMappingsFilePath));
                }

                return _customRoleMappings;
            }
        }

        public override void DoWork()
        {
            try
            {
                //Perform required items for logging
                Progressing();

                ScheduleHistoryItem.AddLogNote("Starting Azure AD B2C Synchronization\n");

                var portals = PortalController.Instance.GetPortalList(Null.NullString);
                foreach (var portal in portals)
                {
                    var settings = new AzureConfig("AzureB2C", portal.PortalID);
                    if (settings.Enabled && settings.RoleSyncEnabled)
                    {
                        var message = SyncRoles(portal.PortalID, settings);
                        ScheduleHistoryItem.AddLogNote(message);
                    }
                }

                ScheduleHistoryItem.AddLogNote(string.Format("Azure AD B2C Synchronization finished successfully\n"));

                //Show success
                ScheduleHistoryItem.Succeeded = true;
            }
            catch (Exception ex)
            {
                ScheduleHistoryItem.Succeeded = false;
                ScheduleHistoryItem.AddLogNote(string.Format("Error performing Azure AD B2C Synchronization: {0}\n", ex.Message)); ;

                Errored(ref ex);
                DotNetNuke.Services.Exceptions.Exceptions.LogException(ex);
            }
        }

        /// <summary>
        /// For all dnn role to b2c group mappings configured in DnnRoleMappings.config, and that exist in AzureB2C tenant, insure that roles exist on dnn.
        /// </summary>
        /// <param name="portalId">PortalID of the dnn portal where roles are created.</param>
        /// <param name="settings">Configuration settings for module</param>
        /// <returns>Logging message(s) of job execution.</returns>
        internal string SyncRoles(int portalId, AzureConfig settings)
        {
            try
            {
                if (CustomRoleMappings == null || CustomRoleMappings.RoleMapping == null || CustomRoleMappings.RoleMapping.Length == 0)
                {
                    return "No role mappings configured.  See DnnRoleMappings.config in module root folder.";
                }
                else
                {
                    if (string.IsNullOrEmpty(settings.AADApplicationId) || string.IsNullOrEmpty(settings.AADApplicationKey))
                    {
                        throw new Exception($"AAD application ID or key are not valid on portal {portalId}");
                    }

                    var graphClient = new GraphClient(settings.AADApplicationId, settings.AADApplicationKey, settings.TenantId);


                    // Add roles from AAD B2C
                    var aadGroups = graphClient.GetAllGroups("");
                    if (aadGroups != null && aadGroups.Values != null)
                    {
                        foreach (var mapGroup in CustomRoleMappings.RoleMapping)
                        {
                            var dnnRole = Security.Roles.RoleController.Instance.GetRoleByName(portalId, mapGroup.DnnRoleName);
                            var aadGroup = aadGroups.Values.FirstOrDefault(s => s.DisplayName == mapGroup.B2cRoleName);

                            if (dnnRole == null && aadGroup != null)
                            {
                                // Create role
                                var roleId = Security.Roles.RoleController.Instance.AddRole(new Security.Roles.RoleInfo
                                {
                                    RoleName = mapGroup.B2cRoleName,
                                    Description = aadGroup.Description,
                                    PortalID = portalId,
                                    Status = Security.Roles.RoleStatus.Approved,
                                    RoleGroupID = -1,
                                    AutoAssignment = false,
                                    IsPublic = false
                                });
                            }
                        }
                    }
                }

                return $"Successfully synchronized portal {portalId}";
            }
            catch (Exception e)
            {
                return $"Error while synchronizing the roles from portal {portalId}: {e}";
            }
        }
    }
}
