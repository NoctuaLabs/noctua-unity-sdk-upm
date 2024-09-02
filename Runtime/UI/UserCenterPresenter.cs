using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
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

        private void SetOrientation()
        {
            
            if (Screen.width > Screen.height)
            {
                View.style.flexDirection = FlexDirection.Row;
                View.style.justifyContent = Justify.FlexEnd;
                
                View.Q<VisualElement>("UserCenter").RemoveFromClassList("portrait");
                View.Q<VisualElement>("UserProfile").RemoveFromClassList("portrait");
                View.Q<VisualElement>("ConnectAccount").RemoveFromClassList("portrait");
                
                View.Q<VisualElement>("UserCenter").AddToClassList("landscape");
                View.Q<VisualElement>("UserProfile").AddToClassList("landscape");
                View.Q<VisualElement>("ConnectAccount").AddToClassList("landscape");
            }
            else
            {
                View.style.flexDirection = FlexDirection.Column;
                View.style.justifyContent = Justify.FlexEnd;
                
                View.Q<VisualElement>("UserCenter").RemoveFromClassList("landscape");
                View.Q<VisualElement>("UserProfile").RemoveFromClassList("landscape");
                View.Q<VisualElement>("ConnectAccount").RemoveFromClassList("landscape");
                
                View.Q<VisualElement>("UserCenter").AddToClassList("portrait");
                View.Q<VisualElement>("UserProfile").AddToClassList("portrait");
                View.Q<VisualElement>("ConnectAccount").AddToClassList("portrait");
            }
        }

        public void Show()
        {
            Visible = true;
            
            View.Q<VisualElement>("MoreOptionsMenu").AddToClassList("hide");
            _credentialListView.Rebuild();
            SetOrientation();
            StartCoroutine(GetCurrentUser().ToCoroutine());
        }

        private async UniTask GetCurrentUser()
        {
            try
            {
                var user = await Model.AuthService.GetCurrentUser();
                
                Debug.Log($"GetCurrentUser: {user?.Id} {user?.Nickname}");

                View.Q<Label>("PlayerName").text = user?.Nickname ?? "";
                View.Q<Label>("UserIdLabel").text = user?.Id.ToString() ?? "";
                
                if (!string.IsNullOrEmpty(user?.PictureUrl))
                {
                    var www = UnityWebRequestTexture.GetTexture(user.PictureUrl);

                    try
                    {
                        await www.SendWebRequest().ToUniTask();
                    
                        if (www.result == UnityWebRequest.Result.Success)
                        {
                            var picture = DownloadHandlerTexture.GetContent(www);
                            View.Q<VisualElement>("PlayerAvatar").style.backgroundImage = new StyleBackground(picture);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log(e.Message);
                    }
                }

                var emailCredential = user?.Credentials.Find(credential => credential.Provider == "email");
            
                _credentials[0].Username = emailCredential?.Id.ToString() ?? "";
                Debug.Log($"Email: {emailCredential?.Id}");
            
                var googleCredential = user?.Credentials.Find(credential => credential.Provider == "google");
            
                _credentials[1].Username = googleCredential?.Id.ToString() ?? "";
                Debug.Log($"Google: {googleCredential?.Id}");
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
                
                _credentials[0].Username = "";
                _credentials[1].Username = "";
            }
        }

        private void OnEnable()
        {
            View.Q<Button>("ExitButton").RegisterCallback<PointerUpEvent>(_ => { Visible = false; });
            View.Q<Button>("MoreOptionsButton").RegisterCallback<PointerUpEvent>(OnMoreOptionsButtonClick);
            View.RegisterCallback<GeometryChangedEvent>(_ => SetOrientation());
            
            BindListView();
        }

        private void OnMoreOptionsButtonClick(PointerUpEvent evt)
        {
            Debug.Log("More options clicked");
            View.Q<VisualElement>("MoreOptionsMenu").ToggleInClassList("hide");
            View.Q<VisualElement>("MoreOptionsMenu").Focus();
        }

        private void BindListView()
        {
            _credentialListView = View.Q<ListView>("AccountList");
            _itemTemplate ??= Resources.Load<VisualTreeAsset>("ConnectAccountItem");
            _credentialListView.makeItem = _itemTemplate.Instantiate;
            _credentialListView.bindItem = BindListViewItem;
            _credentialListView.fixedItemHeight = 52;
            _credentialListView.itemsSource = _credentials;
            _credentialListView.selectionType = SelectionType.Single;
        }

        private void BindListViewItem(VisualElement element, int index)
        {
            element.userData = _credentials[index];

            element.Q<Button>("ConnectButton").UnregisterCallback<PointerUpEvent, UserCredential>(OnConnectButtonClick);

            if (string.IsNullOrEmpty(_credentials[index].Username))
            {
                element.Q<Label>("UsernameLabel").AddToClassList("hide");
                element.Q<Button>("ConnectButton").RemoveFromClassList("hide");
                element.Q<Button>("ConnectButton")
                    .RegisterCallback<PointerUpEvent, UserCredential>(OnConnectButtonClick, _credentials[index]);
            }
            else
            {
                element.Q<Button>("ConnectButton").AddToClassList("hide");
                element.Q<Label>("UsernameLabel").text = _credentials[index].Username;
            }

            element.Q<Label>("MethodName").text = _credentials[index].CredentialProvider.ToString();
            element.Q<VisualElement>("MethodLogo").ClearClassList();
            element.Q<VisualElement>("MethodLogo").AddToClassList("method-logo");
            element.Q<VisualElement>("MethodLogo").AddToClassList(_credentials[index].CredentialIconStyle);
        }

        private void OnConnectButtonClick(PointerUpEvent evt, UserCredential credential)
        {
            Debug.Log($"Selected {credential?.CredentialProvider}");

            if (credential == null) return;

            Visible = false;
            
            switch (credential.CredentialProvider)
            {
                case CredentialProvider.Email:
                    Model.ShowEmailLogin(result =>
                    {
                        Debug.Log($"ShowEmailLogin: {result.Success}");
                        
                        Show();
                    });
                    break;
                case CredentialProvider.Google:
                    Model.ShowSocialLogin("google", result =>
                    {
                        Debug.Log($"ShowSocialLogin: {result.Success}");

                        Show();
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