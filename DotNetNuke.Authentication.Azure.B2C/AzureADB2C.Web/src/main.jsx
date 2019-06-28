import React from "react";
import { render } from "react-dom";
import { Provider } from "react-redux";
import application from "./globals/application";
import configureStore from "./store/configureStore";
import Root from "./containers/Root";

let store = configureStore({enabled: false, instrumentationKey: ""});

application.dispatch = store.dispatch;

const appContainer = document.getElementById("azureADB2C-container");
const initCallback = appContainer.getAttribute("data-init-callback");
application.init(initCallback);

render(
    <Provider store={store}>
        <Root />
    </Provider>,
    appContainer
);    

