using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.UI
{
    internal class BannedConfirmationDialogPresenter : Presenter<object>
    {
        private Button _btnOK;
        private Label _note;
        private Label _note2;

        private readonly ILogger _log = new NoctuaLogger(typeof(BannedConfirmationDialogPresenter));

        private UniTaskCompletionSource<bool> _tcs;

        protected override void Attach()
        {}

        protected override void Detach()
        {}

        private void Start()
        {
            _btnOK = View.Q<Button>("OKButton");
            _note = View.Q<Label>("Note");
            _note2 = View.Q<Label>("Note2");

            _note2.RegisterCallback<PointerUpEvent>(OpenCS);
            _btnOK.RegisterCallback<PointerUpEvent>(CloseDialog);
        }

        public async UniTask<bool> Show(string language)
        {
            _tcs = new UniTaskCompletionSource<bool>();

            Visible = true;

            _note.text = Utility.GetTranslation("User.Banned.Info",  Utility.LoadTranslations(language));
            _note2.text = Utility.GetTranslation("User.Banned.Info2",  Utility.LoadTranslations(language));

            return await _tcs.Task;
        }

        private async void OpenCS(PointerUpEvent evt)
        {
            _log.Debug("clicking customer Service button");
            
            try
            {
                Visible = false;

                await Noctua.Platform.Content.ShowCustomerService("user_get_banned");

                _log.Info("Customer Service URL opened");

                _tcs?.TrySetResult(true);

            } 
            catch (Exception e) {

                Visible = false;

                _tcs?.TrySetResult(true);

                if (e is NoctuaException noctuaEx)
                {
                    _log.Info("NoctuaException: " + noctuaEx.ErrorCode + " : " + noctuaEx.Message);
                } else {
                    _log.Info("Exception: " + e);
                }
            }
        }

        private void CloseDialog(PointerUpEvent evt)
        {
            _log.Debug("clicking OK button");
            
            Visible = false;
            _tcs?.TrySetResult(true);
        }
    }
}
