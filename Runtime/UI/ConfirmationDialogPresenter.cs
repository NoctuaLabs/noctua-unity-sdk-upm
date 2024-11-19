using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class ConfirmationDialogPresenter : Presenter<object>
    {
        private Button _btnCS;
        private Label _note;
        protected override void Attach()
        {}

        protected override void Detach()
        {}

        private void Start()
        {
            _btnCS = View.Q<Button>("CSButton");
            _note = View.Q<Label>("Note");

            _btnCS.RegisterCallback<PointerUpEvent>(OpenCS);
        }

        public void Show(string regionCode)
        {
            Visible = true;

            _btnCS.text = Utility.GetTranslation("Title.Text.ContactSupport",  Utility.LoadTranslations(regionCode));
            _note.text = Utility.GetTranslation("User.Banned.Info",  Utility.LoadTranslations(regionCode));

        }

        private async void OpenCS(PointerUpEvent evt)
        {
            try
            {
                Visible = false;

                await Noctua.Platform.Content.ShowCustomerService();

                Debug.Log("Customer Service URL opened");
            } 
            catch (Exception e) {

                Visible = false;

                if (e is NoctuaException noctuaEx)
                {
                    Debug.Log("NoctuaException: " + noctuaEx.ErrorCode + " : " + noctuaEx.Message);
                } else {
                    Debug.Log("Exception: " + e);
                }
            }
        }
    }
}
