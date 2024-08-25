using System;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public class SwitchAccountConfirmationPresenter : Presenter<AccountSelection>
    {
        protected override void Attach()
        {
            Model.OnAccountSelected += OnAccountSelected;
            Model.OnAccountSwitched += OnAccountSwitched;
        }

        protected override void Detach()
        {
            Model.OnAccountSelected -= OnAccountSelected;
            Model.OnAccountSwitched -= OnAccountSwitched;
        }
        
        private void Awake()
        {
            LoadView();
            
            View.Q<Button>("ConfirmButton").clicked += () =>
            {
                Model.SwitchAccount(Model.SelectedAccount);
            };
            
            View.Q<Button>("CancelButton").clicked += () =>
            {
                Visible = false;
            };
        }
        
        private void OnAccountSwitched(UserBundle obj)
        {
            Visible = false;
        }

        private void OnAccountSelected(UserBundle obj)
        {
            Visible = true;
        }
    }
}