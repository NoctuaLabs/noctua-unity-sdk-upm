using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public class AccountSelectionDialogPresenter : Presenter<AccountSelection>
    {
        private VisualTreeAsset _itemTemplate;
        private ListView _gameAccountListView;
        private ListView _noctuaAccountListView;
        private readonly List<UserBundle> _gameUsers = new();
        private readonly List<UserBundle> _noctuaUsers = new();
        private Button _continueButton;

        protected override void Attach()
        {
            BindListView(_gameAccountListView, _gameUsers);
            BindListView(_noctuaAccountListView, _noctuaUsers);
            Model.AuthService.OnAuthenticated += RefreshItems;
            Model.OnAccountSelectionRequested += OnAccountSelectionRequested;
        }

        protected override void Detach()
        {
            Model.AuthService.OnAuthenticated -= RefreshItems;
            Model.OnAccountSelectionRequested -= OnAccountSelectionRequested;
        }

        private void RefreshItems(UserBundle obj)
        {
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
            Model.RequestLoginOptions();
        }

        private void OnAccountSelectionRequested()
        {
            View.visible = true;
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
                
                Model.SelectAccount(chosenItem);
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