using System;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using System.Text.RegularExpressions;
using System.Linq;
using System.Reflection;
using com.noctuagames.sdk.Events;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace com.noctuagames.sdk.UI
{
    internal class UserCenterPresenter : Presenter<AuthenticationModel>
    {
        public EventSender EventSender;

        private readonly ILogger _log = new NoctuaLogger();

        // Flags
        private bool _ssoDisabled = false;

        private VisualTreeAsset _itemTemplate;
        private VisualElement _rootView;
        private Texture2D _defaultAvatar;
        private ListView _credentialListView;
        private Label _stayConnect;
        private VisualElement _containerStayConnect;
        private VisualElement _hiTextContainer;
        private Label _playerName;
        private Button _moreOptionsMenuButton;
        private Button _helpButton;
        private VisualElement _copyIcon;
        private VisualElement _connectAccountFooter;
        private VisualElement _moreOptionsMenu;

        #region Edit Profile Variable
        //Edit Profile UI
        private VisualElement _editProfileContainer;
        private InputFieldNoctua _nicknameTF;
        private InputFieldNoctua _birthDateTF;
        private DropdownNoctua _genderDF;
        private DropdownNoctua _countryDF;
        private DropdownNoctua _languageDF;
        private VisualElement _profileImage;
        private string _profileImageUrl;
        private VisualElement _playerImage;
        private ButtonNoctua _saveButton;
        private ButtonNoctua _changePictureButton;
        private Label _userIDLabel;
        private string _newProfileUrl;
        private string _userIDValue;
        private ProfileOptionData _profileDataOptions;
        private List<string> _countryOptions = new List<string> { "Select Country" };
        private List<string> _languageOptions = new List<string> { "Select Languages" };
        private VisualElement _noctuaLogoWithText;
        private Label _sdkVersion;
        private string _dateString;
        private VisualElement _veDropdownDrawer;
        #endregion

        // Suggest Bind UI
        private VisualElement _guestContainer;
        private ScrollView _scrollRectCarousel;
        private VisualElement _veCarouselParent;
        private Label _carouselLabel;
        private VisualElement _indicatorContainer;
        private Dictionary<string, string> _translations;
        private List<VisualElement> _carouselImage = new List<VisualElement>();

        private readonly string[] _carouselItems = {
            "SuggestionBindText.Content1",
            "SuggestionBindText.Content2",
            "SuggestionBindText.Content3"
            };

        private string[] carouselTranslate;

        private int _currentIndex = 0;
        private const float SlideInterval = 3f;
        private bool _isGuestUser = false;
        private bool _isDatePickerOpen = false;
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
            new UserCredential
            {
                CredentialIconStyle = "facebook-logo",
                CredentialProvider = CredentialProvider.Facebook
            },
#if UNITY_IOS            
            new UserCredential
            {
                CredentialIconStyle = "apple-logo",
                CredentialProvider = CredentialProvider.Apple
            },
#endif
        };

        private GlobalConfig _globalConfig;
        private StyleBackground _originalStyleBackground;
        private bool _isScrolling;
        private bool _isDraggingCarousel;
        private Vector2 _vectTargetScrollOffset;
        private Vector2 _vectLastPointerPosition;
        private float _fltScrollDuration = 3f; // Duration of the scroll animation
        private float _fltElapsedTime = 0f;

        protected override void Attach()
        {
        }

        protected override void Detach()
        {
        }

        private void Awake()
        {
            _rootView = View.Q<VisualElement>("Root");
            _defaultAvatar = Resources.Load<Texture2D>("DefaultAvatar");

            _stayConnect = View.Q<Label>("StayConnectCompany");
            _containerStayConnect = View.Q<VisualElement>("ContainerStayConnect");
            _veCarouselParent = View.Q<VisualElement>("CarouselParent");
            _hiTextContainer = View.Q<VisualElement>("HiText");
            _playerName = View.Q<Label>("PlayerName");
            _moreOptionsMenuButton = View.Q<Button>("MoreOptionsButton");
            _moreOptionsMenu = View.Q<VisualElement>("MoreOptionsMenu");
            _helpButton = View.Q<Button>("HelpButton");
            _copyIcon = View.Q<VisualElement>("CopyIcon");
            _noctuaLogoWithText = View.Q<VisualElement>("NoctuaLogoContainer");
            _connectAccountFooter = View.Q<VisualElement>("ConnectAccountFooter");
            _sdkVersion = View.Q<Label>("SDKVersion");
            _sdkVersion.text = $"v{Assembly.GetExecutingAssembly().GetName().Version}";

            _moreOptionsMenu.RegisterCallback<ClickEvent>(_ => OnMoreOptionsMenuSelected());
            View.Q<VisualElement>("EditProfile").RegisterCallback<ClickEvent>(_ => OnEditProfile());
            View.Q<Button>("BackButton").RegisterCallback<ClickEvent>(_ => OnBackEditProfile());
            View.Q<VisualElement>("SwitchProfile").RegisterCallback<ClickEvent>(_ => OnSwitchProfile());
            View.Q<VisualElement>("LogoutAccount").RegisterCallback<ClickEvent>(_ => OnLogout());
            View.Q<VisualElement>("PendingPurchases").RegisterCallback<ClickEvent>(_ => OnPendingPurchases());
            View.Q<Label>("FindMoreLabel").RegisterCallback<ClickEvent>(_ => OnFindMore());

            _helpButton.RegisterCallback<ClickEvent>(OnHelp);
            _copyIcon.RegisterCallback<ClickEvent>(_ => OnCopyText());

            //Suggest Bind UI
            _guestContainer = View.Q<VisualElement>("UserGuestUI");
            _guestContainer.AddToClassList("hide");

            //Edit Profile UI
            SetupEditProfileUI();
        }

        protected override void Update()
        {
            base.Update();
            CarouselScrollAnimation();
            AdjustPopupForKeyboard();
        }

        private void AdjustPopupForKeyboard()
        {
            // Adjust the popup behavior to handle keyboard blocking the input field

            if (Screen.width > Screen.height)
            {
                // No specific behavior defined for landscape mode, so leave as is
                return;
            }

            _rootView.style.justifyContent = TouchScreenKeyboard.visible
                ? Justify.FlexStart
                : Justify.FlexEnd;
        }
        private void OnEnable()
        {
            _carouselLabel = View.Q<Label>("TextCarousel");
            _indicatorContainer = View.Q<VisualElement>("IndicatorContainer");
            View.Q<Button>("ExitButton").RegisterCallback<PointerUpEvent>(OnExitButton);
            _moreOptionsMenuButton.RegisterCallback<ClickEvent>(OnMoreOptionsButtonClick);
            View.Q<Button>("GuestConnectButton").RegisterCallback<ClickEvent>(OnGuestConnectButtonClick);
            View.Q<VisualElement>("DeleteAccount").RegisterCallback<ClickEvent>(_ => OnDeleteAccount());
            View.RegisterCallback<GeometryChangedEvent>(_ => SetOrientation());

            View.RegisterCallback<PointerDownEvent>(OnViewClicked);

            BindListView();

            //Carousel            
            InitCarousel();

        }

        private void SetOrientation(bool isEditProfile = false)
        {
            bool isLandscape = Screen.width > Screen.height;

            if (isLandscape)
            {
                View.style.flexDirection = FlexDirection.Row;
                View.style.justifyContent = Justify.FlexEnd;

                View.Q<VisualElement>("UserCenter").RemoveFromClassList("portrait");
                View.Q<VisualElement>("UserProfile").RemoveFromClassList("portrait");
                View.Q<VisualElement>("ConnectAccount").RemoveFromClassList("portrait");

                View.Q<VisualElement>("UserCenter").AddToClassList("landscape");
                View.Q<VisualElement>("UserProfile").AddToClassList("landscape");
                View.Q<VisualElement>("ConnectAccount").AddToClassList("landscape");

                _noctuaLogoWithText.RemoveFromClassList("hide");

                if (isEditProfile)
                {
                    View.Q<VisualElement>("EditProfileBox").RemoveFromClassList("potrait");
                    View.Q<VisualElement>("EditProfileBox").AddToClassList("landscape");

                    View.Q<VisualElement>("UserProfile").RemoveFromClassList("show");
                    View.Q<VisualElement>("UserProfile").AddToClassList("hide");

                }
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

                if (isEditProfile)
                {
                    View.Q<VisualElement>("EditProfileBox").RemoveFromClassList("landscape");
                    View.Q<VisualElement>("EditProfileBox").AddToClassList("potrait");

                    View.Q<VisualElement>("UserProfileHeader").AddToClassList("hide");
                    View.Q<VisualElement>("UserProfileHeader").RemoveFromClassList("show");
                 
                    View.Q<VisualElement>("ConnectAccount").RemoveFromClassList("portrait");
                    View.Q<VisualElement>("ConnectAccount").RemoveFromClassList("connect-account");
                    View.Q<VisualElement>("ConnectAccount").AddToClassList("connect-account-edit-profile-portrait");
                   
                    View.Q<VisualElement>("UserProfile").RemoveFromClassList("show");
                    View.Q<VisualElement>("UserProfile").AddToClassList("hide");
                    View.Q<VisualElement>("UserCenter").style.maxHeight = Length.Percent(65);
                    View.Q<VisualElement>("ScrollViewContainer").style.marginTop = 10;
                }
                else
                {
                    View.Q<VisualElement>("ConnectAccount").AddToClassList("connect-account");
                    View.Q<VisualElement>("ConnectAccount").AddToClassList("portrait");
                    View.Q<VisualElement>("ScrollViewContainer").style.marginTop = 0;
                }
            }
        }

        public void Show()
        {
            StartCoroutine(ShowAsync().ToCoroutine());
        }

        public void SetWhitelabel(GlobalConfig config)
        {
            _globalConfig = config;

            BindListView();

            if (!string.IsNullOrEmpty(config?.CoPublisher?.CompanyName))
            {
                _stayConnect.text = config.CoPublisher.CompanyName;

                var logo = Utility.GetCoPublisherLogo(config.CoPublisher.CompanyName);

                var defaultLogo = Resources.Load<Texture2D>(logo);
                View.Q<VisualElement>("NoctuaLogoWithText").style.backgroundImage = new StyleBackground(defaultLogo);
                View.Q<VisualElement>("NoctuaLogoWithText2").style.backgroundImage = new StyleBackground(defaultLogo);

                string cleanedUrl = config.CoPublisher.CompanyWebsiteUrl.Replace("https://", "");
                View.Q<Label>("FindMoreLabel").text = cleanedUrl;
            }
            else
            {
                _stayConnect.text = "Noctua";
                View.Q<Label>("FindMoreLabel").text = "<color=#3B82F6>noctua.gg</color>";
            }
        }

        private async UniTask ShowAsync()
        {
            try
            {

                // Hide SSO if the backend told so.
                if (_ssoDisabled)
                {
                    // Refresh the connect account item list view.
                    BindListView();
                }

                Model.ShowLoadingProgress(true);
                if (Model.AuthService.RecentAccount == null)
                {
                    Model.ShowLoadingProgress(false);
                    throw new NoctuaException(NoctuaErrorCode.Authentication, "No account is logged in.");
                }

                // Reset some values
                _profileImageUrl = "";
                _originalStyleBackground = null;

                var user = await Model.AuthService.GetUserAsync();
                var isGuest = user?.IsGuest == true;

                await UniTask.Delay(TimeSpan.FromSeconds(2)); // Wait for 2 seconds

                _log.Debug($"current user in user center is '{user?.Id} - {user?.Nickname}'");

                _moreOptionsMenu.AddToClassList("hide");
                View.Q<Label>("PlayerName").text = isGuest ? "Guest " + user.Id : user?.Nickname;
                View.Q<Label>("UserIdLabel").text = "ID : " + user?.Id.ToString() ?? "";
                _userIDValue = user?.Id.ToString() ?? "";

                //Edit Profile - Setup Data
                if (!isGuest)
                {
                    var profileOptions = await Model.AuthService.GetProfileOptions();
                    _profileDataOptions = profileOptions;

                    OnUIEditProfile(false);
                    SetupDropdownUI();
                    SetupDatePickerUI();

                    _nicknameTF.textField.value = user?.Nickname;
                    _newProfileUrl = user?.PictureUrl;

                    bool validDate = DateTime.TryParse(user?.DateOfBirth, null, DateTimeStyles.RoundtripKind, out DateTime dateTime);
                    string formattedDate = validDate ? dateTime.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "";

                    _birthDateTF.textField.value = formattedDate;
                    _dateString = formattedDate;

                    string genderOriginal = user?.Gender;
                    string genderUpperChar = char.ToUpper(genderOriginal[0]) + genderOriginal.Substring(1);

                    _genderDF.value = genderUpperChar;

                    int indexCountry = _profileDataOptions.Countries.FindIndex(item => item.IsoCode.ToLower() == user?.Country.ToLower());
                    if (indexCountry != -1)
                    {
                        _countryDF.value = _countryOptions[indexCountry];
                    }
                    else
                    {
                        _countryDF.value = "Select Country";
                    }

                    int indexLanguage = _profileDataOptions.Languages.FindIndex(item => item.IsoCode.ToLower() == user?.Language.ToLower());
                    if (indexLanguage != -1)
                    {
                        _languageDF.value = _languageOptions[indexLanguage];
                    }
                    else
                    {
                        _languageDF.value = "Select Language";
                    }
                }

                _isGuestUser = user?.IsGuest ?? false;
                UpdateUIGuest(isGuest);

                if (!string.IsNullOrEmpty(user?.PictureUrl))
                {
                    _profileImageUrl = user.PictureUrl;

                    var picture = await DownloadTexture2D(user.PictureUrl);

                    if (picture == null)
                    {
                        picture = _defaultAvatar;
                    }

                    View.Q<VisualElement>("PlayerAvatar").style.backgroundImage = new StyleBackground(picture);
                    _profileImage.style.backgroundImage = new StyleBackground(picture);
                }
                else
                {
                    View.Q<VisualElement>("PlayerAvatar").style.backgroundImage = new StyleBackground(_defaultAvatar);
                }

                if (!isGuest)
                {
                    foreach (var t in _credentials)
                    {
                        _log.Debug($"credential: {t.CredentialProvider} {t.Username}");

                        var credential = user?.Credentials.Find(c => c.Provider == t.CredentialProvider.ToString().ToLower());
                        t.Username = credential?.DisplayText ?? "";
                    }

                    _credentialListView.Rebuild();
                }

                Model.ShowLoadingProgress(false);

                Visible = true;
                SetOrientation();

                EventSender?.Send("user_center_opened");
            }
            catch (Exception e)
            {
                Model.ShowLoadingProgress(false);
                _log.Warning($"{e.Message}\n{e.StackTrace}");

                _credentials.ForEach(c => c.Username = "");
                Model.ShowGeneralNotification(e.Message);
            }
        }

        private void OnFindMore()
        {
            _log.Debug("on find more clicked");

            var findMorelUrl = _globalConfig?.CoPublisher?.CompanyWebsiteUrl ?? "https://noctua.gg";

            Application.OpenURL(findMorelUrl);
        }

        private async void OnHelp(ClickEvent evt)
        {
            await Noctua.Platform.Content.ShowCustomerService();
        }

        private void OnCopyText()
        {
            GUIUtility.systemCopyBuffer = _userIDValue;
            _log.Debug($"copied '{_userIDValue}' to clipboard");

            Model.ShowGeneralNotification($"copied '{_userIDValue}' to clipboard", true);
        }

        private void SetupDatePickerUI()
        {
            var birthDateContainer = View.Q<VisualElement>("BirthdateContainer");
            _birthDateTF = new InputFieldNoctua(View.Q<TextField>("BirthdateTF"));
            _birthDateTF.textField.isReadOnly = true;
            _birthDateTF.textField.focusable = false;

            string _dob = string.IsNullOrEmpty(_dateString) ? "01/01/2000" : _dateString;
            DateTime parsedDate = DateTime.ParseExact(_dob, "dd/MM/yyyy", null);

            birthDateContainer.RegisterCallback<ClickEvent>(upEvent =>
            {
                upEvent.StopImmediatePropagation();

                if (_isDatePickerOpen)
                {
                    return;
                }

                _isDatePickerOpen = true;

                Noctua.OpenDatePicker(parsedDate.Year, parsedDate.Month, parsedDate.Day, 1,
                (DateTime _date) =>
                {
                    _log.Debug($"picked date '{_date:O}'");
                },
                (DateTime _date) =>
                {
                    _birthDateTF.textField.value = _date.ToString("dd/MM/yyyy");
                    Utility.UpdateButtonState(_saveButton.button, true);
                    _isDatePickerOpen = false;
                });
            });

            // Register the callback for each text change
            _birthDateTF.textField.RegisterCallback<ChangeEvent<string>>(evt => OnDateFieldChanged());
        }

        private void OnDateFieldChanged()
        {
            _dateString = _birthDateTF.text;
            _birthDateTF.AdjustLabel();
        }

        private void SetupEditProfileUI()
        {
            _editProfileContainer = View.Q<VisualElement>("EditProfileBox");

            _nicknameTF = new InputFieldNoctua(View.Q<TextField>("NicknameTF"));
            _genderDF = new DropdownNoctua(View.Q<DropdownField>("GenderTF"));
            _countryDF = new DropdownNoctua(View.Q<DropdownField>("CountryTF"));
            _languageDF = new DropdownNoctua(View.Q<DropdownField>("LanguageTF"));
            _profileImage = View.Q<VisualElement>("ProfileImage");
            _playerImage = View.Q<VisualElement>("PlayerAvatar");
            _userIDLabel = View.Q<Label>("UserIdLabel");

            _changePictureButton = new ButtonNoctua(View.Q<Button>("ChangePictureButton"));
            _saveButton = new ButtonNoctua(View.Q<Button>("SaveButton"));

            Utility.UpdateButtonState(_saveButton.button, false);
            _saveButton.button.RegisterCallback<ClickEvent>(_ => OnSaveEditProfile());

            var elementNames = new List<string>
            {
                "NicknameTF",
                "GenderTF",
                "CountryTF",
                "LanguageTF",
            };

            Utility.RegisterForMultipleValueChanges<string>(View, elementNames, _saveButton.button);

            //Show mobile input
            _nicknameTF.textField.hideMobileInput = false;

            _nicknameTF.textField.RegisterValueChangedCallback(evt => OnValueChanged(_nicknameTF));
            _changePictureButton.button.RegisterCallback<ClickEvent>(evt => OnChangeProfile());

            _nicknameTF.SetFocus();

            SetupDropdownUI();
        }

        public void HideAllErrors()
        {
            _nicknameTF.Reset();
            _genderDF.Reset();
            _countryDF.Reset();
            _languageDF.Reset();
        }

        private void SetupDropdownUI()
        {
            var genderOption = new List<string> { "Male", "Female" };

            if (_profileDataOptions != null)
            {
                _countryOptions.Clear();
                _languageOptions.Clear();

                foreach (GeneralProfileData country in _profileDataOptions.Countries)
                {
                    _countryOptions.Add(country.EnglishName);
                }

                foreach (GeneralProfileData _languages in _profileDataOptions.Languages)
                {
                    _languageOptions.Add(_languages.EnglishName);
                }
            }

            _genderDF.SetFocus(DropdownFocus);
            _countryDF.SetFocus(DropdownFocus);
            _languageDF.SetFocus(DropdownFocus);

            _genderDF.SetupList(genderOption);
            _countryDF.SetupList(_countryOptions);
            _languageDF.SetupList(_languageOptions);
        }

        public void DropdownFocus()
        {

            _veDropdownDrawer = View.parent.Q(className: "unity-base-dropdown");
            if (_veDropdownDrawer != null)
            {
                _veDropdownDrawer.Q("unity-content-container").RegisterCallback<FocusOutEvent>(evt => OnDropdownFocusOut());
                _veDropdownDrawer.Q("unity-content-container").RegisterCallback<PointerDownEvent>(evt => OnDropdownFocusOut());
            }
            else
            {
                OnDropdownFocusOut();
            }

        }

        private void OnDropdownFocusOut()
        {
            _genderDF.Reset();
            _countryDF.Reset();
            _languageDF.Reset();
        }

        private void OnChangeProfile()
        {
            if (NativeGallery.IsMediaPickerBusy())
            {
                return;
            }

            RequestPermissionAsynchronously(NativeGallery.PermissionType.Read, NativeGallery.MediaType.Image);
        }

        private async void RequestPermissionAsynchronously(NativeGallery.PermissionType permissionType, NativeGallery.MediaType mediaTypes)
        {
            NativeGallery.Permission permission = await NativeGallery.RequestPermissionAsync(permissionType, mediaTypes);
            if (permission == NativeGallery.Permission.Granted)
            {
                PickImage();
            }
        }

        private void PickImage()
        {

            NativeGallery.Permission permission = NativeGallery.GetImageFromGallery((path) =>
           {
               if (path != null)
               {
                   FileUploader(path);
               }
           });

            _log.Debug("Permission result: " + permission);
        }

        private async void FileUploader(string filePath)
        {
            ShowButtonSpinner(true);

            try
            {
                _newProfileUrl = await Model.AuthService.FileUploader(filePath);

                StartCoroutine(LoadImageFromUrl(_newProfileUrl, true));
                ShowButtonSpinner(false);
            }
            catch (Exception e)
            {
                ShowButtonSpinner(false);

                Model.ShowGeneralNotification(e.Message);
            }
        }

        private IEnumerator LoadImageFromUrl(string url, bool isEditProfile)
        {
            using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                {
                    _log.Error($"Error loading image from url: {url}, error: {www.error}");
                }
                else
                {


                    Texture2D texture = ((DownloadHandlerTexture)www.downloadHandler).texture;
                    if (isEditProfile)
                    {
                        _profileImage.style.backgroundImage = new StyleBackground(texture);
                        Utility.UpdateButtonState(_saveButton.button, true);
                    }
                    else
                    {
                        _profileImage.style.backgroundImage = _originalStyleBackground;
                        _playerImage.style.backgroundImage = _originalStyleBackground;
                    }
                }

            }
        }

        private async UniTask<Texture2D> DownloadTexture2D(string url)
        {
            try
            {
                using UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);

                await www.SendWebRequest();

                if (www.result is not UnityWebRequest.Result.Success)
                {
                    _log.Error($"error loading image from url: {url}, error: {www.error}");

                    return null;
                }

                return DownloadHandlerTexture.GetContent(www);
            }
            catch (Exception e)
            {
                _log.Error($"error loading image from url: {url}, error: {e.Message}");

                return null;
            }
        }

        private void OnEditProfile()
        {
            _log.Debug("clicking edit profile");

            OnUIEditProfile(true);
        }

        private void OnBackEditProfile()
        {
            _log.Debug("clicking back on edit profile");
            ShowUserProfile();
            OnUIEditProfile(false);
        }
        private void ShowUserProfile()
        {
            View.Q<VisualElement>("UserProfileHeader").AddToClassList("show");
            View.Q<VisualElement>("UserProfile").AddToClassList("show");

            View.Q<VisualElement>("UserProfile").RemoveFromClassList("hide");
            View.Q<VisualElement>("UserProfileHeader").RemoveFromClassList("hide");
        }
        private async void OnUIEditProfile(bool isEditProfile)
        {
            SetOrientation(isEditProfile);
            _isDatePickerOpen = false;

            if (isEditProfile)
            {
                _log.Debug("Edit profile");
                _nicknameTF.textField.value = View.Q<Label>("PlayerName").text;

                _originalStyleBackground = _profileImage.style.backgroundImage;
                //remove class
                _guestContainer.RemoveFromClassList("show");
                _stayConnect.RemoveFromClassList("show");
                _containerStayConnect.RemoveFromClassList("show");
                _credentialListView.RemoveFromClassList("show");
                _hiTextContainer.RemoveFromClassList("show");
                _playerName.RemoveFromClassList("show");
                _moreOptionsMenuButton.RemoveFromClassList("show");
                _helpButton.RemoveFromClassList("show");
                _copyIcon.RemoveFromClassList("show");
                _connectAccountFooter.RemoveFromClassList("show");
                _playerImage.RemoveFromClassList("player-avatar");

                //add class
                _moreOptionsMenuButton.AddToClassList("hide");
                _helpButton.AddToClassList("hide");
                _copyIcon.AddToClassList("hide");
                _guestContainer.AddToClassList("hide");
                _hiTextContainer.AddToClassList("hide");
                _playerName.AddToClassList("hide");
                _credentialListView.AddToClassList("hide");
                _stayConnect.AddToClassList("hide");
                _containerStayConnect.AddToClassList("hide");
                _connectAccountFooter.AddToClassList("hide");
                _playerImage.AddToClassList("profile-menu-image");
                _playerImage.style.backgroundImage = Resources.Load<Texture2D>("EditProfileImage");

                _editProfileContainer.AddToClassList("show");

                _userIDLabel.text = "ID : " + Locale.GetTranslation("UserCenterPresenter.MenuEditProfile.Label.text");
                _userIDLabel.style.fontSize = 16;

                View.Q<Label>("TitleEditBack").text = Locale.GetTranslation("UserCenterPresenter.MenuEditProfile.Label.text");

                Utility.UpdateButtonState(_saveButton.button, false);

                if (!string.IsNullOrEmpty(_profileImageUrl))
                {
                    var picture = await DownloadTexture2D(_profileImageUrl);
                    if (picture == null)
                    {
                        picture = _defaultAvatar;
                    }
                    _profileImage.style.backgroundImage = new StyleBackground(picture);
                }
                else
                {
                    _profileImage.style.backgroundImage = Resources.Load<Texture2D>("EditProfileImage");
                }
            }
            else
            {
                _log.Debug("Not edit profile");

                //remove class
                _editProfileContainer.RemoveFromClassList("show");
                _guestContainer.RemoveFromClassList("hide");
                _stayConnect.RemoveFromClassList("hide");
                _containerStayConnect.RemoveFromClassList("hide");
                _credentialListView.RemoveFromClassList("hide");
                _hiTextContainer.RemoveFromClassList("hide");
                _playerName.RemoveFromClassList("hide");
                _moreOptionsMenuButton.RemoveFromClassList("hide");
                _helpButton.RemoveFromClassList("hide");
                _copyIcon.RemoveFromClassList("hide");
                _connectAccountFooter.RemoveFromClassList("hide");
                _playerImage.RemoveFromClassList("profile-menu-image");

                //add class
                _editProfileContainer.AddToClassList("hide");
                _hiTextContainer.AddToClassList("show");
                _playerName.AddToClassList("show");
                _moreOptionsMenuButton.AddToClassList("show");
                _helpButton.AddToClassList("show");
                _copyIcon.AddToClassList("show");
                _connectAccountFooter.AddToClassList("show");
                _playerImage.AddToClassList("player-avatar");

                if (!string.IsNullOrEmpty(_newProfileUrl))
                {
                    _playerImage.style.backgroundImage = _originalStyleBackground;
                    _profileImage.style.backgroundImage = _originalStyleBackground;
                }
                _userIDLabel.text = "ID : " + _userIDValue;
                _userIDLabel.style.fontSize = 12;

                if (_isGuestUser)
                {
                    _credentialListView.AddToClassList("hide");
                    _stayConnect.AddToClassList("hide");
                    _containerStayConnect.AddToClassList("hide");
                    _guestContainer.AddToClassList("show");
                }
                else
                {
                    _guestContainer.AddToClassList("hide");
                    _credentialListView.AddToClassList("show");
                    _stayConnect.AddToClassList("show");
                    _containerStayConnect.AddToClassList("show");
                }

                _nicknameTF.textField.value = View.Q<Label>("PlayerName").text;

                Noctua.CloseDatePicker();
                ShowButtonSpinner(false);
                HideAllErrors();

            }
        }
        private void OnValueChanged(InputFieldNoctua textField)
        {
            textField.AdjustLabel();
        }

        private void OnSaveEditProfile()
        {
            _log.Debug("clicking save edit profile");
            ShowUserProfile();
            SaveProfile();

        }

        private async void SaveProfile()
        {
            ShowButtonSpinner(true);

            HideAllErrors();

            if (string.IsNullOrEmpty(_nicknameTF.textField.value))
            {
                ShowButtonSpinner(false);

                _nicknameTF.Error(Locale.GetTranslation("EditProfile.NicknameValidation"));
                return;
            }

            if (string.IsNullOrEmpty(_countryDF.value) || _countryDF.value == "Select Country")
            {
                ShowButtonSpinner(false);

                _countryDF.Error(Locale.GetTranslation("EditProfile.CountryValidation"));
                return;
            }

            if (string.IsNullOrEmpty(_languageDF.value) || _languageDF.value == "Select Language")
            {
                ShowButtonSpinner(false);

                _languageDF.Error(Locale.GetTranslation("EditProfile.LanguageValidation"));
                return;
            }

            try
            {
                UpdateUserRequest updateUserRequest = new UpdateUserRequest();

                updateUserRequest.Nickname = _nicknameTF.text;

                var _dob = _birthDateTF.text;

                if (!string.IsNullOrEmpty(_dob))
                {
                    string format = "dd/MM/yyyy";
                    DateTime _dateTime = DateTime.ParseExact(_dob, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                    _dateTime = new DateTime(_dateTime.Year, _dateTime.Month, _dateTime.Day, 0, 0, 0, DateTimeKind.Utc);
                    updateUserRequest.DateOfBirth = _dateTime;
                }
                else
                {
                    updateUserRequest.DateOfBirth = null;
                }

                if (!string.IsNullOrEmpty(_genderDF.value))
                {
                    updateUserRequest.Gender = _genderDF.value.ToLower();
                }

                updateUserRequest.PictureUrl = _newProfileUrl;

                int indexCountry = _profileDataOptions.Countries.FindIndex(item => item.EnglishName.ToLower() == _countryDF.value.ToLower());
                int indexLanguage = _profileDataOptions.Languages.FindIndex(item => item.EnglishName.ToLower() == _languageDF.value.ToLower());

                updateUserRequest.Country = _profileDataOptions.Countries[indexCountry].IsoCode;
                updateUserRequest.Language = _profileDataOptions.Languages[indexLanguage].IsoCode;

                _log.Debug($"Update user request: {updateUserRequest.Nickname} - {updateUserRequest.DateOfBirth} - {updateUserRequest.Gender} - {updateUserRequest.PictureUrl} - {updateUserRequest.Country} - {updateUserRequest.Language}");

                await Model.AuthService.UpdateUserAsync(updateUserRequest);

                if (!string.IsNullOrEmpty(_newProfileUrl))
                {
                    StartCoroutine(LoadImageFromUrl(_newProfileUrl, true));
                }

                View.Q<Label>("PlayerName").text = _nicknameTF.text;

                ShowButtonSpinner(false);

                Model.ShowGeneralNotification("Update profile successfully", true);

                OnUIEditProfile(false);

                _log.Debug("updated user profile successfully");

                // Reload the entire presenter because
                // there is bug that will occure if we edit the profile again
                // without closing the user center.
                // https://applink.larksuite.com/client/message/link/open?token=AmdOk3AqAUAMZ2WcwcnAQAw%3D
                Show();
            }
            catch (Exception e)
            {
                _log.Exception(e);

                Model.ShowGeneralNotification(e.Message);

                ShowButtonSpinner(false);
            }
        }

        private void ShowButtonSpinner(bool isShowSpinner)
        {
            _saveButton.ToggleLoading(isShowSpinner);
            _changePictureButton.ToggleLoading(isShowSpinner);
        }

        private void OnSwitchProfile()
        {
            _log.Debug("clicking switch profile");

            Visible = false;
            Model.ShowAccountSelection();
            OnUIEditProfile(false);
        }
        private void OnPendingPurchases()
        {
            _log.Debug("clicking pending purchases");

            Visible = false;
            Model.ShowPendingPurchasesDialog();
            OnUIEditProfile(false);
        }

        private async void OnLogout()
        {
            _log.Debug("clicking logout");

            Visible = false;
            OnUIEditProfile(false);

            Model.ShowLogoutConfirmation();
        }

        private void OnExitButton(PointerUpEvent evt)
        {
            Visible = false;
            OnUIEditProfile(false);
            Model.AuthIntention = AuthIntention.None;
        }

        private void OnDisable()
        {
            DisableCarousel();
        }


        private void OnDeleteAccount()
        {
            _log.Debug("clicking delete account");

            Visible = false;
            OnUIEditProfile(false);
            Model.ShowAccountDeletionConfirmation(Model.AuthService.RecentAccount);
        }

        private void OnMoreOptionsButtonClick(ClickEvent evt)
        {
            _log.Debug("clicking more options button");

            ToggleMoreOptionsMenu();
            evt.StopPropagation();
        }

        private void OnGuestConnectButtonClick(ClickEvent evt)
        {
            _log.Debug("clicking guest connect button");

            View.visible = false;

            Model.PushNavigation(() => Model.ShowUserCenter());
            Model.AuthIntention = AuthIntention.Switch;
            Model.ShowLoginOptions();

            evt.StopPropagation();
        }

        private void ToggleMoreOptionsMenu()
        {
            _moreOptionsMenu.ToggleInClassList("hide");
            if (!_moreOptionsMenu.ClassListContains("hide"))
            {
                _moreOptionsMenu.Focus();
            }
        }

        private void OnViewClicked(PointerDownEvent evt)
        {
            _log.Debug("clicking user center view");

            if (!_moreOptionsMenu.ClassListContains("hide"))
            {
                var clickedElement = evt.target as VisualElement;
                if (clickedElement != null && !_moreOptionsMenu.Contains(clickedElement))
                {
                    ToggleMoreOptionsMenu();
                }
            }
        }

        private void OnMoreOptionsMenuSelected()
        {
            _log.Debug("clicking more options menu item");

            _moreOptionsMenu.AddToClassList("hide");
        }

        private void BindListView()
        {
            var credentialFiltered = Utility.ContainsFlag(_globalConfig?.Noctua?.Flags, "VNLegalPurpose") || _ssoDisabled ? _credentials.Where(c => c.CredentialProvider == CredentialProvider.Email).ToList() : _credentials;

            _credentialListView = View.Q<ListView>("AccountList");
            _itemTemplate ??= Resources.Load<VisualTreeAsset>("ConnectAccountItem");

            _credentialListView.makeItem = _itemTemplate.Instantiate;
            _credentialListView.bindItem = BindListViewItem;
            _credentialListView.fixedItemHeight = 52;
            _credentialListView.itemsSource = credentialFiltered;
            _credentialListView.selectionType = SelectionType.Single;
        }

        private void BindListViewItem(VisualElement element, int index)
        {
            element.userData = _credentials[index];

            element.Q<Button>("ConnectButton").UnregisterCallback<PointerUpEvent, UserCredential>(OnConnectButtonClick);
            element.Q<Button>("ConnectButton").text = Locale.GetTranslation("ConnectAccountItem.Connect");

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
            _log.Debug($"clicking connect button for {credential.CredentialProvider}");

            Visible = false;
            Model.AuthIntention = AuthIntention.Link;

            switch (credential.CredentialProvider)
            {
                case CredentialProvider.Email:
                    Model.ClearNavigation();
                    Model.PushNavigation(() => Model.ShowUserCenter());
                    Model.ShowEmailRegistration(true, true);

                    break;
                case CredentialProvider.Google:
                case CredentialProvider.Facebook:
                case CredentialProvider.Apple:
                    StartCoroutine(SocialLinkAsync(credential.CredentialProvider.ToString().ToLower()).ToCoroutine());

                    break;
                default:
                    throw new NoctuaException(NoctuaErrorCode.Application, $"{credential.CredentialProvider} not supported");
            }
        }

        private async UniTask SocialLinkAsync(string provider)
        {
            try
            {
                var credential = await Model.SocialLinkAsync(provider);

                _log.Debug($"social link completed: {provider}, {credential.DisplayText}");

                Model.ShowGeneralNotification(Locale.GetTranslation("Usercenter.AccountLinked"), true);
            }
            catch (Exception e)
            {
                _log.Exception(e);

                Model.ShowGeneralNotification(e.Message);
            }

            Model.ShowUserCenter();
        }

        private void UpdateUIGuest(bool isGuest)
        {
            var guestContainer = View.Q<VisualElement>("UserGuestUI");
            var stayConnect = View.Q<Label>("StayConnectCompany");
            var containerStayConnect = View.Q<VisualElement>("ContainerStayConnect");
            var moreOptionsButton = View.Q<Button>("MoreOptionsButton");
            var editProfilebutton = View.Q<VisualElement>("EditProfile");
            var switchProfileButton = View.Q<VisualElement>("SwitchProfile");
            var deleteAccountButton = View.Q<VisualElement>("DeleteAccount");
            var logoutAccountButton = View.Q<VisualElement>("LogoutAccount");
            var line = View.Q<VisualElement>("Line");

            // Always show more options button
            moreOptionsButton.AddToClassList("show");
            moreOptionsButton.RemoveFromClassList("hide");

            if (isGuest)
            {
                _credentialListView.AddToClassList("hide");
                _credentialListView.RemoveFromClassList("show");
                stayConnect.AddToClassList("hide");
                stayConnect.RemoveFromClassList("show");
                containerStayConnect.AddToClassList("hide");
                containerStayConnect.RemoveFromClassList("show");
                guestContainer.AddToClassList("show");
                guestContainer.RemoveFromClassList("hide");
                // Hide some menu item in more options button.
                editProfilebutton.AddToClassList("hide");
                deleteAccountButton.AddToClassList("hide");
                logoutAccountButton.AddToClassList("hide");
                line.AddToClassList("hide");

                carouselTranslate = new string[_carouselItems.Length];

                for (int i = 0; i < _carouselItems.Length; i++)
                {
                    carouselTranslate[i] = Locale.GetTranslation(_carouselItems[i]);
                }
            }
            else
            {
                _credentialListView.AddToClassList("show");
                _credentialListView.RemoveFromClassList("hide");
                stayConnect.AddToClassList("show");
                stayConnect.RemoveFromClassList("hide");
                containerStayConnect.AddToClassList("show");
                containerStayConnect.RemoveFromClassList("hide");
                guestContainer.AddToClassList("hide");
                guestContainer.RemoveFromClassList("show");
                // Show all items in more options button
                editProfilebutton.RemoveFromClassList("hide");
                switchProfileButton.RemoveFromClassList("hide");
                deleteAccountButton.RemoveFromClassList("hide");
                logoutAccountButton.RemoveFromClassList("hide");
                line.RemoveFromClassList("hide");
            }
        }

        #region Carousel
        private void DisableCarousel()
        {
            CancelInvoke(nameof(SlideToNextItem));
        }
        private void EnableCarousel()
        {
            InvokeRepeating(nameof(SlideToNextItem), SlideInterval, SlideInterval);
        }
        private void InitCarousel()
        {
            _scrollRectCarousel = View.Q<ScrollView>("ScrollRectCarousel");
            _carouselImage = _scrollRectCarousel.Query(className: "carousel-image").ToList();

            _veCarouselParent.RegisterCallback<PointerDownEvent>(_evt => OnCarouselDragStart(_evt));
            _veCarouselParent.RegisterCallback<PointerUpEvent>(_evt => OnCarouselDragEnd(_evt));

            SetupIndicators();
            HighlightCurrentIndicator();

            _scrollRectCarousel.SetEnabled(false);

            EnableCarousel();
        }
        private void SetupIndicators()
        {
            _indicatorContainer.Clear();

            for (int i = 0; i < _carouselItems.Length; i++)
            {
                VisualElement indicator = new VisualElement();
                indicator.AddToClassList("indicator");

                _indicatorContainer.Add(indicator);
            }
        }
        private void SlideToNextItem()
        {
            if (carouselTranslate == null) return;
            _currentIndex = (_currentIndex + 1) % _carouselItems.Length;

            //var regionCode = _globalConfig?.Noctua?.Region ?? "";
            _carouselLabel.text = carouselTranslate[_currentIndex];
            ScrollToItem(_carouselImage[_currentIndex]);
            HighlightCurrentIndicator();
        }
        private void SlideToNextItem(int _currentIndex)
        {
            if (carouselTranslate == null) return;

            //var regionCode = _globalConfig?.Noctua?.Region ?? "";
            _carouselLabel.text = carouselTranslate[_currentIndex];
            ScrollToItem(_carouselImage[_currentIndex]);
            HighlightCurrentIndicator();
        }
        private void ScrollToItem(VisualElement _element)
        {
            _scrollRectCarousel.MarkDirtyRepaint();
            _vectTargetScrollOffset = _scrollRectCarousel.scrollOffset;
            _vectTargetScrollOffset.x = _element.worldBound.width * _currentIndex;
            _isScrolling = true;
            _fltElapsedTime = 0f;
        }
        private void CarouselScrollAnimation()
        {
            if (_isScrolling && !_isDraggingCarousel)
            {
                // Update elapsed time
                _fltElapsedTime += Time.deltaTime;

                // Calculate progress (0 to 1)
                float _fltProgress = Mathf.Clamp01(_fltElapsedTime / _fltScrollDuration);

                // Interpolate between current and target scroll offsets
                Vector2 _vectNewScrollOffset = Vector2.Lerp(_scrollRectCarousel.scrollOffset, _vectTargetScrollOffset, _fltProgress);

                // Apply the new scroll offset
                _scrollRectCarousel.scrollOffset = _vectNewScrollOffset;

                // Stop scrolling when the animation is complete
                if (_fltProgress >= 1f)
                {
                    _isScrolling = false;
                }
            }
        }
        private void HighlightCurrentIndicator()
        {
            for (int i = 0; i < _indicatorContainer.childCount; i++)
            {
                VisualElement indicator = _indicatorContainer[i];
                if (i == _currentIndex)
                {
                    indicator.AddToClassList("active");
                }
                else
                {
                    indicator.RemoveFromClassList("active");
                }
            }
        }
        private void OnCarouselDragStart(PointerDownEvent _evt)
        {
            _veCarouselParent.CapturePointer(_evt.pointerId);

            _vectLastPointerPosition = _evt.position;

            DisableCarousel();
            _isDraggingCarousel = true;

        }

        public void SetFlag(bool SSODisabled = false)
        {
            _ssoDisabled = SSODisabled;
        }

        private void OnCarouselDragEnd(PointerUpEvent _evt)
        {
            _veCarouselParent.ReleasePointer(_evt.pointerId);
            if (!_isDraggingCarousel) return;
            Vector2 _vectCurrentPosition = _evt.position;

            Vector2 delta = _vectCurrentPosition - _vectLastPointerPosition;

            // Determine the drag direction
            if (delta.x > 0f)
            {
                if (_currentIndex - 1 >= 0)
                    _currentIndex--;
            }
            else if (delta.x < -0f)
            {
                if (_currentIndex + 1 < _carouselImage.Count)
                    _currentIndex++;
            }
            SlideToNextItem(_currentIndex);

            EnableCarousel();
            _isDraggingCarousel = false;
        }

        private VisualElement FindClosestElement(float currentScrollPos)
        {
            VisualElement closestElement = null;
            float minDistance = float.MaxValue;

            foreach (VisualElement _veChild in _carouselImage)
            {
                float elementCenter = _veChild.worldBound.x + (_veChild.worldBound.width / 2);
                float distance = Mathf.Abs(currentScrollPos - elementCenter);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestElement = _veChild;
                }
            }

            return closestElement;
        }

        #endregion

        private enum CredentialProvider
        {
            Email,
            Google,
            Facebook,
            Apple,
        }

        private class UserCredential
        {
            public string Username;
            public string CredentialIconStyle;
            public CredentialProvider CredentialProvider;
        }

    }
}
