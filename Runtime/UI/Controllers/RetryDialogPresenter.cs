using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.UI
{
    /// <summary>
    /// Presenter for the retry dialog, displayed when an operation fails and the user can choose to retry, contact customer service, or dismiss.
    /// </summary>
    internal class RetryDialogPresenter : Presenter<object>
    {
        private Button _btnRetry;
        private Button _btnClose;
        private Label _message;
        private string _context;
        private Label _csInfo;

        private readonly ILogger _log = new NoctuaLogger(typeof(RetryDialogPresenter));

        private UniTaskCompletionSource<bool> _tcs;

        protected override void Attach()
        {}

        protected override void Detach()
        {}

        private void Start()
        {
            _btnRetry = View.Q<Button>("RetryButton");
            _btnClose = View.Q<Button>("ExitButton");
            _message = View.Q<Label>("Info");
            _csInfo = View.Q<Label>("CSInfo");

            _btnRetry.RegisterCallback<PointerUpEvent>(RetryProcess);
            _csInfo.RegisterCallback<PointerUpEvent>(OpenCS);
            _btnClose.RegisterCallback<PointerUpEvent>(CloseDialog);
        }

        /// <summary>
        /// Displays the retry dialog with the given error message and waits for the user's choice.
        /// </summary>
        /// <param name="message">The error message to display.</param>
        /// <param name="context">The context identifier for customer service routing.</param>
        /// <returns><c>true</c> if the user chose to retry; <c>false</c> if dismissed.</returns>
        public async UniTask<bool> Show(string message, string context = "general")
        {            
            _tcs = new UniTaskCompletionSource<bool>();

            Visible = true;

            _message.text = message;
            _context = context;

            return await _tcs.Task;
        }

        private void RetryProcess(PointerUpEvent evt)
        {            
            Visible = false;

            _tcs?.TrySetResult(true);
        }

        private async void OpenCS(PointerUpEvent evt)
        {
            _log.Debug("clicking customer Service button");
            
            try
            {
                Visible = false;
                _tcs?.TrySetResult(false);

                await Noctua.Platform.Content.ShowCustomerService(_context);
            } 
            catch (Exception e) {

                Visible = false;
                _tcs?.TrySetResult(false);

                if (e is NoctuaException noctuaEx)
                {
                    _log.Error("NoctuaException: " + noctuaEx.ErrorCode + " : " + noctuaEx.Message);
                } else {
                    _log.Error("Exception: " + e);
                }
            }
        }

        private void CloseDialog(PointerUpEvent evt)
        { 
            _log.Debug("On close dialog");

            Visible = false;

            _tcs?.TrySetResult(false);
        }
    }
}
