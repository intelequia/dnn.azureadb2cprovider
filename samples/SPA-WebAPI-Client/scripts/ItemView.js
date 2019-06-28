var SPA_WebAPI_Client = SPA_WebAPI_Client || {};

function getCookie(name) {
    var value = "; " + document.cookie;
    var parts = value.split("; " + name + "=");
    if (parts.length == 2) return parts.pop().split(";").shift();
}

SPA_WebAPI_Client.itemListViewModel = function (moduleId, resx, apiUrl, token) {
    var service = {
        path: "api/tasks/",
        framework: $.ServicesFramework(moduleId),
        token: token
    }; 
    service.baseUrl = apiUrl + service.path;

      
    var isLoading = ko.observable(false);
    var errormsg = ko.observable('');
    var newItemValue = ko.observable('');
    if (!service.token)
        errormsg('Cookie AzureB2CUserToken does not exist or expired, you need to login again. Ensure you logged in with a Azure AD B2C user. ADMIN TIP: set the DNN session timeout with a value lower than the B2C auth token timeout');


    var itemList = ko.observableArray([]);

    var init = function () {
        getItemList();
    };

    var getItemList = function () { 
        isLoading(true);
        var jqXHR = $.ajax({
            url: service.baseUrl,
            beforeSend: function (xhr) {
                xhr.setRequestHeader("Authorization", "Bearer " + service.token);
            },
            dataType: "json"
        }).done(function (data) {
            if (data) {
                load(data);
            }
            else {
                // No data to load 
                itemList.removeAll();
            }
        }).always(function (data) {
            isLoading(false);
        }).error(function (e, errDesc) {
            errormsg(errDesc + ". See developer console log for more details");
        });
    };

    var addItem = function () {
        isLoading(true);
        var item = {
                Text: newItemValue()
        }; 

        var jqXHR = $.ajax({ 
            method: "POST",
            url: service.baseUrl,
            beforeSend: function (xhr) {
                xhr.setRequestHeader("Authorization", "Bearer " + service.token);
            },
            data: JSON.stringify(item),
            contentType: "application/json; charset=utf-8",
            dataType: "json"
        }).done(function () {
            newItemValue('');
            getItemList();
        }).always(function (data) {
            isLoading(false);
        }).error(function (e, errDesc) {
            errormsg(errDesc + ". See developer console log for more details");
        });

    };

    var deleteItem = function (item) {
        isLoading(true);
        var restUrl = service.baseUrl + item.id();
        var jqXHR = $.ajax({
            method: "DELETE",
            url: restUrl,
            beforeSend: function (xhr) {
                xhr.setRequestHeader("Authorization", "Bearer " + service.token);
            },
        }).done(function () {
            itemList.remove(item);
        }).always(function (data) {
            isLoading(false);
        }).error(function (e, errDesc) {
            errormsg(errDesc + ". See developer console log for more details");
        });
    };

    var load = function (data) {
        itemList.removeAll();
        var underlyingArray = itemList();
        for (var i = 0; i < data.length; i++) {
            var result = data[i];
            var item = new SPA_WebAPI_Client.itemViewModel();
            item.load(result);
            underlyingArray.push(item);
        }
        itemList.valueHasMutated();
    };

    return {
        init: init,
        load: load,
        itemList: itemList,
        getItemList: getItemList,
        addItem: addItem,
        deleteItem: deleteItem,
        isLoading: isLoading,
        errormsg: errormsg,
        newItemValue: newItemValue
    };
};

SPA_WebAPI_Client.itemViewModel = function () {
    var id = ko.observable('');
    var name = ko.observable('');

    var load = function (data) {
        id(data.Id);
        name(data.Text);
    };

    return {
        id: id,
        name: name,
        load: load
    };
};
