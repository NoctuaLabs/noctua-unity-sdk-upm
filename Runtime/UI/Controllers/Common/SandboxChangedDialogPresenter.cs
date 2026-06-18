using System;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.UI
{
    /// <summary>
    /// Presenter for the sandbox-changed dialog, shown when the runtime sandbox override
    /// resolves to a value different from the one this session was wired with. Acknowledging
    /// the dialog quits the application so the next launch picks up the new value.
    /// Kept separate from <see cref="StartGameErrorDialogPresenter"/> so each can evolve
    /// its own copy and behaviour independently.
    /// </summary>
    internal class SandboxChangedDialogPresenter : Presenter<object>
    {
        private Button _restartButton;
        private Label _messageLabel;

        private readonly ILogger _log = new NoctuaLogger();

        private UniTaskCompletionSource _tcs;

        protected override void Attach()
        {}

        protected override void Detach()
        {}

        private void Start()
        {
            _restartButton = View.Q<Button>("RestartButton");
            _messageLabel = View.Q<Label>("MessageLabel");

            _restartButton.RegisterCallback<PointerUpEvent>(OnRestartButton);
        }

        /// <summary>
        /// Displays the sandbox-changed dialog with the given message and quits the
        /// application after acknowledgment.
        /// </summary>
        /// <param name="message">The message describing the sandbox change.</param>
        public async UniTask Show(string message)
        {
            _tcs = new UniTaskCompletionSource();

            if (!string.IsNullOrEmpty(message))
            {
                _messageLabel.text = message;
            }

            Visible = true;

            await _tcs.Task;

            Application.Quit();
        }

        private void OnRestartButton(PointerUpEvent evt)
        {
            _log.Debug("clicking restart button");

            Visible = false;

            _tcs?.TrySetResult();
        }
    }
}
