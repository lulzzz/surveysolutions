﻿using System;
using System.Reflection;
using System.Threading.Tasks;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V4.Widget;
using Android.Views;
using Android.Widget;
using MvvmCross.Base;
using MvvmCross.Core;
using MvvmCross.Droid.Support.V4;
using MvvmCross.Navigation;
using MvvmCross.Platforms.Android.Binding.BindingContext;
using MvvmCross.Platforms.Android.Presenters.Attributes;
using WB.Core.BoundedContexts.Supervisor.Properties;
using WB.Core.BoundedContexts.Supervisor.ViewModel.Dashboard;
using WB.Core.GenericSubdomains.Portable.ServiceLocation;

namespace WB.UI.Supervisor.Activities.Dashboard
{
    [MvxFragmentPresentation(typeof(DashboardMenuViewModel), Resource.Id.navigation_frame,
        ActivityHostViewModelType = typeof(DashboardViewModel))]
    public class MenuFragment : MvxFragment<DashboardMenuViewModel>, NavigationView.IOnNavigationItemSelectedListener
    {
        private NavigationView navigationView;
        private IMenuItem previousMenuItem;
        private DrawerLayout drawerLayout;

        private IMvxNavigationService mvxNavigationService =>
            ServiceLocator.Current.GetInstance<IMvxNavigationService>();
        private IMvxMainThreadDispatcher mvxMainThreadDispatcher =>
            ServiceLocator.Current.GetInstance<IMvxMainThreadDispatcher>();

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            base.OnCreateView(inflater, container, savedInstanceState);

            var view = this.BindingInflate(Resource.Layout.dashboard_sidebar, null);

            navigationView = view.FindViewById<NavigationView>(Resource.Id.dashboard_sidebar_navigation);
            navigationView.SetNavigationItemSelectedListener(this);

            mvxNavigationService.AfterNavigate += MenuFragment_AfterNavigate;

            LocalizeMenuItem(Resource.Id.dashboard_to_be_assigned, SupervisorDashboard.ToBeAssigned, nameof(ViewModel.ToBeAssignedItemsCount));
            LocalizeMenuItem(Resource.Id.dashboard_your_team, SupervisorDashboard.YourTeam);
            LocalizeMenuItem(Resource.Id.dashboard_collected_interviews, SupervisorDashboard.CollectedInterviews);
            LocalizeMenuItem(Resource.Id.dashboard_waiting_decision, SupervisorDashboard.WaitingForAction, nameof(ViewModel.WaitingForDecisionCount));
            LocalizeMenuItem(Resource.Id.dashboard_outbox, SupervisorDashboard.Outbox, nameof(ViewModel.OutboxItemsCount));

            return view;
        }

        public override void OnDestroyView()
        {
            mvxNavigationService.AfterNavigate -= MenuFragment_AfterNavigate;
            base.OnDestroyView();
        }

        private void MenuFragment_AfterNavigate(object sender, MvvmCross.Navigation.EventArguments.NavigateEventArgs e)
        {
            int? menuItemId = null;
            switch (e.ViewModel)
            {
                case OutboxViewModel outbox:
                    menuItemId = Resource.Id.dashboard_outbox;
                    break;
                case ToBeAssignedItemsViewModel toBeAssignedItems:
                    menuItemId = Resource.Id.dashboard_to_be_assigned;
                    break;
                case WaitingForSupervisorActionViewModel waitingForSupervisorAction:
                    menuItemId = Resource.Id.dashboard_waiting_decision;
                    break;
            }

            if (menuItemId.HasValue)
                mvxMainThreadDispatcher.RequestMainThreadAction(() =>
                    this.SelectMenuItem(navigationView.Menu.FindItem(menuItemId.Value)));
        }

        private void LocalizeMenuItem(int id, string title, string viewModelPropertyName = null)
        {
            void SetLabelText(TextView textView, PropertyInfo viewModelPropertyInfo)
            {
                var value = ViewModel.GetPropertyValueAsString(viewModelPropertyInfo);
                if (value == "0")
                {
                    textView.Visibility = ViewStates.Gone;
                }
                else
                {
                    textView.Visibility = ViewStates.Visible;
                    textView.Text = value;
                }
            }

            var item = navigationView.Menu.FindItem(id);

            if (viewModelPropertyName != null)
            {
                item.SetActionView(Resource.Layout.dashboard_sidebar_counter);
                
                var textView = (TextView) item.ActionView;
                var viewModelPropertyInfo = ViewModel.GetType().GetProperty(viewModelPropertyName);

                SetLabelText(textView, viewModelPropertyInfo);

                ViewModel.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == viewModelPropertyName)
                    { 
                        SetLabelText(textView, viewModelPropertyInfo);
                    }
                };
            }

            item.SetTitle(title);
        }

        public bool OnNavigationItemSelected(IMenuItem item)
        {
            this.SelectMenuItem(item);
            this.Navigate(item.ItemId);

            return true;
        }

        private void SelectMenuItem(IMenuItem item)
        {
            previousMenuItem?.SetChecked(false);

            item.SetCheckable(true);
            item.SetChecked(true);

            previousMenuItem = item;
        }

        private async void Navigate(int itemId)
        {
            ((DashboardActivity)Activity).DrawerLayout.CloseDrawers();
            await Task.Delay(TimeSpan.FromMilliseconds(250));

            switch(itemId)
            {
                case Resource.Id.dashboard_to_be_assigned:
                    ViewModel.ShowToBeAssignedItems.Execute();
                    break;
                case Resource.Id.dashboard_waiting_decision:
                    ViewModel.ShowWaitingForActionItems.Execute();
                    break;
                case Resource.Id.dashboard_outbox:
                    ViewModel.ShowOutboxItems.Execute();
                    break;
            }
        }
    }
}