Type.registerNamespace('dnn.adb2c.UserManagement');
dnn.extend(dnn.adb2c.UserManagement,
    {
        GroupModel: function (userManagement, model) {
            var that = this;

            if (!model)
                model = {};

            this.userManagement = userManagement;
            this.objectId = ko.observable(model.objectId || "");
            this.displayName = ko.observable(model.displayName || "");
            this.objectType = ko.observable(model.objectType || "");
            //this.odataType = ko.observable(model["odata.type"] || "");

            this.toSimple = function () {
                return {
                    objectId: that.objectId(),
                    displayName: that.displayName,
                    objectType: that.objectType
                };
            };

        },
        UserModel: function (userManagement, model) {
            var that = this;

            if (!model)
                model = {};

            this.userManagement = userManagement;

            this.accountEnabled = ko.observable(model.accountEnabled || true);
            this.displayName = ko.observable(model.displayName || "");
            this.givenName = ko.observable(model.givenName || "");
            this.mail = ko.observable(model.mail
                || (model.otherMails && model.otherMails.length > 0 ? model.otherMails[0] : "")
                || (model.signInNames && model.signInNames.length > 0 && model.signInNames[0].value ? model.signInNames[0].value : ""));
            this.mailNickname = ko.observable(model.mailNickname || "");
            this.otherMails = ko.observableArray(model.otherMails || []);
            this.proxyAddresses = ko.observableArray(model.proxyAddresses || []);
            this.showInAddressList = ko.observable(model.showInAddressList || false);
            this.surname = ko.observable(model.surname || "");
            this.userPrincipalName = ko.observable(model.userPrincipalName || "");
            this.userType = ko.observable(model.userType || "");
            this.objectId = ko.observable(model.objectId || "");
            //this.odataType = ko.observable(model.@odata.type || "");
            this.assignedLicenses = ko.observable(model.assignedLicenses || null);
            this.assignedPlans = ko.observable(model.assignedPlans || null);
            this.objectType = ko.observable(model.objectType || "");
            this.deletionTimestamp = ko.observable(model.deletionTimestamp || null);
            this.ageGroup = ko.observable(model.ageGroup || null);
            this.city = ko.observable(model.city || null);
            this.companyName = ko.observable(model.companyName || null);
            this.consentProvidedForMinor = ko.observable(model.consentProvidedForMinor || null);
            this.country = ko.observable(model.country || null);
            this.createdDateTime = ko.observable(model.createdDateTime || null);
            this.creationType = ko.observable(model.creationType || null);
            this.department = ko.observable(model.department || null);
            this.dirSyncEnabled = ko.observable(model.dirSyncEnabled || null);
            this.employeeId = ko.observable(model.employeeId || null);
            this.facsimileTelephoneNumber = ko.observable(model.facsimileTelephoneNumber || null);
            this.immutableId = ko.observable(model.immutableId || null);
            this.isCompromised = ko.observable(model.isCompromised || null);
            this.jobTitle = ko.observable(model.jobTitle || null);
            this.lastDirSyncTime = ko.observable(model.lastDirSyncTime || null);
            this.legalAgeGroupClassification = ko.observable(model.legalAgeGroupClassification || null);
            this.mobile = ko.observable(model.mobile || null);
            this.onPremisesDistinguishedName = ko.observable(model.onPremisesDistinguishedName || null);
            this.onPremisesSecurityIdentifier = ko.observable(model.onPremisesSecurityIdentifier || null);
            this.passwordPolicies = ko.observable(model.passwordPolicies || null);
            this.passwordProfile = ko.observable(model.passwordProfile || null);
            this.physicalDeliveryOfficeName = ko.observable(model.physicalDeliveryOfficeName || null);
            this.postalCode = ko.observable(model.postalCode || null);            
            this.preferredLanguage = ko.observable(model.preferredLanguage || "en");
            this.provisionedPlans = ko.observable(model.provisionedPlans || null);
            this.provisionedErrors = ko.observable(model.provisionedErrors || null);
            this.refreshTokensValidFromDateTime = ko.observable(model.refreshTokensValidFromDateTime || null);
            this.signInNames = ko.observable(model.signInNames || null);
            this.sipProxyAddress = ko.observable(model.sipProxyAddress || null);
            this.telephoneNumber = ko.observable(model.telephoneNumber || null);
            this.thumbnailPhoto = ko.observable(model.thumbnailPhoto || null);
            this.usageLocation = ko.observable(model.usageLocation || null);
            this.userIdentities = ko.observable(model.userIdentities || null);
            this.userState = ko.observable(model.userState || null);
            this.userStateChangedOn = ko.observable(model.userStateChangedOn || null);

            this.passwordType = ko.observable("auto");
            this.password = ko.observable("");
            this.sendEmail = ko.observable(true);

            // User custom attributes
            if (userManagement.customAttributes && userManagement.customAttributes !== "") {
                $.each(userManagement.customAttributes.split(","),
                    function (index, customAttribute) {
                        var p = userManagement.customAttributesPrefix + customAttribute.replace(" ", "");
                        that[customAttribute.replace(" ", "")] = ko.observable(model[p] || "");
                    });
            }
            

            this.groups = ko.observableArray();
            this.groupsSimple = function () {
                var g = [];
                $.each(that.groups(),
                    function (index, group) {
                        g.push(group.toSimple());
                    });
                return g;
            };

            this.usernameToDisplay = ko.computed(function () {
                var sub = "";
                if (that.userPrincipalName().includes("#EXT#")) { 
                    sub = " (External)";
                }
                else if (that.userPrincipalName().startsWith("cpim_")) {
                    sub = " (Federated)";
                }
                if (that.signInNames() && that.signInNames().length > 0 && that.signInNames()[0].type === "emailAddress") {
                    return that.signInNames()[0].value + sub;
                }
                if (that.mail() !== "") {
                    return that.mail() + sub;
                }
                return that.userPrincipalName() + sub;
            }, this);

            this.setDisplayName = function () {
                if (that.displayName() === "")
                    that.displayName(that.givenName() + " " + that.surname());
            }; 
            this.identityIssuer = ko.computed(function () {
                if (that.userIdentities() && that.userIdentities().length > 0) {
                    return that.userIdentities()[0].issuer.replace('.', '');
                }                    
                return "";
            });

            this.addGroup = function () {
                if (that.userManagement.selectedGroup() && (that.groups().length === 0 || !that.groups().find(function (data) {
                        return data.objectId() === that.userManagement.selectedGroup().objectId();
                    }))) {
                    var group = new dnn.adb2c.UserManagement.GroupModel(that);
                    group.displayName(that.userManagement.selectedGroup().displayName());
                    group.objectId(that.userManagement.selectedGroup().objectId());
                    that.groups.push(group);
                }
            };

            this.removeGroup = function (g,t) {
                that.groups.remove(function (group) {
                    return group.objectId() === t.target.attributes["data-oid"].value;
                }); 
            };

            this.addUser = function () {
                that.userManagement.loading(true);
                that.userManagement.ajax("AddUser", {
                    user: {
                        displayName: that.displayName(),
                        givenName: that.givenName(),
                        surname: that.surname(),
                        mail: that.mail(),
                        preferredLanguage: that.preferredLanguage()
                    },
                    passwordType: that.passwordType(),
                    password: that.password(),
                    sendEmail: that.sendEmail(),
                    groups: that.groupsSimple()
                },
                    function (data) {
                        that.userManagement.users.push(new dnn.adb2c.UserManagement.UserModel(that.userManagement, data));
                        that.userManagement.hideTab();
                        that.userManagement.loading(false);
                    },
                    function (e) {
                        that.userManagement.loading(false);
                    }, null, "POST"
                );
            };

            this.forceChangePassword = function () {
                that.userManagement.loading(true);
                that.userManagement.ajax("ForceChangePassword", {
                    user: {
                        objectId: that.objectId()
                    }
                },
                    function (data) {
                        that.userManagement.loading(false);
                        toastr.success("Force change password success. The user will need to change the password on next login.");
                    },
                    function (e) {
                        that.userManagement.loading(false);
                    }, null, "POST"
                );
            };

            this.update = function () {
                that.userManagement.loading(true);

                var u = {
                    objectId: that.objectId(),
                    displayName: that.displayName(),
                    givenName: that.givenName(),
                    surname: that.surname(),
                    mail: that.mail(),
                    preferredLanguage: that.preferredLanguage()
                };

                if (userManagement.customAttributes && userManagement.customAttributes !== "") {
                    $.each(userManagement.customAttributes.split(","),
                        function (index, customAttribute) {
                            var p = userManagement.customAttributesPrefix + customAttribute.replace(" ", "");
                            u[p] = that[customAttribute.replace(" ", "")]();
                        });
                }
                that.userManagement.ajax("UpdateUser", {
                    user: u,
                    groups: that.groupsSimple()
                    },
                    function (data) {
                        that.userManagement.hideTab();
                        that.userManagement.loading(false);
                    },
                    function (e) {
                        that.userManagement.loading(false);
                    }, null, "POST"
                );
            };

            this.changePassword = function () {
                that.userManagement.loading(true);
                that.userManagement.ajax("ChangePassword", {
                    user: {
                        objectId: that.objectId()
                    },
                    passwordType: that.passwordType(),
                    password: that.password(),
                    sendEmail: that.sendEmail()
                },
                    function (data) {
                        that.userManagement.hideTab();
                        that.userManagement.loading(false);
                    },
                    function (e) {
                        that.userManagement.loading(false);
                    }, null, "POST"
                );
            };


            this.remove = function () {
                swal({
                    title: dnn.getVar("AreYouSure"),
                    text: dnn.getVar("DeleteMessage"),
                    type: "warning",
                    showCancelButton: true,
                    confirmButtonColor: "#004e97",
                    confirmButtonText: dnn.getVar("Yes"),
                    cancelButtonText: dnn.getVar("Cancel"),
                    closeOnConfirm: true
                }, function () {
                    that.userManagement.loading(true);
                    that.userManagement.ajax("Remove", {
                        objectId: that.objectId()
                    },
                        function (data) {
                            that.userManagement.refresh();
                        },
                        function (e) {
                            that.userManagement.loading(false);
                        }, null, "POST"
                    );
                });
            };
        },
        UserManagementModel: function () {
            var that = this;
            var sf = $.dnnSF(dnn.getVar("moduleId"));

            this.users = ko.observableArray();
            this.groups = ko.observableArray();
            this.selectedGroup = ko.observable('');
            this.newUser = ko.observable();
            this.selectedUser = ko.observable();
            this.searchText = ko.observable('');
            this.searchTimeout = null;
            this.loading = ko.observable(true);
            this.changingPassword = ko.observable(false);
            this.customAttributes = dnn.getVar("customAttributes");
            this.customAttributesPrefix = dnn.getVar("customAttributesPrefix");


            function setHeaders(xhr) {
                sf.setModuleHeaders(xhr);
            }

            function ajax(webapimethod, parameters, success, failure, complete, method) {
                var url = sf.getServiceRoot('DotNetNuke.Authentication.Azure.B2C.Services/UserManagement') + webapimethod;
                method = method || "GET";
                $.ajax({
                    url: url,
                    beforeSend: setHeaders,
                    cache: false,
                    contentType: 'application/json; charset=UTF-8',
                    type: method,
                    data: parameters ? ko.toJSON(parameters) : null,
                    success: success,
                    error: function (e) {
                        toastr.error(e.responseJSON);
                        if (failure)
                            failure();
                    },
                    complete: complete
                });
            }

            this.ajax = ajax;

            this.downloadUsers = function () {
                swal({
                    title: dnn.getVar("AreYouSure"),
                    text: dnn.getVar("ExportMessage"),
                    type: "warning",
                    showCancelButton: true,
                    confirmButtonColor: "#004e97",
                    confirmButtonText: dnn.getVar("YesDownload"),
                    cancelButtonText: dnn.getVar("Cancel"),
                    closeOnConfirm: true
                }, function () {
                    that.loading(true);
                        that.ajax("Export?search=" + that.searchText(), null,
                        function (data) {
                            if (data.downloadUrl) {
                                window.location.replace(data.downloadUrl);
                            }
                            else {
                                toastr.error("Could not get the export Url. Contact your site administrator.");
                                that.loading(false);
                            }
                        },
                        function (e) {
                            that.loading(false);
                        }, null, "GET"
                    );
                });
            };

            this.impersonate = function () {
                that.loading(true);
                ajax("Impersonate",
                    {
                        returnUri: window.location.href
                    },
                    function (data) {
                        if (data.impersonateUrl) {
                            window.location.replace(data.impersonateUrl);
                        }
                        else {
                            toastr.error("Could not get the impersonation Url. Contact your site administrator.");
                            that.loading(false);
                        }
                    },
                    function (e) {
                        that.loading(false);
                    },null, "POST"
                );
            };

            this.refresh = function () {
                that.loading(true);
                ajax("GetAllGroups", null,
                    function (data) {
                        that.groups.removeAll();
                        $.each(data,
                            function (index, group) {
                                that.groups.push(new dnn.adb2c.UserManagement.GroupModel(that, group));
                            });
                        ajax("GetAllUsers?search=" + that.searchText(), null,
                            function (data) {
                                that.users.removeAll();
                                $.each(data,
                                    function (index, user) {
                                        that.users.push(new dnn.adb2c.UserManagement.UserModel(that, user));
                                    });
                                that.loading(false);
                            },
                            function (e) {
                                that.loading(false);
                            }
                        );
                    },
                    function (e) {
                        that.loading(false);
                    }
                );
            };

            this.search = function () {
                if (that.searchTimeout)
                    clearTimeout(that.searchTimeout);
                that.searchTimeout = setTimeout(function () {
                    that.searchTimeout = null;
                    ajax("GetAllUsers?search=" + that.searchText(), null,
                        function (data) {
                            that.users.removeAll();
                            $.each(data,
                                function (index, user) {
                                    that.users.push(new dnn.adb2c.UserManagement.UserModel(that, user));
                                });
                            that.loading(false);
                        },
                        function (e) {
                            that.loading(false);
                        }
                    );
                }, 500);
            };
            this.showTab = function() {
                $(".b2c-overlay").css("display", "block");
                $(".b2c-rightTab").addClass("b2c-rightTab-show");
            };
            this.hideTab = function () {
                $(".b2c-rightTab").removeClass("b2c-rightTab-show");
                $(".b2c-overlay").css("display", "none");
                that.newUser(null);
                that.selectedUser(null);
            };
            this.addUser = function (evt) {
                that.selectedUser(null);
                that.newUser(new dnn.adb2c.UserManagement.UserModel(that));
                that.showTab();                
            };
            this.updateUser = function (evt) {
                that.newUser(null);
                that.changingPassword(false);
                ajax("GetUserGroups?objectId=" + evt.objectId(), null,
                    function (data) {
                        evt.groups.removeAll();
                        $.each(data,
                            function (index, group) {
                                evt.groups.push(new dnn.adb2c.UserManagement.GroupModel(that, group));
                            });
                        that.selectedGroup('');
                        that.selectedUser(evt);
                        evt.passwordType("auto");
                        evt.password("");
                        evt.sendEmail(true);
                        that.showTab();
                        that.loading(false);
                    },
                    function (e) {
                        that.loading(false);
                    }
                );
            };

            this.changePassword = function () {
                that.changingPassword(true);
            };

            this.refresh();
        }
    });