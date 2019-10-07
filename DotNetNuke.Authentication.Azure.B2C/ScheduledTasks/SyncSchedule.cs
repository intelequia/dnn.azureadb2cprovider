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
        private RoleMappings _customRoleMappings;
        public RoleMappings CustomRoleMappings
        {
            get
            {
                if (_customRoleMappings == null)
                {
                    _customRoleMappings = RoleMappings.GetRoleMappings(System.Web.Hosting.HostingEnvironment.MapPath(RoleMappings.DefaultRoleMappingsFilePath));
                }
                return _customRoleMappings;
            }
        }
        public SyncSchedule(ScheduleHistoryItem oItem) : base()
        {
            ScheduleHistoryItem = oItem;
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

        internal string SyncRoles(int portalId, AzureConfig settings)
        {
            try
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
                    var groupPrefix = "AzureB2C-";
                    var groups = aadGroups.Values;
                    if (CustomRoleMappings.RoleMapping.Length > 0) {
                        groupPrefix = "";
                        var b2cRoles = CustomRoleMappings.RoleMapping.Select(rm => rm.B2cRoleName);
                        groups.RemoveAll(x => b2cRoles.Contains(x.DisplayName));
                    }

                    foreach (var aadGroup in groups)
                    {                        
                        var dnnRole = Security.Roles.RoleController.Instance.GetRoleByName(portalId, $"{groupPrefix}{aadGroup.DisplayName}");
                        if (dnnRole == null)
                        {
                            // Create role
                            var roleId = Security.Roles.RoleController.Instance.AddRole(new Security.Roles.RoleInfo
                            {
                                RoleName = $"{groupPrefix}{aadGroup.DisplayName}",
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

                // Remove roles no longer exists on AAD B2C (but only if no role mappings are configured)
                if (CustomRoleMappings.RoleMapping.Length == 0)
                {
                    var dnnRoles = Security.Roles.RoleController.Instance.GetRoles(portalId, x => x.RoleName.StartsWith($"AzureB2C-"));
                    foreach (var dnnRole in dnnRoles)
                    {
                        if (aadGroups == null
                            || aadGroups.Values == null
                            || aadGroups.Values.FirstOrDefault(x => x.DisplayName == dnnRole.RoleName.Substring($"AzureB2C-".Length)) == null)
                        {
                            Security.Roles.RoleController.Instance.DeleteRole(dnnRole);
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
