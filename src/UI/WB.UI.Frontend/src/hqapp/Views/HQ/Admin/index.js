import EmailProviders from "./EmailProviders"
import TabletLogs from "./TabletLogs"
import Vue from "vue"

export default class AdminComponent {
    constructor(rootStore) {
        this.rootStore = rootStore;
    }

    get routes() {
        return [{
                path: '/Settings/EmailProviders',
                component: EmailProviders
            },
            {
                path: '/Diagnostics/Logs',
                component: TabletLogs
            }
        ]
    }

    initialize() {
        const VeeValidate = require('vee-validate');
        Vue.use(VeeValidate);
        // const installApi = require("~/webinterview/api").install

        // installApi(Vue, {
        //     store: this.rootStore
        // });
    }
}