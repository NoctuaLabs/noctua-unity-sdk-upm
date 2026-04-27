using System;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    /// <summary>
    /// Presenter for the switch account confirmation dialog, asking the user to confirm before switching to a different account.
    /// </summary>
    internal class SwitchAccountConfirmationDialogPresenter : Presenter<AuthUIController>
    {
        private readonly ILogger _log = new NoctuaLogger();
        
        private UserBundle _recentAccount;

        /// <summary>
        /// Displays the switch account confirmation dialog for the specified user.
        /// </summary>
        /// <param name="obj">The target user account to switch to.</param>
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