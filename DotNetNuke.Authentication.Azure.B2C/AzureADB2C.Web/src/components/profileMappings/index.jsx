import React, {Component} from "react";
import PropTypes from "prop-types";
import { connect } from "react-redux";
import SettingsActions from "../../actions/settings";
import ProfileMappingRow from "./profileMappingRow";
import ProfileMappingEditor from "./profileMappingEditor";
import { Collapsible } from "@dnnsoftware/dnn-react-common";
import "./style.less";
import { SvgIcons } from "@dnnsoftware/dnn-react-common";
import utils from "../../utils";
import resx from "../../resources";

class ProfileMappings extends Component {

    constructor() {
        super();

        this.state = {
            openId: "",
            tableFields: [],
            error: {
                profileMapping: false
            }
        };
    }
    UNSAFE_componentWillMount() {
        const {props} = this;

        props.dispatch(SettingsActions.getProfileSettings());
    }

    UNSAFE_componentWillReceiveProps(nextProps) {
        const {state} = this;

        state.error["profileMapping"] = (nextProps.profileMapping === null);
    }   

    onSettingChange(key, event) {
        let {props} = this;

        // TODO - We will have something like profileMappingsModified

        // props.dispatch(SettingsActions.settingsClientModified({
        //     enabled: (key === "AADB2CProviderEnabled") ? !props.enabled : props.enabled,
        //     useGlobalSettings: (key === "UseGlobalSettings") ? !props.useGlobalSettings : props.useGlobalSettings,
        //     autoRedirect: (key === "AutoRedirect") ? !props.autoRedirect : props.autoRedirect,
        //     apiKey: (key === "AppId") ? event.target.value : props.apiKey,
        //     apiSecret: (key === "AppSecret") ? event.target.value : props.apiSecret,
        //     redirectUri: (key === "RedirectUri") ? event.target.value: props.redirectUri,
        //     tenantName: (key === "TenantName") ? event.target.value : props.tenantName,
        //     tenantId: (key === "TenantId") ? event.target.value : props.tenantId,
        //     signUpPolicy: (key === "SignUpPolicy") ? event.target.value : props.signUpPolicy,
        //     profilePolicy: (key === "ProfilePolicy") ? event.target.value : props.profilePolicy,
        //     passwordResetPolicy: (key === "PasswordResetPolicy") ? event.target.value : props.passwordResetPolicy,
        // }));
    }

    onUpdateProfileMapping(profileMappingDetail) {
        const {props} = this;
        
        // TODO - Update profile mapping
    }

    onDeleteProfileMapping(profileMappingId) {
        const {props} = this;
        utils.utilities.confirm(resx.get("ProfileMappingDeletedWarning"), resx.get("Yes"), resx.get("No"), () => {

            // TODO - Remove the mapping

            // const siteAliases = Object.assign({}, props.siteAliases);
            // siteAliases.PortalAliases = siteAliases.PortalAliases.filter((item) => item.PortalAliasID !== aliasId);
            // props.dispatch(SiteBehaviorActions.deleteSiteAlias(aliasId, siteAliases, () => {
            //     util.utilities.notify(resx.get("SiteAliasDeleteSuccess"));
            //     this.collapse();
            // }, () => {
            //     util.utilities.notify(resx.get("SiteAliasDeleteError"));
            // }));
        });
    }

    onClickCancel() {
        utils.utilities.closePersonaBar();
    }

    onClickSave() {
        event.preventDefault();
        let {props} = this;

        // TODO - We will have an updateProfileMappings

        // props.dispatch(SettingsActions.updateGeneralSettings({
        //     enabled: props.enabled,
        //     useGlobalSettings: props.useGlobalSettings,
        //     autoRedirect: props.autoRedirect,
        //     apiKey: props.apiKey,
        //     apiSecret: props.apiSecret,
        //     redirectUri: props.redirectUri,
        //     tenantName: props.tenantName,
        //     tenantId: props.tenantId,
        //     signUpPolicy: props.signUpPolicy,
        //     profilePolicy: props.profilePolicy,
        //     passwordResetPolicy: props.passwordResetPolicy,
        // }, () => {
        //     utils.utilities.notify(resx.get("SettingsUpdateSuccess"));
        //     this.setState({
        //         clientModified: false
        //     });            
        // }, () => {
        //     utils.utilities.notifyError(resx.get("SettingsError"));
        // }));
    }

