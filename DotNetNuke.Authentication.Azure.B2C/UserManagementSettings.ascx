<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="UserManagementSettings.ascx.cs" Inherits="DotNetNuke.Authentication.Azure.B2C.UserManagementSettings" %>
<%@ Register TagPrefix="dnn" Namespace="DotNetNuke.Web.Client.ClientResourceManagement" Assembly="DotNetNuke.Web.Client" %>
<%@ Register TagPrefix="dnn" TagName="Label" Src="~/controls/LabelControl.ascx" %>
<%@ Register TagPrefix="dnn" Namespace="DotNetNuke.Web.UI.WebControls.Internal" Assembly="DotNetNuke.Web" %>

<div class="dnnForm form-horizontal user-management-settings dnnClear">
    <div class="dnnForm" id="panels-userManagement">
        <h2 class="dnnFormSectionHead"><a href="#"><%= LocalizeString("UserManagementSettingsHeader")%></a></h2>
        <fieldset class="dnnClear" data-bind="with: settings">
	        <div class="dnnFormItem">
		        <dnn:label id="lblEnableAdd" controlname="chkEnableAdd" runat="server" />
		        <asp:Checkbox ID="chkEnableAdd" runat="server" />
	        </div>
	        <div class="dnnFormItem">
		        <dnn:label id="lblEnableUpdate" controlname="chkEnableUpdpate" runat="server" />
		        <asp:Checkbox ID="chkEnableUpdate" runat="server" />
	        </div>
	        <div class="dnnFormItem">
		        <dnn:label id="lblEnableDelete" controlname="chkEnableDelete" runat="server" />
		        <asp:Checkbox ID="chkEnableDelete" runat="server" />
	        </div>
	        <div class="dnnFormItem">
		        <dnn:label id="lblEnableImpersonate" controlname="chkEnableImpersonate" runat="server" />
		        <asp:Checkbox ID="chkEnableImpersonate" runat="server" />
	        </div>
	        <div class="dnnFormItem">
		        <dnn:label id="lblEnableExport" controlname="chkEnableExport" runat="server" />
		        <asp:Checkbox ID="chkEnableExport" runat="server" />
	        </div>
        </fieldset>
    </div>
</div>
