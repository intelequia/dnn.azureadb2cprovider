'use strict';
define(['jquery',
    'main/config',
    'main/loader'
],
    function ($, cf, loader) {
        var initCallback;
        var utility;
        var settings;
        var config = cf.init();

        function loadScript(basePath) {
            var normalizedCulture = config.culture.split("-")[0];
            var language = getBundleLanguage(normalizedCulture);
            var url = basePath + "/bundle-" + language + ".js";
            $.ajax({
                dataType: "script",
                cache: true,
                url: url
            });
        }

        function getBundleLanguage(culture) {
            var fallbackLanguage = "en";
            var availableLanguages = ["en"];
            return availableLanguages.indexOf(culture) > 0 ? culture : fallbackLanguage;
        }


        return {
            init: function (wrapper, util, params, callback) {
                initCallback = callback;
                utility = util;
                settings = params.settings;

                if (!settings) {
                    throw new Error("Azure AD B2C settings are not defined in persona bar customSettings");
                }

                var publicPath = settings.uiUrl + "/scripts/bundles/";
                window.dnn.initAzureADB2C = function initializeAzureADB2C() {
                    return {
                        publicPath: publicPath,
                        apiServiceUrl: settings.apiUrl,
                        libraryVersion: settings.libraryVersion,
                        loader: loader,
                        utility: util,
                        moduleName: 'AzureADB2C',
                        notifier: {
                            confirm: util.confirm,
                            notify: util.notify,
                            notifyError: util.notifyError
                        }
                    };
                }
                loadScript(publicPath);

                if (typeof callback === "function") {
                    callback();
                }
            },

            load: function (params, callback) {
                var azureADB2C = window.dnn.azureADB2C;
                if (azureADB2C && azureADB2C.load) {
                    azureADB2C.load();
                }

                if (typeof callback === "function") {
                    callback();
                }
            }
        };
    });
