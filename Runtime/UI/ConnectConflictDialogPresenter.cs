using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class ConnectConflictDialogPresenter : Presenter<AuthenticationModel>
    {
        private readonly ILogger _log = new NoctuaLogger();
        private Button _switchAccountButton;
        private Button _cancelButton;
        private VisualElement _playerAvatarImage;
        private Label _userNickname;
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
            _userNickname = View.Q<Label>("UserNickname");
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
            
            _playerAvatarImage.RemoveFromClassList("google-player-avatar");
            _playerAvatarImage.RemoveFromClassList("facebook-player-avatar");
            _playerAvatarImage.RemoveFromClassList("email-player-avatar");
            _playerAvatarImage.AddToClassList(avatarClass);
            
            _userNickname.text = targetPlayer.User?.Nickname;
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