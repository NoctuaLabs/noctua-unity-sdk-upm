using System;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public class SwitchAccountConfirmationDialogPresenter : Presenter<NoctuaBehaviour>
    {

        private UserBundle _recentAccount;

        public void Show(UserBundle obj)
        {
            _recentAccount = obj;
            Visible = true;
        }

        protected override void Attach(){}
        protected override void Detach(){}
        
        private void Awake()
        {
            LoadView();
            
            View.Q<Button>("ConfirmButton").clicked += () =>
            {
                // TODO call AuthService to switch account
                Model.ShowAccountSelection();
                Visible = false;
            };
            
            View.Q<Button>("CancelButton").clicked += () =>
            {
                Visible = false;
            };
        }
    }
}