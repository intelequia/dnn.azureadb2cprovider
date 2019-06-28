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
        sf.post("UpdateGeneralSettings", payload, failureCallback, callback);
    }    

    updateAdvancedSettings(payload, callback, failureCallback) {
        const sf = this.getServiceFramework("AzureADB2C");        
        sf.post("UpdateAdvancedSettings", payload, failureCallback, callback);
    }      
}
const applicationService = new ApplicationService();
export default applicationService;