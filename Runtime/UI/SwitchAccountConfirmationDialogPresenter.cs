using System;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class SwitchAccountConfirmationDialogPresenter : Presenter<AuthenticationModel>
    {
        private readonly ILogger _log = new NoctuaLogger();
        
        private UserBundle _recentAccount;

        public void Show(UserBundle obj)
        {
            _recentAccount = obj;
            Visible = true;
        }

        protected override void Attach(){}
        protected override void Detach(){}
        
        private void Start()
        {
            View.Q<Button>("ConfirmButton").RegisterCallback<PointerUpEvent>(async _ =>
            {
                _log.Debug("clicking confirm button");
                
                try 
                {
                    await Model.AuthService.SwitchAccountAsync(_recentAccount);
                }
                catch (Exception e)
                {
                    Model.ShowGeneralNotification(e.Message);
                }

                Visible = false;
            });
            
            View.Q<Button>("CancelButton").RegisterCallback<PointerUpEvent>(_ =>
            {
                _log.Debug("clicking cancel button");
                
                Visible = false;
            });
        }
    }
}