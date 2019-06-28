<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="Login.ascx.cs" Inherits="DotNetNuke.Authentication.Azure.B2C.Login" %>
<%@ Register TagPrefix="dnnC" Namespace="DotNetNuke.Web.Client.ClientResourceManagement" Assembly="DotNetNuke.Web.Client" %>
<dnnC:DnnCssInclude ID="AzureCss" runat="server" FilePath="~/DesktopModules/AuthenticationServices/AzureB2C/module.css" />


<li id="loginItem" runat="server" class="azureb2c" >
    <asp:LinkButton runat="server" ID="loginButton" CausesValidation="False">
        <span><%=LocalizeString("LoginAzureB2C")%></span>
    </asp:LinkButton>
</li>
<li id="registerItem" runat="Server" class="azureb2c">
    <asp:LinkButton ID="registerButton" runat="server" CausesValidation="False">
        <span><%=LocalizeString("RegisterAzureB2C") %></span>
    </asp:LinkButton>
</li>
<script>
    function getHashParams() {
        var hashParams = {};
        var e,
            a = /\+/g,  // Regex for replacing addition symbol with a space
            r = /([^&;=]+)=?([^&;]*)/g,
            d = function (s) { return decodeURIComponent(s.replace(a, " ")); },
            q = window.location.hash.substring(1);
        while (e = r.exec(q))
            hashParams[d(e[1])] = d(e[2]);
        return hashParams;
    }
    window.onload = function () {
        var params = getHashParams();
        if (params["error"] && params["error_description"] && document.getElementsByClassName("loginContent").length > 0) {
            var e = document.getElementsByClassName("loginContent")[0];
            if (e.parentElement) e = e.parentElement;
            if (e.parentElement) e = e.parentElement;
            if (e.parentElement) e = e.parentElement;
            e.insertAdjacentHTML('afterBegin', '<div class="dnnFormMessage dnnFormValidationSummary"><span>'
                + params["error_description"].replace(/\n/g, '<br/>')
                + '</span></div>');
        }
    }
</script>