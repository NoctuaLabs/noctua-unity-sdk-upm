using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

// Call Task
using System.Threading.Tasks;

namespace com.noctuagames.sdk.UI
{
    public class AccountSelectionDialogPresenter : Presenter<NoctuaBehaviour>
    {
        private VisualTreeAsset _itemTemplate;
        private ListView _gameAccountListView;
        private ListView _noctuaAccountListView;
        private readonly List<UserBundle> _gameUsers = new();
        private readonly List<UserBundle> _noctuaUsers = new();
        private Button _continueButton;

        protected override void Attach(){
            // Separate AccountList into Game Users (current game) and Noctua Users
            LoadData();
            // Render
            BindListView(_gameAccountListView, _gameUsers);
            BindListView(_noctuaAccountListView, _noctuaUsers);
        }
        protected override void Detach(){}

        public void Show()
        {
            // Separate AccountList into Game Users (current game) and Noctua Users
            LoadData();
            // Render
            BindListView(_gameAccountListView, _gameUsers);
            BindListView(_noctuaAccountListView, _noctuaUsers);

            Visible = true;
        }

        private void LoadData()
        {
            if (_gameAccountListView == null) {
                _gameAccountListView = View.Q<ListView>("GameAccountList");
            }

            if (_noctuaAccountListView == null) {
                _noctuaAccountListView = View.Q<ListView>("NoctuaAccountList");
            }

            var obj = Model.AuthService.RecentAccount;

            _gameUsers.Clear();
            var gameUsers = Model.AuthService.AccountList
                .Where(x => x.Value.PlayerAccounts.Any(y => y.BundleId == Application.identifier))
                .Select(x => x.Value)
                .ToList();
            
            _gameUsers.AddRange(gameUsers);

            
            if (_gameUsers.Count > 0)
            {
                _gameAccountListView.Rebuild();
            }
            else
            {
                _gameAccountListView.Clear();
            }

            _noctuaUsers.Clear();
            var noctuaUsers = Model.AuthService.AccountList
                .Where(x => x.Value.PlayerAccounts.All(y => y.BundleId != Application.identifier))
                .Select(x => x.Value)
                .ToList();

            _noctuaUsers.AddRange(noctuaUsers);
            
            if (_noctuaUsers.Count > 0)
            {
                _noctuaAccountListView.Rebuild();
            }
            else
            {
                _noctuaAccountListView.Clear();
            }
        }

        private void Awake()
        {
            LoadView();

            _itemTemplate = Resources.Load<VisualTreeAsset>("AccountItem");
            _gameAccountListView = View.Q<ListView>("GameAccountList");
            _noctuaAccountListView = View.Q<ListView>("NoctuaAccountList");
            _continueButton = View.Q<Button>("ContinueButton");
            _continueButton.RegisterCallback<ClickEvent>(OnContinueButtonClick);
        }

        private void OnContinueButtonClick(ClickEvent evt)
        {
            View.visible = false;

            // Use recent account as selectedAccount
            Model.ShowLoginOptionsDialogUI(Model.AuthService.RecentAccount);
        }

        private void BindListView(ListView listView, List<UserBundle> items)
        {
            listView.makeItem = () =>
            {
                var newListEntry = _itemTemplate.Instantiate();
                return newListEntry;
            };

            listView.bindItem = (element, index) => BindListViewItem(element, index, items);
            listView.fixedItemHeight = 40;
            listView.itemsSource = items;
            listView.selectionType = SelectionType.Single;
            listView.onItemsChosen += chosenItems =>
            {
                var chosenItem = chosenItems.First() as UserBundle;
                Debug.Log($"Selected {chosenItem?.User?.Nickname}");

                if (chosenItem.IsRecent) {
                    Model.ShowSwitchAccountConfirmationDialogUI(chosenItem);
                } else {
                    Model.AuthService.SwitchAccount(chosenItem);
                }
            };
        }

        private void BindListViewItem(VisualElement element, int index, List<UserBundle> items)
        {
            element.userData = items[index];
            
            element.Q<Label>("PlayerName").text = items[index] switch {
                {Player: {Username: not null } player} => player.Username,
                {User: {Nickname: not null } user} => user.Nickname,
                {Credential: {Provider: "device_id"}} => "Guest " + items[index].User?.Id,
                _ => "User " + items[index].User?.Id
            };
            
            element.Q<Label>("RecentLabel").text = items[index].User?.Id == Model.AuthService.RecentAccount.User.Id ? "Recent" : "";
        }
    }
}