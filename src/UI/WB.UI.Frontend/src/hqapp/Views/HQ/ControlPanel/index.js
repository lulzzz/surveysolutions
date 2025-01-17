const ControlPanelLayout = () => import( /*  webpackChunkName: "controlpanel" */ './ControlPanelLayout')
const AppUpdates = () => import( /*  webpackChunkName: "controlpanel" */ './AppUpdates')
const InterviewPackages = () => import( /*  webpackChunkName: "controlpanel" */ './InterviewPackages')
const ReevaluateInterview = () => import( /*  webpackChunkName: "controlpanel" */ './ReevaluateInterview')

export default class MapComponent {
    get routes() {
        return [
            {
                path: '/ControlPanel',
                component: ControlPanelLayout,
                children: [
                    {
                        path: 'Configuration',
                        component: () => import(/* webpackChunkName: "controlpanel" */'./Configuration'),
                    },
                    {
                        path: 'AppUpdates',
                        component: AppUpdates,
                    },
                    {
                        path: 'InterviewPackages',
                        component: InterviewPackages,
                    },
                    {
                        path: 'ReevaluateInterview',
                        component: ReevaluateInterview,
                    },
                    {
                        path: '',
                        component: InterviewPackages,
                    },
                ],
            },
        ]
    }
}
