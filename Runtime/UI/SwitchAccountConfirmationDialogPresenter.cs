﻿using System;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class SwitchAccountConfirmationDialogPresenter : Presenter<AuthenticationModel>
    {

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
                await Model.AuthService.SwitchAccountAsync(_recentAccount);
                Visible = false;
            });
            
            View.Q<Button>("CancelButton").RegisterCallback<PointerUpEvent>(_ =>
            {
                Visible = false;
            });
        }
    }
}