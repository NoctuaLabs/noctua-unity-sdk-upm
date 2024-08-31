using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class UserCenterPresenter : Presenter<NoctuaAuthenticationBehaviour>
    {
        private VisualTreeAsset _itemTemplate;
        private ListView _credentialListView;
        
        private readonly List<UserCredential> _credentials = new()
        {
            new UserCredential
            {
                CredentialIconStyle = "email-icon",
                CredentialProvider = CredentialProvider.Email
            },
            new UserCredential
            {
                CredentialIconStyle = "google-logo",
                CredentialProvider = CredentialProvider.Google
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
            // _itemTemplate = Resources.Load<VisualTreeAsset>("ConnectAccountItem");
            // BindListView();
        }
        
        private void BindListView()
        {
            _credentialListView = View.Q<ListView>("AccountList");
            _credentialListView.makeItem = _itemTemplate.Instantiate;
            _credentialListView.bindItem = BindListViewItem;
            _credentialListView.fixedItemHeight = 40;
            _credentialListView.itemsSource = _credentials;
            _credentialListView.selectionType = SelectionType.Single;
        }

        private void BindListViewItem(VisualElement element, int index)
        {
            element.userData = _credentials[index];
            
            if (string.IsNullOrEmpty(_credentials[index].Username))
            {
                element.Q<Label>("UsernameLabel").AddToClassList("hide");
                element.Q<Button>("ConnectButton").RemoveFromClassList("hide");
                element.Q<Button>("ConnectButton").RegisterCallback<PointerUpEvent, UserCredential>(OnConnectButtonClick, _credentials[index]);
            }
            else
            {
                element.Q<Button>("ConnectButton").AddToClassList("hide");
                element.Q<Label>("UsernameLabel").text = _credentials[index].Username;
            }
            
            element.Q<VisualElement>("MethodLogo").ClearClassList();
            element.Q<VisualElement>("MethodLogo").AddToClassList("method-logo");
            element.Q<VisualElement>("MethodLogo").AddToClassList(_credentials[index].CredentialIconStyle);
        }
        
        private void OnConnectButtonClick(PointerUpEvent evt, UserCredential credential)
        {
            Debug.Log($"Selected {credential?.CredentialProvider}");

            if (credential == null) return;
                
            switch (credential.CredentialProvider)
            {
                case CredentialProvider.Email:
                    Model.ShowEmailLogin(result =>
                    {
                        Debug.Log($"ShowEmailLogin: {result.Success}");
                        
                        _credentials[0].Username = result.Success ? result.User?.DisplayName : "";
                        _credentialListView.Rebuild();
                    });
                    break;
                case CredentialProvider.Google:
                    Model.ShowSocialLogin("google", result =>
                    {
                        Debug.Log($"ShowSocialLogin: {result.Success}");

                        _credentials[1].Username = result.Success ? result.User?.DisplayName : "";
                        _credentialListView.Rebuild();
                    });
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private enum CredentialProvider
        {
            Email,
            Google
        }

        private class UserCredential
        {
            public string Username;
            public string CredentialIconStyle;
            public CredentialProvider CredentialProvider;
        }
    }
}