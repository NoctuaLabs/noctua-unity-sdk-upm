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
            Debug.Log("Continue button clicked");
        }

        public void LoadPlayers()
        {
            _players = Model.AllPlayers.Values.ToList();

            _playerListView.makeItem = () =>
            {
                // Instantiate the UXML template for the entry
                var newListEntry = _itemTemplate.Instantiate();

                // Return the root of the instantiated visual tree
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

        public void SetVisible(bool visible)
        {
            View.visible = visible;
        }
    }
}