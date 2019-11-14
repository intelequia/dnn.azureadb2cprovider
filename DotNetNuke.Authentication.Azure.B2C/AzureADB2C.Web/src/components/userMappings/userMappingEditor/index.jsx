import React, { Component } from "react";
import PropTypes from "prop-types";
import { connect } from "react-redux";
import "./style.less";
import { SingleLineInputWithError, GridSystem, Button, InputGroup } from "@dnnsoftware/dnn-react-common";
import SettingsActions from "../../../actions/settings";
import util from "../../../utils";
import resx from "../../../resources";

class UserMappingEditor extends Component {
    constructor() {
        super();

        this.state = {
            mappingDetail: {
                DnnPropertyName: "",
                B2cPropertyName: ""
            },
            error: {
                dnnPropertyName: false,
                b2cPropertyName: false
            },
            triedToSubmit: false
        };
    }
    
    componentWillMount() {
        const {props} = this;
        const {state} = this;

        state.mappingDetail["MappingId"] = props.mappingId;
        state.mappingDetail["DnnPropertyName"] = props.dnnPropertyName;
        state.mappingDetail["B2cPropertyName"] = props.b2cPropertyName;

        state.error["dnnPropertyName"] = (props.dnnPropertyName === null);
        state.error["b2cPropertyName"] = (props.b2cPropertyName === null);
    }

    /* eslint-disable react/no-did-update-set-state */
    componentDidUpdate(prevProps) {
        const {props, state} = this;
        if ((props !== prevProps) && props.mappingDetail ) {
            if (props.mappingDetail["DnnPropertyName"] === undefined || props.mappingDetail["DnnPropertyName"] === "") {
                state.error["dnnPropertyName"] = true;
            }
            else if (props.mappingDetail["DnnPropertyName"] !== "" && props.mappingDetail["DnnPropertyName"] !== undefined) {
                state.error["dnnPropertyName"] = false;
            }
    
            this.setState({
                mappingDetail: Object.assign({}, props.mappingDetail),
                triedToSubmit: false,
                error: state.error
            });
        }
    }

    onSettingChange(key, event) {
        let {state, props} = this;
        let mappingDetail = Object.assign({}, state.mappingDetail);

        if (key === "DnnPropertyName") {
            state.error["dnnPropertyName"] = !props.onValidate(mappingDetail, event.value);
        }

        if (mappingDetail[key] === "" && key === "B2cPropertyName") {
            state.error["b2cPropertyName"] = true;
        }
        else if (mappingDetail[key] !== "" && key === "B2cPropertyName") {
            state.error["b2cPropertyName"] = false;
        }

        mappingDetail[key] = typeof (event) === "object" ? event.target.value : event;

        this.setState({
            mappingDetail: mappingDetail,
            triedToSubmit: false,
            error: state.error
        });

        props.dispatch(SettingsActions.userMappingClientModified(mappingDetail));
    }

    onSave() {
        const {props, state} = this;
        this.setState({
            triedToSubmit: true
        });
        if (state.error.dnnPropertyName || state.error.b2cPropertyName) {
            return;
        }

        props.onUpdate(state.mappingDetail);
        props.Collapse();
    }

    onCancel() {
        const {props} = this;

        if (props.mappingClientModified) {
            util.utilities.confirm(resx.get("SettingsRestoreWarning"), resx.get("Yes"), resx.get("No"), () => {
                props.dispatch(SettingsActions.cancelUserMappingClientModified());
                props.Collapse();
            });
        }
        else {
            props.Collapse();
        }
    }

    /* eslint-disable react/no-danger */
    render() {
        if (this.state.mappingDetail !== undefined || this.props.id === "add") {
            const columnOne = <div key="column-one" className="left-column">
                <InputGroup>
                    <SingleLineInputWithError
                        withLabel={true}
                        label={resx.get("lblDnnPropertyName")}
                        tooltipMessage={resx.get("lblDnnPropertyName.Help")}
                        inputStyle={{ margin: "0" }}
                        value={this.state.mappingDetail.DnnPropertyName}
                        enabled={false}
                    />
                </InputGroup>
            </div>;
            const columnTwo = <div key="column-two" className="right-column">
                <InputGroup>
                    <SingleLineInputWithError
                        withLabel={true}
                        label={resx.get("lblB2cPropertyName")}
                        tooltipMessage={resx.get("lblB2cPropertyName.Help")}
                        inputStyle={{ margin: "0" }}
                        error={this.state.error.b2cPropertyName}
                        errorMessage={resx.get("InvalidB2cPropertyName")}
                        value={this.state.mappingDetail.B2cPropertyName}
                        onChange={this.onSettingChange.bind(this, "B2cPropertyName")}
                    />
                </InputGroup>
            </div>;

            return (
                <div className="usermapping-editor">
                    <GridSystem numberOfColumns={2}>{[columnOne, columnTwo]}</GridSystem>
                    <div className="editor-buttons-box">
                        <Button
                            type="secondary"
                            onClick={this.onCancel.bind(this)}>
                            {resx.get("Cancel")}
                        </Button>
                        <Button
                            type="primary"
                            onClick={this.onSave.bind(this)}>
                            {resx.get("SaveSettings")}
                        </Button>
                    </div>
                </div>
            );
        }
        else return <div />;
    }
}

UserMappingEditor.propTypes = {
    dispatch: PropTypes.func.isRequired,
    mappingDetail: PropTypes.object,
    mappingId: PropTypes.string,
    dnnPropertyName: PropTypes.string,
    b2cPropertyName: PropTypes.string,
    Collapse: PropTypes.func,
    onUpdate: PropTypes.func,
    id: PropTypes.string,
    mappingClientModified: PropTypes.bool,
    onValidate: PropTypes.func
};

function mapStateToProps() {
    return {
    };
}

export default connect(mapStateToProps)(UserMappingEditor);