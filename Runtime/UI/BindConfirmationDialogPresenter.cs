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
        private Button _createNewButton;
        
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
            _createNewButton = View.Q<Button>("CreateNewButton");
            
            _connectButton.RegisterCallback<PointerUpEvent>(OnConnectButtonClicked);
            _createNewButton.RegisterCallback<PointerUpEvent>(OnCreateNewButtonClicked);
        }

        private async void OnConnectButtonClicked(PointerUpEvent evt)
        {
            _log.Debug("clicking connect button");
            
            try 
            {
                Model.ShowLoadingProgress(true);
                
                await Model.AuthService.BindGuestAndLoginAsync(_bindTarget);
            }
            catch (Exception e)
            {
                _log.Exception(e);

                Model.ShowError(e.Message);
            }
            finally
            {
                Model.ShowLoadingProgress(false);                
            }

            Visible = false;
        }

        private async void OnCreateNewButtonClicked(PointerUpEvent evt)
        {
            _log.Debug("clicking cancel button");

            try
            {
                Model.ShowLoadingProgress(true);

                await Model.AuthService.ExchangeTokenAsync(_bindTarget.AccessToken);
            }
            catch (Exception e)
            {
                _log.Exception(e);

                Model.ShowError(e.Message);
            }
            finally
            {
                Model.ShowLoadingProgress(false);
            }
            
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