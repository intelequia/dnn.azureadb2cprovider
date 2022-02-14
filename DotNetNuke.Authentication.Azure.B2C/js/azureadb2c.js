Type.registerNamespace('dnn.adb2c.UserManagement');
dnn.extend(dnn.adb2c.UserManagement,
    {
        GroupModel: function (userManagement, model) {
            var that = this;

            if (!model)
                model = {};

            this.userManagement = userManagement;
            this.id = ko.observable(model.Id || "");
            this.displayName = ko.observable(model.DisplayName || "");
            this.objectType = ko.observable(model.IbjectType || "");
            //this.odataType = ko.observable(model["odata.type"] || "");

            this.toSimple = function () {
                return {
                    id: that.id(),
                    displayName: that.displayName(),
                    objectType: that.objectType()
                };
            };

        },
        UserModel: function (userManagement, model, addByUsername) {
            var that = this;

            if (!model)
                model = {};

            this.userManagement = userManagement;
            this.isAddingByUsername = ko.observable(addByUsername || false);
            this.identities = ko.observable(model.Identities || null);
            this.identityIsUsername = ko.computed(function () {
                return that.identities() && that.identities().length > 0 && that.identities()[0].SignInType === "userName";
            });

            this.accountEnabled = ko.observable(model.AccountEnabled || true);
            this.displayName = ko.observable(model.DisplayName || "");
            this.givenName = ko.observable(model.GivenName || "");
            this.mail = ko.observable(model.Mail
                || (model.OtherMails && model.OtherMails.length > 0 ? model.OtherMails[0] : "")
                || (model.Identities && model.Identities.length > 0 && model.Identities[0].IssuerAssignedId ? model.Identities[0].IssuerAssignedId : ""));
            this.username = ko.observable(model.Identities && model.Identities.length > 0 && model.Identities[0].IssuerAssignedId ? model.Identities[0].IssuerAssignedId : "");

            this.mailNickname = ko.observable(model.MailNickname || "");
            this.otherMails = ko.observableArray(model.OtherMails || []);
            this.proxyAddresses = ko.observableArray(model.ProxyAddresses || []);
            this.showInAddressList = ko.observable(model.ShowInAddressList || false);
            this.surname = ko.observable(model.Surname || "");
            this.userPrincipalName = ko.observable(model.UserPrincipalName || "");
            this.userType = ko.observable(model.UserType || "");
            this.id = ko.observable(model.Id || "");
            //this.odataType = ko.observable(model.@odata.type || "");
            this.assignedLicenses = ko.observable(model.AssignedLicenses || null);
            this.assignedPlans = ko.observable(model.AssignedPlans || null);
            this.objectType = ko.observable(model.ObjectType || "");
            this.deletionTimestamp = ko.observable(model.DeletionTimestamp || null);
            this.ageGroup = ko.observable(model.AgeGroup || null);
            this.city = ko.observable(model.City || null);
            this.companyName = ko.observable(model.CompanyName || null);
            this.consentProvidedForMinor = ko.observable(model.ConsentProvidedForMinor || null);
            this.country = ko.observable(model.Country || null);
            this.createdDateTime = ko.observable(model.CreatedDateTime || null);
            this.creationType = ko.observable(model.CreationType || null);
            this.department = ko.observable(model.Department || null);
            this.dirSyncEnabled = ko.observable(model.DirSyncEnabled || null);
            this.employeeId = ko.observable(model.EmployeeId || null);
            this.facsimileTelephoneNumber = ko.observable(model.FacsimileTelephoneNumber || null);
            this.immutableId = ko.observable(model.ImmutableId || null);
            this.isCompromised = ko.observable(model.IsCompromised || null);
            this.jobTitle = ko.observable(model.JobTitle || null);
            this.lastDirSyncTime = ko.observable(model.LastDirSyncTime || null);
            this.legalAgeGroupClassification = ko.observable(model.LegalAgeGroupClassification || null);
            this.mobile = ko.observable(model.Mobile || null);
            this.onPremisesDistinguishedName = ko.observable(model.OnPremisesDistinguishedName || null);
            this.onPremisesSecurityIdentifier = ko.observable(model.OnPremisesSecurityIdentifier || null);
            this.passwordPolicies = ko.observable(model.PasswordPolicies || null);
            this.passwordProfile = ko.observable(model.PasswordProfile || null);
            this.physicalDeliveryOfficeName = ko.observable(model.PhysicalDeliveryOfficeName || null);
            this.postalCode = ko.observable(model.PostalCode || null);            
            this.preferredLanguage = ko.observable(model.PreferredLanguage || "en");
            this.provisionedPlans = ko.observable(model.ProvisionedPlans || null);
            this.provisionedErrors = ko.observable(model.ProvisionedErrors || null);
            this.refreshTokensValidFromDateTime = ko.observable(model.RefreshTokensValidFromDateTime || null);
            this.signInNames = ko.observable(model.SignInNames || null);
            this.sipProxyAddress = ko.observable(model.SipProxyAddress || null);
            this.telephoneNumber = ko.observable(model.TelephoneNumber || null);
            this.thumbnailPhoto = ko.observable(model.ThumbnailPhoto || null);
            this.usageLocation = ko.observable(model.UsageLocation || null);
            this.userIdentities = ko.observable(model.UserIdentities || null);
            this.userState = ko.observable(model.UserState || null);
            this.userStateChangedOn = ko.observable(model.UserStateChangedOn || null);

            this.passwordType = ko.observable("auto");
            this.password = ko.observable("");
            this.sendEmail = ko.observable(true);

            // User custom attributes
            if (userManagement.customAttributes && userManagement.customAttributes !== "") {
                $.each(userManagement.customAttributes.split(","), 
                    function (index, customAttribute) {
                        var p = userManagement.customAttributesPrefix + customAttribute.replace(" ", "");
                        that[customAttribute.replace(" ", "")] = ko.observable((model && model.AdditionalData) ? model.AdditionalData[p] || "" : "");
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
                if (that.identities() && that.identities().length > 0 && that.identities()[0].SignInType === "userName") {
                    return that.identities()[0].IssuerAssignedId + sub;
                }
                else if (that.identities() && that.identities().length > 0 && that.identities()[0].SignInType === "emailAddress") {
                    return that.identities()[0].IssuerAssignedId + sub;
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
                if (that.identities() && that.identities().length > 0) {
                    return that.identities()[0].Issuer.replace('.', '');
                }                    
                return "";
            });

            this.addGroup = function () {
                if (that.userManagement.selectedGroup() && (that.groups().length === 0 || !that.groups().find(function (data) {
                        return data.id() === that.userManagement.selectedGroup().id();
                    }))) {
                    var group = new dnn.adb2c.UserManagement.GroupModel(that);
                    group.displayName(that.userManagement.selectedGroup().displayName());
                    group.id(that.userManagement.selectedGroup().id());
                    that.groups.push(group);
                    that.groups.valueHasMutated();
                }
            };

            this.removeGroup = function (g,t) {
                that.groups.remove(function (group) {
                    return group.id() === t.target.attributes["data-oid"].value;
                }); 
            };

            this.addUser = function () {
                var g = that.groupsSimple();

                that.userManagement.loading(true);

                var identity = that.isAddingByUsername() ?
                    {
                        "@odata.type": "microsoft.graph.objectIdentity",
                        issuerAssignedId: that.username(),
                        signInType: "userName"
                    } :
                    {
                        "@odata.type": "microsoft.graph.objectIdentity",
                        issuerAssignedId: that.mail(),
                        signInType: "emailAddress"
                    };

                var u = {
                    displayName: that.displayName(),
                    givenName: that.givenName(),
                    surname: that.surname(),
                    mail: that.mail(),
                    identities: [identity]
                };

                that.userManagement.ajax("AddUser", {
                    user: u,
                    passwordType: that.passwordType(),
                    password: that.password(),
                    sendEmail: that.sendEmail(),
                    groups: g
                },
                    function (data) {
                        that.userManagement.users.push(new dnn.adb2c.UserManagement.UserModel(that.userManagement, data));
                        that.userManagement.users.valueHasMutated();
                        that.userManagement.hideTab();
                        that.userManagement.loading(false);
                        toastr.success("User '" + that.displayName() + "' successfully added");
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
                        id: that.id()
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
                var g = that.groupsSimple();

                that.userManagement.loading(true);

                var u = {
                    id: that.id(),
                    displayName: that.displayName(),
                    givenName: that.givenName(),
                    surname: that.surname(),
                    mail: that.mail(),
                    identities: that.identities(),
                    accountEnabled: that.accountEnabled()
                };

                var u = {
                    id: that.id(),
                    username: that.username(),
                    displayName: that.displayName(),
                    givenName: that.givenName(),
                    surname: that.surname(),
                    mail: that.mail(),
                    preferredLanguage: that.preferredLanguage()
                };

                u["additionalData"] = {};
                if (userManagement.customAttributes && userManagement.customAttributes !== "") {
                    $.each(userManagement.customAttributes.split(","),
                        function (index, customAttribute) {
                            var p = userManagement.customAttributesPrefix + customAttribute.replace(" ", "");
                            u.additionalData[p] = that[customAttribute.replace(" ", "")]();
                        });
                }
                that.userManagement.ajax("UpdateUser", {
                    user: u,
                    groups: g
                    },
                    function (data) {
                        that.userManagement.hideTab();
                        that.userManagement.loading(false);
                        toastr.success("User '" + that.displayName() + "' successfully updated");
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
                        id: that.id()
                    },
                    passwordType: that.passwordType(),
                    password: that.password(),
                    sendEmail: that.sendEmail()
                },
                    function (data) {
                        toastr.success(dnn.getVar("UserPasswordUpdatedMessage"));
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
                        id: that.id()
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
                                that.groups.valueHasMutated();  
                            });
                        ajax("GetAllUsers?search=" + that.searchText(), null,
                            function (data) {
                                that.users.removeAll();
                                that.users.valueHasMutated();
                                $.each(data,
                                    function (index, user) {
                                        that.users.push(new dnn.adb2c.UserManagement.UserModel(that, user));
                                        that.users.valueHasMutated();
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
                                    that.users.valueHasMutated()
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
                that.newUser(new dnn.adb2c.UserManagement.UserModel(that, undefined, false));
                that.showTab();                
            };
            this.updateUser = function (evt) {
                that.newUser(null);
                that.changingPassword(false);
                ajax("GetUserGroups?objectId=" + evt.id(), null,
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

            window.onkeydown = function keyPress(e) {
                if (e.key === "Escape" && $(".b2c-overlay").css("display") === "block") {
                    that.hideTab();
                }
            };

            this.refresh();
        }
    });