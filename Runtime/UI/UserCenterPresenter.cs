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
        private Texture2D _defaultAvatar;
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
            StartCoroutine(ShowAsync().ToCoroutine());
        }

        private async UniTask ShowAsync()
        {
            try
            {
                var user = await Model.AuthService.GetCurrentUser();
                
                Debug.Log($"GetCurrentUser: {user?.Id} {user?.Nickname}");

                View.Q<Label>("PlayerName").text = user?.IsGuest == true ? "Guest " + user.Id  : user?.Nickname;
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
                else
                {
                    View.Q<VisualElement>("PlayerAvatar").style.backgroundImage = new StyleBackground(_defaultAvatar);
                }

                var emailCredential = user?.Credentials.Find(credential => credential.Provider == "email");
            
                _credentials[0].Username = emailCredential?.DisplayText ?? "";
                Debug.Log($"Email: {emailCredential?.DisplayText}");
            
                var googleCredential = user?.Credentials.Find(credential => credential.Provider == "google");
            
                _credentials[1].Username = googleCredential?.DisplayText ?? "";
                Debug.Log($"Google: {googleCredential?.DisplayText}");
                
                _credentialListView.Rebuild();
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
                
                _credentials[0].Username = "";
                _credentials[1].Username = "";
            }
            
            Visible = true;

            View.Q<VisualElement>("MoreOptionsMenu").AddToClassList("hide");
            SetOrientation();

        }

        private void Awake()
        {
            _defaultAvatar = Resources.Load<Texture2D>("PlayerProfileBackground");
            View.Q<VisualElement>("MoreOptionsMenu").RegisterCallback<PointerUpEvent>(OnMoreOptionsMenuSelected);
            View.Q<VisualElement>("SwitchProfile").RegisterCallback<PointerUpEvent>(_ => OnSwitchProfile());
            View.Q<VisualElement>("LogoutAccount").RegisterCallback<PointerUpEvent>(_ => OnLogout());
        }

        private void OnSwitchProfile()
        {
            Visible = false;
            Model.ShowAccountSelection();
        }

        private void OnLogout()
        {
            Visible = false;
            StartCoroutine(Model.AuthService.LogoutAsync().ToCoroutine());
        }

        private void OnEnable()
        {
            View.Q<Button>("ExitButton").RegisterCallback<PointerUpEvent>(_ => { Visible = false; });
            View.Q<Button>("MoreOptionsButton").RegisterCallback<PointerUpEvent>(OnMoreOptionsButtonClick);
            View.RegisterCallback<GeometryChangedEvent>(_ => SetOrientation());
            
            View.RegisterCallback<PointerDownEvent>(OnViewClicked);
            
            BindListView();
        }

        private void OnMoreOptionsButtonClick(PointerUpEvent evt)
        {
            Debug.Log("More options clicked");
            ToggleMoreOptionsMenu();
            evt.StopPropagation();
        }

        private void ToggleMoreOptionsMenu()
        {
            var moreOptionsMenu = View.Q<VisualElement>("MoreOptionsMenu");
            moreOptionsMenu.ToggleInClassList("hide");
            if (!moreOptionsMenu.ClassListContains("hide"))
            {
                moreOptionsMenu.Focus();
            }
        }

        private void OnViewClicked(PointerDownEvent evt)
        {
            var moreOptionsMenu = View.Q<VisualElement>("MoreOptionsMenu");
            if (!moreOptionsMenu.ClassListContains("hide"))
            {
                var clickedElement = evt.target as VisualElement;
                if (clickedElement != null && !moreOptionsMenu.Contains(clickedElement))
                {
                    ToggleMoreOptionsMenu();
                }
            }
        }

        private void OnMoreOptionsMenuSelected(PointerUpEvent evt)
        {
            View.Q<VisualElement>("MoreOptionsMenu").AddToClassList("hide");
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
                element.Q<VisualElement>("Username").AddToClassList("hide");
                element.Q<Button>("ConnectButton").RemoveFromClassList("hide");
                element.Q<Button>("ConnectButton")
                    .RegisterCallback<PointerUpEvent, UserCredential>(OnConnectButtonClick, _credentials[index]);
            }
            else
            {
                element.Q<Button>("ConnectButton").AddToClassList("hide");
                element.Q<VisualElement>("Username").RemoveFromClassList("hide");
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
                    Model.ShowEmailLogin(_ => Model.ShowUserCenter());
                    
                    break;
                case CredentialProvider.Google:
                    StartCoroutine(SocialLinkAsync("google").ToCoroutine());
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async UniTask SocialLinkAsync(string provider)
        {
            try
            {
                var userBundle = await Model.SocialLinkAsync(provider);
                            
                Debug.Log($"StartSocialLink: {userBundle.User?.Id} {userBundle.User?.Nickname}");
                            
                Model.ShowUserCenter();
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
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