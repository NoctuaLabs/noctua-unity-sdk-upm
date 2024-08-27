using System;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public class SwitchAccountConfirmationDialogPresenter : Presenter<NoctuaBehaviour>
    {

        private UserBundle _recentAccount;

        protected override void Attach()
        {
        }

        protected override void Detach()
        {
        }

        public void Show(UserBundle obj)
        {
            _recentAccount = obj;
            Visible = true;
        }
        
        private void Awake()
        {
            LoadView();
            
            View.Q<Button>("ConfirmButton").clicked += () =>
            {
                // TODO call AuthService to switch account
                Model.AuthService.SwitchAccount(_recentAccount);
                Visible = false;
            };
            
            View.Q<Button>("CancelButton").clicked += () =>
            {
                Visible = false;
            };
        }
    }
}