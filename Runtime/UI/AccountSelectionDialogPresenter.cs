using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UIElements;

// Call Task

namespace com.noctuagames.sdk.UI
{
    internal class AccountSelectionDialogPresenter : Presenter<AuthenticationModel>
    {
        private VisualTreeAsset _itemTemplate;
        private ListView _gameAccountListView;
        private VisualElement _separator;
        private ListView _noctuaAccountListView;
        private readonly List<UserBundle> _gameUsers = new();
        private readonly List<UserBundle> _noctuaUsers = new();
        private Button _continueButton;
        private Button _closeButton;
        private readonly ILogger _log = new NoctuaLogger(typeof(AccountSelectionDialogPresenter));
        private GlobalConfig _config;
        private Label _sdkVersion;

        protected override void Attach()
        {
        }

        protected override void Detach()
        {
        }

        public async void Show()
        {

            Model.ShowLoadingProgress(true);
            var isOffline = await Noctua.IsOfflineAsync();
            Model.ShowLoadingProgress(false);
            if (isOffline)
            {
                var offlineModeMessage = Noctua.Platform.Locale.GetTranslation(LocaleTextKey.OfflineModeMessage) + " [AccountSelection]";
                Model.HandleRetryAccountSelectionAsync(offlineModeMessage);
                return;
            }

            LoadData();
            Model.ClearNavigation();
            Model.AuthIntention = AuthIntention.Switch;
            Visible = true;
            _log.Debug("Loading and showing user");
        }

        private void LoadData()
        {
            _gameUsers.Clear();

            var currentGameAccountList = IsVNLegalPurposeEnabled() ? Model.AuthService.CurrentGameAccountList.Where(user => !user.IsGuest) : Model.AuthService.CurrentGameAccountList;
            _gameUsers.AddRange(currentGameAccountList);
            _gameAccountListView.Rebuild();

            _noctuaUsers.Clear();

            var otherGamesAccountList = IsVNLegalPurposeEnabled() ? Model.AuthService.OtherGamesAccountList.Where(user => !user.IsGuest) : Model.AuthService.OtherGamesAccountList;

            _noctuaUsers.AddRange(otherGamesAccountList);
            _noctuaAccountListView.Rebuild();

            _separator.style.display = _noctuaUsers.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Start()
        {
            _itemTemplate = Resources.Load<VisualTreeAsset>("AccountItem");
            _gameAccountListView = View.Q<ListView>("GameAccountList");
            _separator = View.Q<VisualElement>("Separator");
            _noctuaAccountListView = View.Q<ListView>("NoctuaAccountList");
            _continueButton = View.Q<Button>("ContinueButton");
            _continueButton.RegisterCallback<PointerUpEvent>(OnContinueButtonClick);
            _closeButton = View.Q<Button>("CloseButton");
            _closeButton.RegisterCallback<PointerUpEvent>(OnCloseButtonClick);
            _sdkVersion = View.Q<Label>("SDKVersion");
            _sdkVersion.text = $"v{Assembly.GetExecutingAssembly().GetName().Version}";
            
            BindListView(_gameAccountListView, _gameUsers);
            BindListView(_noctuaAccountListView, _noctuaUsers);
        }

        public void SetWhitelabel(GlobalConfig config)
        {
            _config = config;

            if (string.IsNullOrEmpty(_config?.CoPublisher?.CompanyName)) return;

            var logo = Utility.GetCoPublisherLogo(_config?.CoPublisher?.CompanyName);

            var defaultLogo = Resources.Load<Texture2D>(logo);
            View.Q<VisualElement>("NoctuaLogoWithText").style.backgroundImage = new StyleBackground(defaultLogo);
        }

        private void OnContinueButtonClick(PointerUpEvent evt)
        {
            _log.Debug("clicking continue button");
            
            View.visible = false;
            
            Model.PushNavigation(() => Model.ShowAccountSelection());
            if(IsVNLegalPurposeEnabled())
            {
                Model.ShowEmailLogin(null);
            }
            else
            {
                Model.ShowLoginOptions();
            }
        }

        private void OnCloseButtonClick(PointerUpEvent evt)
        {
            _log.Debug("clicking close button");
            
            Visible = false;
            
            Model.ClearNavigation();
            Model.AuthIntention = AuthIntention.None;
        }

        private void BindListView(ListView listView, List<UserBundle> items)
        {
            listView.makeItem = _itemTemplate.Instantiate;
            listView.bindItem = (element, index) => BindListViewItem(element, index, items);
            listView.fixedItemHeight = 40;
            listView.itemsSource = items;
            listView.selectionType = SelectionType.None;
        }

        private void BindListViewItem(VisualElement element, int index, List<UserBundle> items)
        {
            element.userData = items[index];
            
            var timeout = false;
            
            void TimerCallback(object _)
            {
                timeout = true;

                _log.Debug($"held down player '{items[index]?.Player?.Id}' for 3 seconds");

                UniTask.Void(
                    async () =>
                    {
                        await UniTask.SwitchToMainThread();

                        var textToCopy = $"{{"                                            +
                            $"\"userId\":\"{items[index]?.User?.Id}\","                   +
                            $"\"playerId\":\"{items[index]?.Player?.Id}\","               +
                            $"\"provider\":\"{items[index]?.Credential?.Provider}\","     +
                            $"\"credentialId\":\"{items[index]?.Credential?.Id}\","       +
                            $"\"credential\":\"{items[index]?.Credential?.DisplayText}\"" +
                            $"}}";

                        Model.ShowGeneralNotification(
                            $"Player '{items[index]?.Player?.Id}' data copied to clipboard",
                            true,
                            7000
                        );

                        GUIUtility.systemCopyBuffer = textToCopy;
                    }
                );
            }
            
            var holdTimer = new Timer(TimerCallback, null, Timeout.Infinite, Timeout.Infinite);
            
            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                timeout = false;
                holdTimer.Change(3000, Timeout.Infinite);
            });

            element.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (timeout)
                {
                    return;
                }
                
                holdTimer.Change(Timeout.Infinite, Timeout.Infinite);

                var selected = items[index];
                
                _log.Info($"selected {selected?.Credential?.DisplayText} - {selected?.User?.Id} - {selected?.Player?.Id}");

                if (selected is { IsRecent: false })
                {
                    Model.ShowSwitchAccountConfirmation(selected);
                }

                View.visible = false;
            });
            
            element.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                timeout = false;
                holdTimer.Change(Timeout.Infinite, Timeout.Infinite);
            });

            element.Q<Label>("PlayerName").text = items[index].DisplayName;
            _log.Debug(items[index].DisplayName);
            var logoClass = items?[index].Credential.Provider switch
            {
                "google" => "google-player-avatar",
                "facebook" => "facebook-player-avatar",
                "email" => "email-player-avatar",
                _ => "guest-player-avatar"
            };
            
            element.Q<VisualElement>("PlayerLogo").ClearClassList();
            element.Q<VisualElement>("PlayerLogo").AddToClassList(logoClass);

            var isActive = items?[index].User?.Id == Model.AuthService.RecentAccount?.User.Id; 
            element.Q<Label>("RecentLabel").style.display = isActive ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private bool IsVNLegalPurposeEnabled()
        {
            return _config?.Noctua?.RemoteFeatureFlags?.ContainsKey("vnLegalPurposeEnabled") == true && _config?.Noctua?.RemoteFeatureFlags?["vnLegalPurposeEnabled"] == true;
        }
    }
}
