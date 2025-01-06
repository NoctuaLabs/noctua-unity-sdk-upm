using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class BindConflictDialogPresenter : Presenter<AuthenticationModel>
    {
        private readonly ILogger _log = new NoctuaLogger();
        private Button _switchAccountButton;
        private Button _cancelButton;
        private VisualElement _playerAvatarImage;
        private Label _playerLabel;
        private Label _credentialDisplayText;
        private PlayerToken _targetPlayer;

        protected override void Attach()
        {
            
        }

        protected override void Detach()
        {
            
        }
        
        private void Start()
        {
            _playerAvatarImage = View.Q<VisualElement>("PlayerAvatarImage");
            _playerLabel = View.Q<Label>("PlayerLabel");
            _credentialDisplayText = View.Q<Label>("CredentialDisplayText");
            _switchAccountButton = View.Q<Button>("SwitchAccountButton");
            _cancelButton = View.Q<Button>("CancelButton");
            
            _switchAccountButton.RegisterCallback<PointerUpEvent>(_ => OnSwitchAccountButtonClicked());
            _cancelButton.RegisterCallback<PointerUpEvent>(_ => OnCancelButtonClicked());
        }
        
        public void Show(PlayerToken targetPlayer)
        {
            _targetPlayer = targetPlayer;
            
            var avatarClass = _targetPlayer.Credential?.Provider switch
            {
                "google" => "google-player-avatar",
                "facebook" => "facebook-player-avatar",
                _ => "email-player-avatar"
            };
            
            _playerAvatarImage.ClearClassList();
            _playerAvatarImage.AddToClassList(avatarClass);
            
            _playerLabel.text = $"User {targetPlayer.User?.Id}";
            _credentialDisplayText.text = targetPlayer.Credential?.DisplayText;
            
            Visible = true;
        }

        private void OnCancelButtonClicked()
        {
            _log.Debug("clicking cancel button");
            
            Visible = false;
        }

        private void OnSwitchAccountButtonClicked()
        {
            _log.Debug("clicking switch account button");

            Model.AuthService.LoginWithToken(_targetPlayer);

            Visible = false;
        }
    }
}