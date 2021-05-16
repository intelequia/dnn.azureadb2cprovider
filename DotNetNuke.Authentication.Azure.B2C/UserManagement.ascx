<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="UserManagement.ascx.cs" Inherits="DotNetNuke.Authentication.Azure.B2C.UserManagement" %>
<%@ Register TagPrefix="dnn" Namespace="DotNetNuke.Web.Client.ClientResourceManagement" Assembly="DotNetNuke.Web.Client" %>

<dnn:DnnJsInclude runat="server" FilePath="~/Resources/Shared/scripts/knockout.js" Priority="10" />
<dnn:DnnJsInclude runat="server" FilePath="DesktopModules/AuthenticationServices/AzureB2C/js/azureadb2c.js" />
<dnn:DnnCssInclude runat="server" FilePath="DesktopModules/AuthenticationServices/AzureB2C/css/azureadb2c.css" Priority="10" />
<dnn:DnnJsInclude runat="server" FilePath="DesktopModules/AuthenticationServices/AzureB2C/js/sweetalert.min.js" Priority="22"></dnn:DnnJsInclude>
<dnn:DnnJsInclude runat="server" FilePath="DesktopModules/AuthenticationServices/AzureB2C/js/toastr.min.js" Priority="22"></dnn:DnnJsInclude>
<dnn:DnnCssInclude runat="server" FilePath="DesktopModules/AuthenticationServices/AzureB2C/css/sweetalert.css" Priority="22"></dnn:DnnCssInclude>
<dnn:DnnCssInclude runat="server" FilePath="DesktopModules/AuthenticationServices/AzureB2C/css/toastr.min.css" />

<div class="b2c-overlay"></div>