    /* eslint-disable react/no-did-update-set-state */
    componentDidUpdate(prevProps) {
        const {props} = this;
        if (props !== prevProps) {
            let tableFields = [];
            if (tableFields.length === 0) {
                tableFields.push({ "name": resx.get("DnnProfileProperty.Header"), "id": "DnnProfileProperty" });
                tableFields.push({ "name": resx.get("B2cClaim.Header"), "id": "B2cClaim" });
                tableFields.push({ "name": resx.get("B2cExtension.Header"), "id": "B2cExtension" });
            }
            this.setState({tableFields});
        }
    }

    uncollapse(id) {
        this.setState({
            openId: id
        });
    }

    collapse() {
        if (this.state.openId !== "") {
            this.setState({
                openId: ""
            });
        }
    }

    toggle(openId) {
        if (openId !== "") {
            this.uncollapse(openId);
        }
    }

    renderHeader() {
        let tableHeaders = this.state.tableFields.map((field) => {
            // let className = "profile-items header-" + field.id;
            let className = "header-" + field.id;
            return <div className={className} key={"header-" + field.id}>
                <span>{field.name}&nbsp; </span>
            </div>;
        });
        return <div className="header-row">{tableHeaders}</div>;
    }

    renderedProfileMappings() {
        let i = 0;
        if (this.props.profileMapping) {
            return this.props.profileMapping.map((item, index) => {
                let id = "row-" + i++;
                let profileMappingId = item.DnnProfilePropertyName + "-" + item.B2cClaimName + "-" + item.B2cExtensionName;
                return (
                    <ProfileMappingRow
                        profileMappingId={profileMappingId}
                        dnnProfilePropertyName={item.DnnProfilePropertyName}
                        b2cClaimName={item.B2cClaimName}
                        b2cExtensionName={item.B2cExtensionName}
                        index={index}
                        key={"profile-" + index}
                        closeOnClick={true}
                        openId={this.state.openId}
                        OpenCollapse={this.toggle.bind(this)}
                        Collapse={this.collapse.bind(this)}
                        onDelete={this.onDeleteProfileMapping.bind(this, profileMappingId)}
                        id={id}>
                        <ProfileMappingEditor
                            profileMappingId={profileMappingId}
                            Collapse={this.collapse.bind(this)}
                            onUpdate={this.onUpdateProfileMapping.bind(this)}
                            id={id}
                            openId={this.state.openId} />
                    </ProfileMappingRow>
                );
            });
        }
    }

    /* eslint-disable react/no-danger */
    render() {
        let opened = (this.state.openId === "add");
        return (
            <div className="dnn-azuread-b2c-profileMappingSettings">
                <div className="profile-items">
                    <div className="AddItemRow">
                        <div className="sectionTitle">{resx.get("lblProfilePropertiesMapping")}</div>
                        <div className={opened ? "AddItemBox-active" : "AddItemBox"} onClick={this.toggle.bind(this, opened ? "" : "add")}>
                            <div className="add-icon" dangerouslySetInnerHTML={{ __html: SvgIcons.AddIcon }}>
                            </div> {resx.get("cmdAddProfileMapping")}
                        </div>
                    </div>
                    <div className="profile-items-grid">
                        {this.renderHeader()}
                        <Collapsible isOpened={opened} style={{width: "100%", overflow: opened ? "visible" : "hidden"}}>
                            <ProfileMappingRow
                                dnnProfilePropertyName={"-"}
                                b2cClaimName={"-"}
                                b2cExtensionName={"-"}
                                deletable={false}
                                editable={false}
                                index={"add"}
                                key={"profileItem-add"}
                                closeOnClick={true}
                                openId={this.state.openId}
                                OpenCollapse={this.toggle.bind(this)}
                                Collapse={this.collapse.bind(this)}
                                onDelete={this.onDeleteProfileMapping.bind(this)}
                                id={"add"}>
                                <ProfileMappingEditor
                                    Collapse={this.collapse.bind(this)}
                                    onUpdate={this.onUpdateProfileMapping.bind(this)}
                                    id={"add"}
                                    openId={this.state.openId} />
                            </ProfileMappingRow>
                        </Collapsible>
                        {this.renderedProfileMappings()}
                    </div>
                </div>
            </div>
        );
    }
}

ProfileMappings.propTypes = {
    profileMapping: PropTypes.array
};


function mapStateToProps(state) {
    return {
        profileMapping: state.settings.profileMapping
    };
}

export default connect(mapStateToProps)(ProfileMappings);