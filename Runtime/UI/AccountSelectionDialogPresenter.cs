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
        private List<Player> _players;
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
            _players = Model.AllPlayers.Values.ToList();

            _playerListView.makeItem = () =>
            {
                var newListEntry = _itemTemplate.Instantiate();
                return newListEntry;
            };

            _playerListView.fixedItemHeight = 40;
            
            _playerListView.bindItem = (element, i) =>
            {
                element.Q<Label>("NoctuaPlayerName").text = _players[i].User.Nickname;
                element.Q<Button>("NoctuaRecentLabel").text = _players[i].UserId == Model.Player.UserId ? "Recent" : "";
            };
            
            _playerListView.itemsSource = _players;
        }
    }
}