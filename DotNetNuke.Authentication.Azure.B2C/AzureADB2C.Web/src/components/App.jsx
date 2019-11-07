import React, {Component} from "react";
import PropTypes from "prop-types";
import { connect } from "react-redux";
import {PersonaBarPage, PersonaBarPageHeader, PersonaBarPageBody, DnnTabs as Tabs} from "@dnnsoftware/dnn-react-common";
import SettingsActions from "../actions/settings";
import GeneralSettings from "./general";
import SyncSettings from "./sync";
import ProfileMappings from "./profileMappings";

import "./style.less";

class App extends Component {

    constructor() {
        super();
        this.onSelectTab = this.onSelectTab.bind(this);
    }
    onSelectTab(index) {
        this.props.dispatch(SettingsActions.switchTab(index));
    }
    render() {
        return (
            <div id="AzureADAppContainer">
                <PersonaBarPage isOpen={true}>
                    <PersonaBarPageHeader title="Azure Active Directory B2C" titleCharLimit={30}>
                    </PersonaBarPageHeader>
                    <PersonaBarPageBody>
                        <Tabs
                            onSelect={this.onSelectTab.bind(this)}
                            selectedIndex={this.props.selectedTab}
                            tabHeaders={["General Settings","Advanced Settings", "Profile Mappings"]}>
                            <GeneralSettings />
                            <SyncSettings />
                            <ProfileMappings />
                        </Tabs>  
                    </PersonaBarPageBody>
                </PersonaBarPage>
            </div>
        );
    }
}

App.propTypes = {
    dispatch: PropTypes.func.isRequired,
    selectedTab: PropTypes.number    
};


function mapStateToProps(state) {
    return {
        selectedTab: state.settings.selectedTab
    };
}

export default connect(mapStateToProps)(App);