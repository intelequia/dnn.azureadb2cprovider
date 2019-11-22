import util from "../utils";
class ApplicationService {
    getServiceFramework(controller) {
        let sf = util.utilities.sf;
        sf.controller = controller;
        return sf;
    }

    getSettings(callback) {
        const sf = this.getServiceFramework("AzureADB2C");        
        sf.get("GetSettings", {}, callback);
    }

    updateGeneralSettings(payload, callback, failureCallback) {
        const sf = this.getServiceFramework("AzureADB2C");        
        sf.post("UpdateGeneralSettings", payload, callback, failureCallback);
    }    

    updateAdvancedSettings(payload, callback, failureCallback) {
        const sf = this.getServiceFramework("AzureADB2C");        
        sf.post("UpdateAdvancedSettings", payload, callback, failureCallback);
    }

    updateProfileMapping(payload, callback, failureCallback) {
        const sf = this.getServiceFramework("AzureADB2C");        
        sf.post("UpdateProfileMapping", payload, callback, failureCallback);
    }

    deleteProfileMapping(payload, callback, failureCallback) {
        const sf = this.getServiceFramework("AzureADB2C");        
        sf.post("DeleteProfileMapping", payload, callback, failureCallback);
    }
    getProfileSettings(callback) {
        const sf = this.getServiceFramework("AzureADB2C");        
        sf.get("GetProfileSettings", {}, callback);
    }

    getProfileProperties(callback) {
        const sf = this.getServiceFramework("AzureADB2C");        
        sf.get("GetProfileProperties", {}, callback);
    }

    getRoleMappingSettings(callback) {
        const sf = this.getServiceFramework("AzureADB2C");        
        sf.get("GetRoleMappingSettings", {}, callback);
    }

    getAvailableRoles(callback) {
        const sf = this.getServiceFramework("AzureADB2C");        
        sf.get("GetAvailableRoles", {}, callback);
    }

    updateRoleMapping(payload, callback, failureCallback) {
        const sf = this.getServiceFramework("AzureADB2C");        
        sf.post("UpdateRoleMapping", payload, callback, failureCallback);
    }

    deleteRoleMapping(payload, callback, failureCallback) {
        const sf = this.getServiceFramework("AzureADB2C");        
        sf.post("DeleteRoleMapping", payload, callback, failureCallback);
    }

    getUserMappingSettings(callback) {
        const sf = this.getServiceFramework("AzureADB2C");        
        sf.get("GetUserMappingSettings", {}, callback);
    }

    updateUserMapping(payload, callback, failureCallback) {
        const sf = this.getServiceFramework("AzureADB2C");        
        sf.post("UpdateUserMapping", payload, callback, failureCallback);
    }
}
const applicationService = new ApplicationService();
export default applicationService;