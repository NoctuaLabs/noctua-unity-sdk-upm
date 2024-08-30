using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public class UserCenterPresenter : Presenter<NoctuaBehaviour>
    {
        private VisualTreeAsset _itemTemplate;

        private readonly List<UserCredential> _userCredentials = new()
        {
            new UserCredential
            {
                CredentialIconStyle = "email-icon",
                CredentialProvider = "Email"
            },
            new UserCredential
            {
                CredentialIconStyle = "google-logo",
                CredentialProvider = "Google"
            },
        };
        
        protected override void Attach()
        {
        }

        protected override void Detach()
        {
        }

        private void Awake()
        {
            LoadView();
            _itemTemplate = Resources.Load<VisualTreeAsset>("ConnectAccountItem");
            BindListView(View.Q<ListView>("AccountList"), Model.AuthService.RecentAccount.Credential);
        }
        
        private void BindListView(ListView listView, List<UserBundle> items)
        {
            listView.makeItem = _itemTemplate.Instantiate;
            listView.bindItem = (element, index) => BindListViewItem(element, index, items);
            listView.fixedItemHeight = 40;
            listView.itemsSource = items;
            listView.selectionType = SelectionType.Single;
        }

        private void BindListViewItem(VisualElement element, int index, List<UserCredential> items)
        {
            element.userData = items[index];
            element.RegisterCallback<PointerUpEvent>(_ =>
            {
            //     var selectedAccount = items[index];
            //     
            //     Debug.Log($"Selected {selectedAccount?.User?.Nickname}");
            //
            //     if (selectedAccount is { IsRecent: true })
            //     {
            //         Model.AuthService.SwitchAccount(selectedAccount);
            //     }
            //     else
            //     {
            //         Model.ShowSwitchAccountConfirmationDialogUI(selectedAccount);
            //     }
            //
            //     View.visible = false;
            //
            // });
            //
            // element.Q<Label>("PlayerName").text = items[index] switch
            // {
            //     { Player: { Username: not null } player } => player.Username,
            //     { User: { Nickname: not null } user } => user.Nickname,
            //     { Credential: { Provider: "device_id" } } => "Guest " + items[index].User?.Id,
            //     _ => "User " + items[index].User?.Id
            // };
            //
            // element.Q<Label>("RecentLabel").text =
            //     items[index].User?.Id == Model.AuthService.RecentAccount.User.Id ? "Recent" : "";
        }
        
        private class UserCredential
        {
            public string Username;
            public string CredentialIconStyle;
            public string CredentialProvider;
        }
    }
}