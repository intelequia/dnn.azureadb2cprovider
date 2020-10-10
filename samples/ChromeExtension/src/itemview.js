var SPA_WebAPI_Client = SPA_WebAPI_Client || {};

SPA_WebAPI_Client.itemListViewModel = function (apiUrl, token) {
    var that = this;
    var service = {
        apiUrl: apiUrl,
        token: token
    }; 
    const headers = new Headers();
    const bearer = `Bearer ${token}`;  
    headers.append("Authorization", bearer);
    headers.append("Content-Type", "application/json; charset=utf-8")
      
    var isLoading = ko.observable(false);
    var errormsg = ko.observable('');
    if (!service.token)
        errormsg('Login before calling the todo list api');


    var itemList = ko.observableArray([]);

    var init = function () {
        getItemList();
    };

    var getItemList = function () { 
        isLoading(true);

        const options = {
            method: "GET",
            headers: headers
          };
               
        fetch(service.apiUrl, options)
            .then(response => response.json())
            .then(response => {
                console.log("Web API returned:\n" + JSON.stringify(response));
                if (response) {
                    load(response);
                }
                else {
                    itemList.removeAll();
                }      
                isLoading(false);          
            }).catch(error => {
                isLoading(false);
                errormsg(error + ". See developer console log for more details");
            });          
    };

    var addItem = function (newItemValue) {
        isLoading(true);
        var item = {
                Text: newItemValue
        }; 

        const options = {
            method: "POST",
            headers: headers,
            body: JSON.stringify(item)            
          };
               
        fetch(service.apiUrl, options)
            .then(response => {
                console.log("Item added: " + document.getElementById('txtNewItem').value);
                document.getElementById('txtNewItem').value = "";
                getItemList();
                isLoading(false);          
            }).catch(error => {
                isLoading(false);
                errormsg(error + ". See developer console log for more details");
            });          
    };

    var deleteItem = function (itemId) {
        isLoading(true);
        var restUrl = service.apiUrl + "/" + itemId;
        const options = {
            method: "DELETE",
            headers: headers            
          };
               
        fetch(restUrl, options)
            .then(response => {
                console.log("Item deleted: " + itemId);
                getItemList();
                isLoading(false);          
            }).catch(error => {
                isLoading(false);
                errormsg(error + ". See developer console log for more details");
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


        // Instead of using Knockout bindings the elements are created using DOM. To use
        // bindings, Chrome extension sandboxing workaround is required (out of the scope of this demo)
        // See http://aboutcode.net/2013/01/06/knockout-chrome-extension.html for more info
        itemsTable.innerHTML = "";
        itemList().forEach(item => {
            var a = document.createElement("a");
            a.setAttribute("class", "list-group-item list-group-item-action");
            a.setAttribute("href", "#");
            a.innerText = item.name();
            a.addEventListener("click", function() {
                deleteItem(item.id());
                itemsTable.removeChild(a);
                return false;
              });
            itemsTable.appendChild(a);
        });        

    };

    return {
        init: init,
        load: load,
        itemList: itemList,
        getItemList: getItemList,
        addItem: addItem,
        deleteItem: deleteItem,
        isLoading: isLoading,
        errormsg: errormsg
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