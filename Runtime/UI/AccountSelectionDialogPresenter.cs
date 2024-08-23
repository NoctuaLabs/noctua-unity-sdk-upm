using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public class AccountSelectionDialogPresenter : Presenter<NoctuaAuthService>
    {
        private VisualTreeAsset _itemTemplate;
        private ListView _playerListView;
        private List<UserBundle> _players;
        private Button _continueButton;

        protected override void Attach()
        {
            
        }

        protected override void Detach()
        {
            
        }

        private void Awake()
        {
            LoadView();
            
            _itemTemplate = Resources.Load<VisualTreeAsset>("AccountItem");
            _playerListView = View.Q<ListView>("NoctuaAccountSelectionList");
            _continueButton = View.Q<Button>("NoctuaAccountSelectionContinueButton");
            _continueButton.RegisterCallback<ClickEvent>(OnContinueButtonClick);
            
            // View.visible = false;
        }

        private void Start()
        {
            LoadPlayers();
        }

        private void OnEnable()
        {
            View.Focus();
        }

        private void OnContinueButtonClick(ClickEvent evt)
        {
            View.visible = false;
            
        }

        public void LoadPlayers()
        {
            _players = Model.AccountList.Values.ToList();

            _playerListView.makeItem = () =>
            {
                var newListEntry = _itemTemplate.Instantiate();
                return newListEntry;
            };

            _playerListView.fixedItemHeight = 40;
            
            _playerListView.bindItem = (element, i) =>
            {
                if (_players[i]?.Player?.Username != null && _players[i]?.Player?.Username.Length > 0)
                {
                    // Use player username from in-game if possible
                    element.Q<Label>("NoctuaPlayerName").text = _players[i]?.Player?.Username;
                } else if (_players[i]?.User?.Nickname != null && _players[i]?.User?.Nickname.Length > 0)
                {
                    // Fallback to user's nickname if the player username is not available
                    element.Q<Label>("NoctuaPlayerName").text = _players[i]?.User?.Nickname;
                } else if (_players[i]?.Credential?.Provider == "device_id") {
                    // Fallback to prefix guest
                    element.Q<Label>("NoctuaPlayerName").text = "Guest " + _players[i].User?.Id.ToString();
                } else {
                    // Fallback to prefix user
                    element.Q<Label>("NoctuaPlayerName").text = "User " + _players[i].User?.Id.ToString();
                }
                element.Q<Button>("NoctuaRecentLabel").text = _players[i].User.Id == Model.RecentAccount.User.Id ? "Recent" : "";
            };
            
            _playerListView.itemsSource = _players;
        }
    }
}