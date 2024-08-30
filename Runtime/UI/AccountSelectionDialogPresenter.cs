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
        private VisualElement _separator;
        private ListView _noctuaAccountListView;
        private readonly List<UserBundle> _gameUsers = new();
        private readonly List<UserBundle> _noctuaUsers = new();
        private Button _continueButton;
        private Button _closeButton;

        protected override void Attach()
        {
            // Separate AccountList into Game Users (current game) and Noctua Users
            LoadData();
            // Render
            BindListView(_gameAccountListView, _gameUsers);
            BindListView(_noctuaAccountListView, _noctuaUsers);
        }

        protected override void Detach()
        {
        }

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

            _separator.style.display = _noctuaUsers.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Awake()
        {
            LoadView();

            _itemTemplate = Resources.Load<VisualTreeAsset>("AccountItem");
            _gameAccountListView = View.Q<ListView>("GameAccountList");
            _separator = View.Q<VisualElement>("Separator");
            _noctuaAccountListView = View.Q<ListView>("NoctuaAccountList");
            _continueButton = View.Q<Button>("ContinueButton");
            _continueButton.RegisterCallback<ClickEvent>(OnContinueButtonClick);
            _closeButton = View.Q<Button>("CloseButton");
            _closeButton.RegisterCallback<ClickEvent>(OnCloseButtonClick);
        }

        private void OnContinueButtonClick(ClickEvent evt)
        {
            View.visible = false;

            // Use recent account as selectedAccount
            Model.ShowLoginOptions(Model.AuthService.RecentAccount);
        }

        private void OnCloseButtonClick(ClickEvent evt)
        {
            Visible = false;
        }

        private void BindListView(ListView listView, List<UserBundle> items)
        {
            listView.makeItem = _itemTemplate.Instantiate;
            listView.bindItem = (element, index) => BindListViewItem(element, index, items);
            listView.fixedItemHeight = 40;
            listView.itemsSource = items;
            listView.selectionType = SelectionType.Single;
        }

        private void BindListViewItem(VisualElement element, int index, List<UserBundle> items)
        {
            element.userData = items[index];
            element.RegisterCallback<PointerUpEvent>(_ =>
            {
                var selectedAccount = items[index];
                
                Debug.Log($"Selected {selectedAccount?.User?.Nickname}");

                if (selectedAccount is { IsRecent: true })
                {
                    Model.AuthService.SwitchAccount(selectedAccount);
                }
                else
                {
                    Model.ShowSwitchAccountConfirmation(selectedAccount);
                }

                View.visible = false;
            
            });

            element.Q<Label>("PlayerName").text = items[index].DisplayName;

            element.Q<Label>("RecentLabel").text =
                items[index].User?.Id == Model.AuthService.RecentAccount.User.Id ? "Recent" : "";
        }
    }
}