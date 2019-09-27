import { settings as ActionTypes } from "../constants/actionTypes";
import ApplicationService from "../services/applicationService";

const settingsActions = {
    switchTab(index, callback) {
        return (dispatch) => {
            dispatch({
                type: ActionTypes.SWITCH_TAB,
                payload: index
            });
            if (callback) {
                callback();
            }
        };
    },    
    getSettings(callback) {
        return (dispatch) => {
            ApplicationService.getSettings(data => {
                dispatch({
                    type: ActionTypes.RETRIEVED_SETTINGS,
                    data: {
                        enabled: data.enabled,
                        useGlobalSettings: data.useGlobalSettings,
                        autoRedirect: data.autoRedirect,
                        apiKey: data.apiKey,
                        apiSecret: data.apiSecret,
                        redirectUri: data.redirectUri,
                        tenantName: data.tenantName,
                        tenantId: data.tenantId,
                        signUpPolicy: data.signUpPolicy,
                        profilePolicy: data.profilePolicy,
                        passwordResetPolicy: data.passwordResetPolicy,
                        aadAppClientId: data.aadAppClientId,                        
                        aadAppSecret: data.aadAppSecret,
                        jwtAudiences: data.jwtAudiences,
                        roleSyncEnabled: data.roleSyncEnabled,
                        profileSyncEnabled: data.profileSyncEnabled,
                        jwtAuthEnabled: data.jwtAuthEnabled,
                        apiResource: data.apiResource,
                        scopes: data.scopes,
                        clientModified: false
                    }
                });
                if (callback) {
                    callback(data);
                }
            });
        };
    },
    updateGeneralSettings(payload, callback, failureCallback) {
        return (dispatch) => {
            ApplicationService.updateGeneralSettings(payload, data => {
                dispatch({
                    type: ActionTypes.UPDATED_SETTINGS,
                    data: {
                        clientModified: false
                    }
                });
                if (callback) {
                    callback(data);
                }
            }, data => {
                if (failureCallback) {
                    failureCallback(data);
                }
            });
        };
    },
    updateAdvancedSettings(payload, callback, failureCallback) {
        return (dispatch) => {
            ApplicationService.updateAdvancedSettings(payload, data => {
                dispatch({
                    type: ActionTypes.UPDATED_SETTINGS,
                    data: {
                        clientModified: false
                    }
                });
                if (callback) {
                    callback(data);
                }
            }, data => {
                if (failureCallback) {
                    failureCallback(data);
                }
            });
        };
    },    
    settingsClientModified(settings) {
        return (dispatch) => {
            dispatch({
                type: ActionTypes.SETTINGS_CLIENT_MODIFIED,
                data: {
                    enabled: settings.enabled,
                    useGlobalSettings: settings.useGlobalSettings,
                    autoRedirect: settings.autoRedirect,
                    apiKey: settings.apiKey,
                    apiSecret: settings.apiSecret,
                    redirectUri: settings.redirectUri,
                    tenantName: settings.tenantName,
                    tenantId: settings.tenantId,
                    signUpPolicy: settings.signUpPolicy,
                    profilePolicy: settings.profilePolicy,
                    passwordResetPolicy: settings.passwordResetPolicy,
                    aadAppClientId: settings.aadAppClientId,
                    aadAppSecret: settings.aadAppSecret,
                    jwtAudiences: settings.jwtAudiences,
                    roleSyncEnabled: settings.roleSyncEnabled,
                    profileSyncEnabled: settings.profileSyncEnabled,
                    jwtAuthEnabled: settings.jwtAuthEnabled,
                    apiResource: settings.apiResource,
                    scopes: settings.scopes,
                    clientModified: true
                }
            });
        };
    }
};

export default settingsActions;