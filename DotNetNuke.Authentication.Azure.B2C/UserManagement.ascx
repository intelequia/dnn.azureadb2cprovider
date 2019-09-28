<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="UserManagement.ascx.cs" Inherits="DotNetNuke.Authentication.Azure.B2C.UserManagement" %>
<%@ Register TagPrefix="dnn" Namespace="DotNetNuke.Web.Client.ClientResourceManagement" Assembly="DotNetNuke.Web.Client" %>

<dnn:DnnJsInclude runat="server" FilePath="~/Resources/Shared/scripts/knockout.js" Priority="10" />
<dnn:DnnJsInclude runat="server" FilePath="DesktopModules/AuthenticationServices/AzureB2C/js/azureadb2c.js" />
<dnn:DnnCssInclude runat="server" FilePath="DesktopModules/AuthenticationServices/AzureB2C/css/azureadb2c.css" Priority="10" />
<dnn:DnnJsInclude runat="server" FilePath="DesktopModules/AuthenticationServices/AzureB2C/js/sweetalert.min.js" Priority="22"></dnn:DnnJsInclude>
<dnn:DnnJsInclude runat="server" FilePath="DesktopModules/AuthenticationServices/AzureB2C/js/toastr.min.js" Priority="22"></dnn:DnnJsInclude>
<dnn:DnnCssInclude runat="server" FilePath="DesktopModules/AuthenticationServices/AzureB2C/css/sweetalert.css" Priority="22"></dnn:DnnCssInclude>
<dnn:DnnCssInclude runat="server" FilePath="DesktopModules/AuthenticationServices/AzureB2C/css/toastr.min.css" />

<!-- ko with: dnn.adb2c.usermgt -->
<div id="UserManagement-<% = ModuleId %>">
    <input id="addUser" type="submit" class="dnnPrimaryAction" value="Add" data-bind="click: addUser" />
    <table id="userManagementTable" class="table table-hover">
        <thead>
            <tr role="row">
                <th>Display Name</th>
                <th class="userName">Username</th>
                <th></th>
            </tr>
        </thead>
        <tbody data-bind="foreach: users">
            <tr>
                <td class="displayName">
                    <span data-bind="text: displayName"></span>
                </td>
                <td class="userName">
                    <div data-bind="text: userPrincipalName"></div>
                </td>
                <td>
                    <a class="kblist-glyph segoemdl2 pull-right fa fa-trash-o" style="font-size: 1.2em; margin-left: 2.3em" data-bind="click: remove"></a>   
                </td>
            </tr>
        </tbody>
        <tfoot>
            <tr>
                <th colspan="3">
                    <div data-bind="visible: !loading()"><span class="userCount" data-bind="text: users().length">0</span> user(s)</div>
                    <div data-bind="visible: loading()">Loading...</div>
                </th>
            </tr>
        </tfoot>
    </table>
</div>
<!-- /ko -->
 
<script type="text/javascript">
    if (typeof dnn.adb2c === 'undefined') dnn.adb2c = {};
    $(function() {
        dnn.adb2c.usermgt = new dnn.adb2c.UserManagement.UserManagementModel();
        ko.applyBindings(dnn.adb2c.usermgt, $("#UserManagement-<% = ModuleId %>")[0]);
    });
</script>