using System;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class BindConfirmationDialogPresenter : Presenter<AuthenticationModel>
    {
        private readonly ILogger _log = new NoctuaLogger();
        private PlayerToken _bindTarget;
        private VisualElement _targetPlayerAvatar;
        private Label _guestDisplayName;
        private Label _targetDisplayName;
        private Button _connectButton;
        private Button _cancelButton;
        
        protected override void Attach()
        {
            
        }

        protected override void Detach()
        {
            
        }

        private void Start()
        {
            _targetPlayerAvatar = View.Q<VisualElement>("TargetPlayerAvatarImage");
            _guestDisplayName = View.Q<Label>("GuestDisplayName");
            _targetDisplayName = View.Q<Label>("TargetDisplayName");
            _connectButton = View.Q<Button>("ConnectButton");
            _cancelButton = View.Q<Button>("CancelButton");
            
            _connectButton.RegisterCallback<PointerUpEvent>(OnConnectButtonClicked);
            _cancelButton.RegisterCallback<PointerUpEvent>(OnCancelButtonClicked);
        }

        private async void OnConnectButtonClicked(PointerUpEvent evt)
        {
            _log.Debug("clicking connect button");
            
            try 
            {
                await Model.AuthService.BindGuestAndLoginAsync(_bindTarget);
                
                Visible = false;
            }
            catch (Exception e)
            {
                Model.ShowGeneralNotification(e.Message);
            }

            Visible = false;
        }

        private void OnCancelButtonClicked(PointerUpEvent evt)
        {
            _log.Debug("clicking cancel button");
            
            Visible = false;
        }

        public void Show(PlayerToken bindTarget)
        {
            _bindTarget = bindTarget;
            Visible = true;
            _guestDisplayName.text = $"Player {Model.AuthService.RecentAccount?.Player?.Id}";
            _targetDisplayName.text = _bindTarget?.Credential?.DisplayText;
            _targetPlayerAvatar.RemoveFromClassList("email-player-avatar");
            _targetPlayerAvatar.RemoveFromClassList("google-player-avatar");
            _targetPlayerAvatar.RemoveFromClassList("facebook-player-avatar");
            
            var avatarClass = _bindTarget?.Credential?.Provider switch
            {
                "email" => "email-player-avatar",
                "google" => "google-player-avatar",
                "facebook" => "facebook-player-avatar",
                _ => "email-player-avatar"
            };
            
            _targetPlayerAvatar.AddToClassList(avatarClass);
        }
    }
}