<!-- ko with: dnn.adb2c.usermgt -->
<div id="UserManagement-<% = ModuleId %>">
    <div class="userManagement actions">
        <% if (EnableAdd)
            { %>
        <a id="addUser" data-bind="click: addUser"> <i class="fa fa-plus-square-o"></i> <% = LocalizeString("AddUser") %></a>
         | 
        <% } %>
        <a id="refreshUsers" data-bind="click: refresh"> <i class="fa icon-refresh"></i> <% = LocalizeString("Refresh") %></a>
        <% if (EnableExport)
            { %>
        |
        <a id="export" data-bind="click: downloadUsers"> <% = LocalizeString("Export") %></a>
        <% } %>
        <% if (EnableImpersonate)
            { %>
        <% if (CanImpersonate)
                                     { %>
         | 
        <a id="impersonate" data-bind="click: impersonate"> <% = LocalizeString("Impersonate") %></a>
        <% }
                                     else
                                     { %>
        | <span class="disabled"><% = LocalizeString("Impersonate") %></span>
        <% }        %>
        <% } %>
        <div class="searchbox">
            <input type="text" class="txtSearch" placeholder="Search..." data-bind="value: searchText, valueUpdate: 'keyup', event: { keyup: search }" autocomplete="off" />
        </div>
    </div>
    <table id="userManagementTable" class="table table-hover">
        <thead>
            <tr role="row">
                <th><% = LocalizeString("DisplayName") %></th>
                <th class="userName"><% = LocalizeString("Username") %></th>
                <th></th>
            </tr>
        </thead>
        <tbody data-bind="foreach: users">
            <tr>
                <td class="displayName" data-bind="event: { click: $parent.updateUser }">
                    <span data-bind="text: displayName"></span>
                </td>
                <td class="userName" data-bind="event: { click: $parent.updateUser }">
                    <div data-bind="text: usernameToDisplay, css: identityIssuer" ></div>
                </td>
                <td>
                    <% if (EnableDelete)
                        { %>
                    <a class="kblist-glyph segoemdl2 float-right fa fa-trash" style="font-size: 1.2em; margin-left: 2.3em" data-bind="click: remove"></a>   
                    <% } %>
                </td>
            </tr>
        </tbody>
        <tfoot>
            <tr> 
                <th colspan="3">
                    <div data-bind="visible: !loading()"><span class="userCount" data-bind="text: users().length">0</span> <% = LocalizeString("Users") %></div>
                    <div data-bind="visible: loading()"><% = LocalizeString("Loading") %></div>
                </th>
            </tr>
        </tfoot>
    </table>
     
    <div id="rightPanel" class="b2c-rightTab">  
        <div class="b2c-close-panel">
            <a id="b2c-close-btn" data-bind="click: hideTab"> <i class="fa fa-window-close-o"></i></a>
        </div>
        <!-- ko with: newUser -->
        <div id="addUserPanel" class="b2c-panel">
            <h3 class="dnnFormSectionHead"><% = LocalizeString("AddUser") %></h3>
            <div class="dnnForm">
                <p><% = LocalizeString("AddUserDescription") %></p>
                <fieldset class="dnnClear">
                    <% if (EnableAddUsersByUsername)
                        { %>
                    <div class="dnnFormItem">
                        <label for="txtUsername"><% = LocalizeString("lblUserName") %> <% = (!EnableAddUsersByEmail ? "*" : "")%></label>
                        <input type="text" id="txtUsername" class="full-width" data-bind="value: username" />
                    </div>
                    <% } %>
                    <div class="dnnFormItem">
                        <label for="txtFirstName"><% = LocalizeString("lblFirstName") %> *</label>
                        <input type="text" id="txtFirstName" class="full-width" data-bind="value: givenName" />
                    </div>
                    <div class="dnnFormItem">
                        <label for="txtLastName"><% = LocalizeString("lblLastName") %> *</label>
                        <input type="text" id="txtLastName" class="full-width" data-bind="value: surname" />
                    </div>
                    <div class="dnnFormItem">
                        <label for="txtDisplayName"><% = LocalizeString("lblDisplayName") %> *</label>
                        <input type="text" id="txtDisplayName" class="full-width" data-bind="value: displayName, event: { focus: setDisplayName }" />
                    </div>
                    <% if (EnableAddUsersByEmail)
                        { %> 
                    <div class="dnnFormItem">
                        <label for="txtEmail"><% = LocalizeString("lblEmail") %> <% = (!EnableAddUsersByUsername ? "*" : "") %></label>
                        <input type="text" id="txtEmail" class="full-width" autocomplete="new-password" data-bind="value: mail" />
                    </div>
                    <% } %>
                    <div class="dnnFormItem">
                        <label><% =LocalizeString("PasswordSettings") %></label>
                    </div>
                    <div class="dnnFormItem">
                        <input type="radio" name="PasswordType" value="auto" data-bind="checked: passwordType" />
                        <span class="inline"><% = LocalizeString("AutoGeneratePassword") %></span>
                    </div>
                    <div class="dnnFormItem">
                        <input type="radio" name="PasswordType" value="user" data-bind="checked: passwordType" />
                        <span class="inline"><% = LocalizeString("UserGeneratedPassword") %></span>
                    </div>
                    <!-- ko if: passwordType() === "user" -->
                    <div class="dnnFormItem">
                        <label for="txtPassword"><% = LocalizeString("lblPassword") %> *</label>
                        <input type="password" id="txtPassword" class="full-width" autocomplete="new-password" data-bind="value: password" />
                    </div>
                    <!-- /ko -->
                    <div class="dnnFormItem">
                        <input type="checkbox" id="chkSendEmail" data-bind="checked: sendEmail" />
                        <span class="inline" for="chkSendEmail"><% = LocalizeString("lblSendEmail") %></span>
                    </div>
                    <hr />  
                    <div class="dnnFormItem">
                        <label for="cboGroups"><% = LocalizeString("lblGroups") %></label>
                        <select data-bind="options: $parent.groups, optionsText: 'displayName', value: $parent.selectedGroup, optionsCaption: '<% = LocalizeString("ChooseGroup") %>'"></select>
                        <a id="btnAddGroup" class="dnnPrimaryAction" data-bind="click: addGroup"><% = LocalizeString("AddGroup") %></a>
                    </div>
                    <div class="dnnFormItem">
                        <div class="b2c-groups-header"><% = LocalizeString("CurrentGroups") %></div>
                        <div data-bind="foreach: groups">
                            <div class="b2c-groups-row">
                                <span data-bind="text: displayName"></span>
                                <a class="kblist-glyph segoemdl2 float-right fa fa-trash" style="font-size: 1.2em; margin-left: 2.3em" data-bind="attr: { 'data-oid': objectId }, click: $parent.removeGroup"></a>   
                            </div> 
                        </div>
                        <!-- ko if: groups().length == 0 -->
                            <div class="b2c-groups-row"><% = LocalizeString("NoGroups") %></div>
                        <!-- /ko -->
                    </div>

                </fieldset>
                <a id="btnAddUser" class="dnnPrimaryAction" data-bind="click: addUser"><% = LocalizeString("AddUser") %></a>
                <a id="btnCancel" class="dnnSecondaryAction" data-bind="click: $parent.hideTab"><% = LocalizeString("Cancel") %></a>
            </div>
        </div>
        <!-- /ko -->

        <!-- ko with: selectedUser -->
        <!-- ko ifnot: $parent.changingPassword() -->
        <div id="updateUserPanel" class="b2c-panel">
            <h3 class="dnnFormSectionHead"><% = LocalizeString("UpdateUser") %></h3>
            <div class="dnnForm">
                <% if (EnableUpdate)
                    { %>
                    <div class="actions">
                        <a id="btnForcePasswordReset" href="#" data-bind="click: forceChangePassword"><% = LocalizeString("ForcePasswordReset") %></a>
                        |
                        <a id="btnChangePassword" href="#" data-bind="click: $parent.changePassword"><% = LocalizeString("ChangePassword") %></a>                        
                    </div>
                <% } %>
                <fieldset class="dnnClear">
                    <% if (EnableAddUsersByUsername)
                        { %>
                    <div class="dnnFormItem">
                        <label for="txtUsername"><% = LocalizeString("lblUserName") %> <% = (!EnableAddUsersByEmail ? "*" : "")%></label>
                        <input type="text" id="txtUsername" class="full-width" data-bind="value: username" />
                    </div>
                    <% } %>
                    <div class="dnnFormItem">
                        <label for="txtFirstName"><% = LocalizeString("lblFirstName") %> *</label>
                        <input type="text" id="txtFirstName" class="full-width" data-bind="value: givenName" />
                    </div>
                    <div class="dnnFormItem">
                        <label for="txtLastName"><% = LocalizeString("lblLastName") %> *</label>
                        <input type="text" id="txtLastName" class="full-width" data-bind="value: surname" />
                    </div>
                    <div class="dnnFormItem">
                        <label for="txtDisplayName"><% = LocalizeString("lblDisplayName") %> *</label>
                        <input type="text" id="txtDisplayName" class="full-width" data-bind="value: displayName, event: { focus: setDisplayName }" />
                    </div>
                    <% if (EnableAddUsersByEmail)
                        { %> 
                    <div class="dnnFormItem">
                        <label for="txtEmail"><% = LocalizeString("lblEmail") %> <% = (!EnableAddUsersByUsername ? "*" : "") %></label>
                        <input type="text" id="txtEmail" class="full-width" autocomplete="new-password" data-bind="value: mail" />
                    </div>
                    <% } %>
                    <% foreach (string customField in CustomFields)
                        { %>
                    <div class="dnnFormItem">
                        <label><% = customField %></label>
                        <input type="text" id="txt<% = customField.Replace(" ", "") %>" class="full-width" data-bind="value: <% = customField.Replace(" ", "") %>" />
                    </div>
                    <% }   %>
                    <% if (EnableUpdate)
                        { %>
                    <div class="dnnFormItem">
                        <label for="cboGroups"><% = LocalizeString("lblGroups") %></label>
                        <select data-bind="options: $parent.groups, optionsText: 'displayName', value: $parent.selectedGroup, optionsCaption: '<% = LocalizeString("ChooseGroup") %>'"></select>
                        <a id="btnAddGroup" class="dnnPrimaryAction" data-bind="click: addGroup"><% = LocalizeString("AddGroup") %></a>
                    </div>
                    <% } %>
                    <div class="dnnFormItem">
                        <div class="b2c-groups-header"><% = LocalizeString("CurrentGroups") %></div>
                        <div data-bind="foreach: groups">
                            <div class="b2c-groups-row">
                                <span data-bind="text: displayName"></span>
                                <a class="kblist-glyph segoemdl2 float-right fa fa-trash" style="font-size: 1.2em; margin-left: 2.3em" data-bind="attr: { 'data-oid': objectId }, click: $parent.removeGroup"></a>   
                            </div>
                        </div>
                        <!-- ko if: groups().length == 0 -->
                            <div class="b2c-groups-row"><% = LocalizeString("NoGroups") %></div>
                        <!-- /ko -->
                    </div>
                </fieldset>
                <% if (EnableUpdate)
                    { %>
                <a id="btnAddUser" class="dnnPrimaryAction" data-bind="click: update"><% = LocalizeString("UpdateUser") %></a>
                <% } %>
                <a id="btnCancel" class="dnnSecondaryAction" data-bind="click: $parent.hideTab"><% = LocalizeString("Cancel") %></a>
            </div>
        </div>
         <!-- /ko -->

        <!-- ko if: $parent.changingPassword() -->
        <div id="changePasswordPanel" class="b2c-panel">
            <h3 class="dnnFormSectionHead"><% = LocalizeString("ChangePassword") %></h3>
            <div class="dnnForm">
            <p><% = LocalizeString("ChangePasswordSubtitle") %> <strong><span class="inline" data-bind="text: displayName"></span></strong> (<span class="inline" data-bind="text: mail"></span>)</p>
                <fieldset class="dnnClear">
                    <div class="dnnFormItem">
                        <label><% =LocalizeString("PasswordSettings") %></label>
                    </div>
                    <div class="dnnFormItem">
                        <input type="radio" name="PasswordType" value="auto" data-bind="checked: passwordType" />
                        <span class="inline"><% = LocalizeString("AutoGeneratePassword") %></span>
                    </div>
                    <div class="dnnFormItem">
                        <input type="radio" name="PasswordType" value="user" data-bind="checked: passwordType" />
                        <span class="inline"><% = LocalizeString("UserGeneratedPassword") %></span>
                    </div>
                    <!-- ko if: passwordType() === "user" -->
                    <div class="dnnFormItem">
                        <label for="txtPassword"><% = LocalizeString("lblPassword") %> *</label>
                        <input type="password" id="txtPassword" class="full-width" autocomplete="new-password" data-bind="value: password" />
                    </div>
                    <!-- /ko -->
                    <div class="dnnFormItem">
                        <input type="checkbox" id="chkSendEmail" data-bind="checked: sendEmail" />
                        <span class="inline" for="chkSendEmail"><% = LocalizeString("lblSendEmail") %></span>
                    </div>

                </fieldset>
                <a id="btnUpdatePassword" class="dnnPrimaryAction" data-bind="click: changePassword"><% = LocalizeString("ChangePassword") %></a>
                <a id="btnCancelUpdatePassword" class="dnnSecondaryAction" data-bind="click: $parent.hideTab"><% = LocalizeString("Cancel") %></a>
            </div>
        </div>
        <!-- /ko -->

        <!-- /ko -->




    </div>

</div>

<!-- /ko -->
 

 
<script type="text/javascript">
    if (typeof dnn.adb2c === 'undefined') dnn.adb2c = {};
    $(function() {
        dnn.adb2c.usermgt = new dnn.adb2c.UserManagement.UserManagementModel();
        ko.applyBindings(dnn.adb2c.usermgt, $("#UserManagement-<% = ModuleId %>")[0]);
    });
</script>