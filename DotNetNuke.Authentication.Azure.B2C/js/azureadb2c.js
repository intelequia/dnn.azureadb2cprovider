Type.registerNamespace('dnn.adb2c.UserManagement');
dnn.extend(dnn.adb2c.UserManagement,
    {
        UserModel: function (userManagement, model) {
            var that = this;

            this.userManagement = userManagement;
            this.displayName = ko.observable(model.displayName);
            this.givenName = ko.observable(model.givenName);
            this.id = ko.observable(model.id);
            this.surname = ko.observable(model.surname);
            this.userPrincipalName = ko.observable(model.userPrincipalName);
            this.mail = ko.observable(model.mail);
            this.jobTitle = ko.observable(model.jobTitle);
            this.mobilePhone = ko.observable(model.mobilePhone);
            this.officeLocation = ko.observable(model.officeLocation);
            this.preferredLanguage = ko.observable(model.preferredLanguage);
            this.businessPhones = ko.observableArray(model.businessPhones);

            /*this.cssClass = function () {
                return that.complete() ? "complete" : "";
            };*/

            this.update = function () {
                that.userManagement.loading(true);
                that.userManagement.ajax("Update", {
                    id: that.id,
                    Complete: that.complete
                },
                    function (data) {
                        that.userManagement.refresh();
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
                        id: that.id
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
            this.loading = ko.observable(true);

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

            this.refresh = function () {
                that.loading(true);
                ajax("GetAllUsers", null,
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
            };

            this.addUser = function (evt) {
/*                var textbox = $("#newItemContent"),
                    itemText = textbox.val();
                if (itemText !== '') {
                    ajax("Add", { Content: itemText },
                        function (data) {
                            that.refresh();
                        },
                        function (e) {
                            that.loading(false);
                        }, null, "POST"
                    );
                }
                textbox.val('').focus();
                evt.preventDefault();*/
            };

            this.refresh();
        }
    });