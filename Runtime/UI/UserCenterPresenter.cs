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

namespace com.noctuagames.sdk.UI
{
    internal class UserCenterPresenter : Presenter<AuthenticationModel>
    {
        public EventSender EventSender;

        private readonly ILogger _log = new NoctuaLogger();

        private VisualTreeAsset _itemTemplate;
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

        //Edit Profile UI
        private VisualElement _editProfileContainer;
        private TextField _nicknameTF;
        private TextField _birthDateTF;
        private DropdownField _genderTF;
        private DropdownField _countryTF;
        private DropdownField _languageTF;
        private VisualElement _profileImage;
        private VisualElement _playerImage;
        private Button _changeProfile;
        private Label _userIDLabel;
        private string _newProfileUrl;
        private string _userIDValue;
        private ProfileOptionData _profileDataOptions;
        private List<string> _countryOptions = new List<string> { "Select Country" };
        private List<string> _languageOptions = new List<string> { "Select Languages" };
        private VisualElement _spinner;
        private VisualElement _noctuaLogoWithText;
        private Label _sdkVersion;
        private string _dateString;

        // Suggest Bind UI
        private VisualElement _guestContainer;
        private VisualElement _carouselVE;
        private Label _carouselLabel;
        private VisualElement _indicatorContainer;
        private Dictionary<string, string> _translations;
        private readonly string[] _carouselImage =
        {
            "UC_Guest_1",
            "UC_Guest_2",
            "UC_Guest_3"
        };

        private readonly string[] _carouselItems = {
            "SuggestionBindText.Content1",
            "SuggestionBindText.Content2",
            "SuggestionBindText.Content3"
            };

        private string[] carouselTranslate;

        private int _currentIndex = 0;
        private const float SlideInterval = 3f;
        private bool _isGuestUser = false;
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
        };

        private GlobalConfig _globalConfig;

        protected override void Attach()
        {
        }

        protected override void Detach()
        {
        }

        private void SetOrientation(bool isEditProfile = false)
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

                if (isEditProfile)
                {
                    View.Q<VisualElement>("ConnectAccount").RemoveFromClassList("portrait");
                    View.Q<VisualElement>("ConnectAccount").RemoveFromClassList("connect-account");
                    View.Q<VisualElement>("ConnectAccount").AddToClassList("connect-account-edit-profile-portrait");

                    View.Q<VisualElement>("UserProfile").AddToClassList("hide");
                    View.Q<VisualElement>("UserCenter").style.maxHeight = Length.Percent(65);
                    View.Q<VisualElement>("ScrollViewContainer").style.marginTop = 10;
                }
                else
                {
                    View.Q<VisualElement>("ConnectAccount").RemoveFromClassList("connect-account-edit-profile-portrait");
                    View.Q<VisualElement>("ConnectAccount").AddToClassList("connect-account");
                    View.Q<VisualElement>("ConnectAccount").AddToClassList("portrait");

                    View.Q<VisualElement>("UserProfile").RemoveFromClassList("hide");
                    View.Q<VisualElement>("UserCenter").style.maxHeight = StyleKeyword.Null;
                    View.Q<VisualElement>("ScrollViewContainer").style.marginTop = StyleKeyword.Null;
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
                Model.ShowLoadingProgress(true);
                if (Model.AuthService.RecentAccount == null)
                {
                    Model.ShowLoadingProgress(false);
                    throw new NoctuaException(NoctuaErrorCode.Authentication, "No account is logged in.");
                }

                var user = await Model.AuthService.GetUserAsync();
                var isGuest = user?.IsGuest == true;

                _log.Debug($"current user in user center is '{user?.Id} - {user?.Nickname}'");

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

                    _nicknameTF.value = user?.Nickname;
                    _newProfileUrl = user?.PictureUrl;

                    bool validDate = DateTime.TryParse(user?.DateOfBirth, null, DateTimeStyles.RoundtripKind, out DateTime dateTime);
                    string formattedDate = validDate ? dateTime.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "";

                    string genderOriginal = user?.Gender;
                    string genderUpperChar = char.ToUpper(genderOriginal[0]) + genderOriginal.Substring(1);

                    _birthDateTF.value = formattedDate;
                    _genderTF.value = genderUpperChar;

                    _dateString = formattedDate;

                    int indexCountry = _profileDataOptions.Countries.FindIndex(item => item.IsoCode.ToLower() == user?.Country.ToLower());
                    if (indexCountry != -1)
                    {
                        _countryTF.value = _countryOptions[indexCountry];
                    }
                    else
                    {
                        _countryTF.value = "Select Country";
                    }

                    int indexLanguage = _profileDataOptions.Languages.FindIndex(item => item.IsoCode.ToLower() == user?.Language.ToLower());
                    if (indexLanguage != -1)
                    {
                        _languageTF.value = _languageOptions[indexLanguage];
                    }
                    else
                    {
                        _languageTF.value = "Select Language";
                    }

                    SetupEditProfileUI();
                }

                _isGuestUser = user?.IsGuest ?? false;
                UpdateUIGuest(isGuest);

                if (!string.IsNullOrEmpty(user?.PictureUrl))
                {
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

        private void Awake()
        {
            _defaultAvatar = Resources.Load<Texture2D>("DefaultAvatar");

            _stayConnect = View.Q<Label>("ConnectAccountLabel");
            _containerStayConnect = View.Q<VisualElement>("ContainerStayConnect");
            _hiTextContainer = View.Q<VisualElement>("HiText");
            _playerName = View.Q<Label>("PlayerName");
            _moreOptionsMenuButton = View.Q<Button>("MoreOptionsButton");
            _helpButton = View.Q<Button>("HelpButton");
            _copyIcon = View.Q<VisualElement>("CopyIcon");
            _noctuaLogoWithText = View.Q<VisualElement>("NoctuaLogoContainer");
            _connectAccountFooter = View.Q<VisualElement>("ConnectAccountFooter");
            _sdkVersion = View.Q<Label>("SDKVersion");
            _sdkVersion.text = $"v{Assembly.GetExecutingAssembly().GetName().Version}";

            View.Q<VisualElement>("MoreOptionsMenu").RegisterCallback<PointerUpEvent>(OnMoreOptionsMenuSelected);
            View.Q<VisualElement>("EditProfile").RegisterCallback<PointerUpEvent>(_ => OnEditProfile());
            View.Q<VisualElement>("BackButton").RegisterCallback<ClickEvent>(_ => OnBackEditProfile());
            View.Q<VisualElement>("SwitchProfile").RegisterCallback<PointerUpEvent>(_ => OnSwitchProfile());
            View.Q<VisualElement>("LogoutAccount").RegisterCallback<PointerUpEvent>(_ => OnLogout());
            View.Q<VisualElement>("PendingPurchases").RegisterCallback<PointerUpEvent>(_ => OnPendingPurchases());
            View.Q<VisualElement>("FindMoreLabel").RegisterCallback<PointerUpEvent>(_ => OnFindMore());

            _helpButton.RegisterCallback<PointerUpEvent>(OnHelp);
            _copyIcon.RegisterCallback<PointerUpEvent>(_ => OnCopyText());

            //Suggest Bind UI
            _guestContainer = View.Q<VisualElement>("UserGuestUI");
            _guestContainer.AddToClassList("hide");

            //Edit Profile UI
            SetupEditProfileUI();
        }

        private void OnFindMore()
        {
            _log.Debug("on find more clicked");

            var findMorelUrl = _globalConfig?.CoPublisher?.CompanyWebsiteUrl ?? "https://noctua.gg";

            Application.OpenURL(findMorelUrl);
        }

        private async void OnHelp(PointerUpEvent evt)
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
            _birthDateTF = View.Q<TextField>("BirthdateTF");
            _birthDateTF.isReadOnly = true;
            _birthDateTF.focusable = false;

            string _dob = string.IsNullOrEmpty(_dateString) ? "01/01/2000" : _dateString;
            DateTime parsedDate = DateTime.ParseExact(_dob, "dd/MM/yyyy", null);

            _birthDateTF.RegisterCallback<ClickEvent>(upEvent =>
            {

                upEvent.StopImmediatePropagation();

                Noctua.OpenDatePicker(parsedDate.Year, parsedDate.Month, parsedDate.Day, 1,
                (DateTime _date) =>
                {
                    _log.Debug($"picked date '{_date:O}'");
                },
                (DateTime _date) =>
                {
                    _birthDateTF.value = _date.ToString("dd/MM/yyyy");

                });
            });

            // Register the callback for each text change
            _birthDateTF.RegisterCallback<ChangeEvent<string>>(evt => OnDateFieldChanged());
        }

        private void OnDateFieldChanged()
        {
            _dateString = _birthDateTF.value;
            AdjustHideLabelElement(_birthDateTF);
        }

        private void SetupEditProfileUI()
        {
            _editProfileContainer = View.Q<VisualElement>("EditProfileBox");

            _nicknameTF = View.Q<TextField>("NicknameTF");
            _genderTF = View.Q<DropdownField>("GenderTF");
            _countryTF = View.Q<DropdownField>("CountryTF");
            _languageTF = View.Q<DropdownField>("LanguageTF");
            _changeProfile = View.Q<Button>("ChangePictureButton");
            _profileImage = View.Q<VisualElement>("ProfileImage");
            _playerImage = View.Q<VisualElement>("PlayerAvatar");
            _userIDLabel = View.Q<Label>("UserIdLabel");

            var saveButton = View.Q<Button>("SaveButton");
            saveButton.SetEnabled(false);
            saveButton.RegisterCallback<PointerUpEvent>(_ => OnSaveEditProfile());

            var elementNames = new List<string>
            {
                "NicknameTF",
                "GenderTF",
                "CountryTF",
                "LanguageTF",
            };

            Utility.RegisterForMultipleValueChanges<string>(View, elementNames, saveButton);

            _nicknameTF.RegisterValueChangedCallback(evt => OnTextChanged(_nicknameTF));
            _changeProfile.RegisterCallback<PointerUpEvent>(OnChangeProfile);

            _nicknameTF.RegisterCallback<FocusInEvent>(OnTextFieldFocusChange);
            _nicknameTF.RegisterCallback<FocusOutEvent>(OnTextFieldFocusChange);

            SetupDatePickerUI();
            SetupDropdownUI();
            SetupSpinner();
        }

        public void OnTextFieldFocusChange(FocusInEvent _event)
        {
            HideAllErrors();
            (_event.target as VisualElement).Children().ElementAt(1).AddToClassList("noctua-text-input-focus");
            (_event.target as VisualElement).Q<VisualElement>("title").style.color = ColorModule.white;
        }

        public void OnTextFieldFocusChange(FocusOutEvent _event)
        {
            (_event.target as VisualElement).Children().ElementAt(1).RemoveFromClassList("noctua-text-input-focus");
            (_event.target as VisualElement).Q<VisualElement>("title").style.color = ColorModule.greyInactive;
        }

        public void HideAllErrors()
        {
            _nicknameTF.Children().ElementAt(1).RemoveFromClassList("noctua-text-input-error");
            _countryTF.Children().ElementAt(1).RemoveFromClassList("noctua-text-input-error");
            _languageTF.Children().ElementAt(1).RemoveFromClassList("noctua-text-input-error");

            _nicknameTF.Q<Label>("error").AddToClassList("hide");
            _countryTF.Q<Label>("error").AddToClassList("hide");
            _languageTF.Q<Label>("error").AddToClassList("hide");
        }

        private void SetupDropdownUI()
        {
            var genderChoices = new List<string> { "Male", "Female" };

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

            _genderTF.choices = genderChoices;
            _genderTF.RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                _genderTF.value = evt.newValue;
                _genderTF.labelElement.style.display = DisplayStyle.None;
                _genderTF.Q<VisualElement>("title").RemoveFromClassList("hide");
            });

            _countryTF.choices = _countryOptions;
            _countryTF.RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                _countryTF.value = evt.newValue;
                _countryTF.labelElement.style.display = DisplayStyle.None;
                _countryTF.Q<VisualElement>("title").RemoveFromClassList("hide");
            });

            _languageTF.choices = _languageOptions;
            _languageTF.RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                _languageTF.value = evt.newValue;
                _languageTF.labelElement.style.display = DisplayStyle.None;
                _languageTF.Q<VisualElement>("title").RemoveFromClassList("hide");
            });

        }

        private void OnChangeProfile(PointerUpEvent evt)
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
            View.Q<Button>("ChangePictureButton").SetEnabled(false);

            try
            {
                _newProfileUrl = await Model.AuthService.FileUploader(filePath);

                StartCoroutine(LoadImageFromUrl(_newProfileUrl, true));

                View.Q<Button>("ChangePictureButton").SetEnabled(true);

                SaveProfile();
            }
            catch (Exception e)
            {
                Model.ShowGeneralNotification(e.Message);
                View.Q<Button>("ChangePictureButton").SetEnabled(true);
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
                    }
                    else
                    {
                        _playerImage.style.backgroundImage = new StyleBackground(texture);
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

            OnUIEditProfile(false);
        }

        private void OnUIEditProfile(bool isEditProfile)
        {
            SetOrientation(isEditProfile);
            if (isEditProfile)
            {
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
                _noctuaLogoWithText.RemoveFromClassList("hide");

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

                var regionCode = _globalConfig?.Noctua?.Region ?? "";

                _userIDLabel.text = "ID : " + Locale.GetTranslation("UserCenterPresenter.MenuEditProfile.Label.text");
                _userIDLabel.style.fontSize = 16;
                
                View.Q<Label>("TitleEditBack").text = Locale.GetTranslation("UserCenterPresenter.MenuEditProfile.Label.text");

                View.Q<Button>("SaveButton").SetEnabled(false);
                View.Q<VisualElement>("UserProfileHeader").style.justifyContent = Justify.FlexEnd;
            }
            else
            {
                View.Q<VisualElement>("UserProfileHeader").style.justifyContent = Justify.SpaceBetween;

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
                _noctuaLogoWithText.AddToClassList("hide");

                //change player image with profile image
                if (!string.IsNullOrEmpty(_newProfileUrl))
                {
                    StartCoroutine(LoadImageFromUrl(_newProfileUrl, false));
                }
                else
                {
                    _playerImage.style.backgroundImage = _defaultAvatar;
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

                _nicknameTF.value = View.Q<Label>("PlayerName").text;
                SetErrorUpdateProfile(false);
                HideAllErrors();

            }
        }


        private void OnTextChanged(TextField textField)
        {
            AdjustHideLabelElement(textField);
        }

        private void SetupSpinner()
        {
            _spinner = new Spinner();
            View.Q<VisualElement>("Spinner").Clear();
            View.Q<VisualElement>("Spinner").Add(_spinner);
            View.Q<VisualElement>("Spinner").AddToClassList("hide");

            View.Q<VisualElement>("Spinner2").Clear();
            View.Q<VisualElement>("Spinner2").Add(_spinner);
            View.Q<VisualElement>("Spinner2").AddToClassList("hide");
        }

        private void OnSaveEditProfile()
        {
            _log.Debug("clicking save edit profile");

            SaveProfile();
        }

        private async void SaveProfile()
        {
            View.Q<VisualElement>("Spinner").RemoveFromClassList("hide");

            View.Q<Button>("ChangePictureButton").SetEnabled(false);
            View.Q<Button>("SaveButton").AddToClassList("hide");

            HideAllErrors();

            if (string.IsNullOrEmpty(_nicknameTF.value))
            {
                View.Q<Button>("SaveButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                View.Q<VisualElement>("Spinner2").AddToClassList("hide");

                _nicknameTF.ElementAt(1).AddToClassList("noctua-text-input-error");
                _nicknameTF.Q<Label>("error").RemoveFromClassList("hide");
                _nicknameTF.Q<Label>("error").text = Locale.GetTranslation("EditProfile.NicknameValidation");
                _nicknameTF.Q<VisualElement>("title").style.color = ColorModule.redError;

                return;
            }

            if (string.IsNullOrEmpty(_countryTF.value) || _countryTF.value == "Select Country")
            {
                View.Q<Button>("SaveButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                View.Q<VisualElement>("Spinner2").AddToClassList("hide");

                _countryTF.ElementAt(1).AddToClassList("noctua-text-input-error");
                _countryTF.Q<Label>("error").RemoveFromClassList("hide");
                _countryTF.Q<Label>("error").text = Locale.GetTranslation("EditProfile.CountryValidation");
                _countryTF.Q<VisualElement>("title").style.color = ColorModule.redError;

                return;
            }

            if (string.IsNullOrEmpty(_languageTF.value) || _languageTF.value == "Select Language")
            {
                View.Q<Button>("SaveButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                View.Q<VisualElement>("Spinner2").AddToClassList("hide");

                _languageTF.ElementAt(1).AddToClassList("noctua-text-input-error");
                _languageTF.Q<Label>("error").RemoveFromClassList("hide");
                _languageTF.Q<Label>("error").text = Locale.GetTranslation("EditProfile.LanguageValidation");
                _languageTF.Q<VisualElement>("title").style.color = ColorModule.redError;

                return;
            }

            try
            {
                UpdateUserRequest updateUserRequest = new UpdateUserRequest();

                updateUserRequest.Nickname = _nicknameTF.value;

                var _dob = _birthDateTF.value;

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

                if (!string.IsNullOrEmpty(_genderTF.value))
                {
                    updateUserRequest.Gender = _genderTF.value.ToLower();
                }

                updateUserRequest.PictureUrl = _newProfileUrl;

                int indexCountry = _profileDataOptions.Countries.FindIndex(item => item.EnglishName.ToLower() == _countryTF.value.ToLower());
                int indexLanguage = _profileDataOptions.Languages.FindIndex(item => item.EnglishName.ToLower() == _languageTF.value.ToLower());

                updateUserRequest.Country = _profileDataOptions.Countries[indexCountry].IsoCode;
                updateUserRequest.Language = _profileDataOptions.Languages[indexLanguage].IsoCode;

                await Model.AuthService.UpdateUserAsync(updateUserRequest);

                if (!string.IsNullOrEmpty(_newProfileUrl))
                {
                    StartCoroutine(LoadImageFromUrl(_newProfileUrl, true));
                }

                View.Q<Button>("SaveButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");

                View.Q<Label>("PlayerName").text = _nicknameTF.value;

                SetErrorUpdateProfile(false);

                Model.ShowGeneralNotification("Update profile successfully", true);

                OnUIEditProfile(false);

                _log.Debug("updated user profile successfully");
            }
            catch (Exception e)
            {
                _log.Exception(e);

                Model.ShowGeneralNotification(e.Message);
                
                SetErrorUpdateProfile(false);
            }
        }

        private void SetErrorUpdateProfile(bool isError)
        {
            if(isError)
            {

                View.Q<Button>("SaveButton").RemoveFromClassList("hide");
                View.Q<Button>("ChangePictureButton").SetEnabled(true);

                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                View.Q<VisualElement>("Spinner2").AddToClassList("hide");
            }
            else
            {
                View.Q<Button>("SaveButton").RemoveFromClassList("hide");
                View.Q<Button>("ChangePictureButton").SetEnabled(true);
                
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                View.Q<VisualElement>("Spinner2").AddToClassList("hide");
            }
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

        private void OnLogout()
        {
            _log.Debug("clicking logout");

            Visible = false;
            OnUIEditProfile(false);
            StartCoroutine(Model.AuthService.LogoutAsync().ToCoroutine());
        }

        private void OnEnable()
        {
            _carouselLabel = View.Q<Label>("TextCarousel");
            _carouselVE = View.Q<VisualElement>("ImageCarousel");
            _indicatorContainer = View.Q<VisualElement>("IndicatorContainer");

            View.Q<Button>("ExitButton").RegisterCallback<PointerUpEvent>(_ =>
            {
                Visible = false;
                OnUIEditProfile(false);
            });
            View.Q<Button>("MoreOptionsButton").RegisterCallback<PointerUpEvent>(OnMoreOptionsButtonClick);
            View.Q<Button>("GuestConnectButton").RegisterCallback<PointerUpEvent>(OnGuestConnectButtonClick);
            View.Q<VisualElement>("DeleteAccount").RegisterCallback<PointerUpEvent>(_ => OnDeleteAccount());
            View.RegisterCallback<GeometryChangedEvent>(_ => SetOrientation());

            View.RegisterCallback<PointerDownEvent>(OnViewClicked);

            BindListView();
            SetupIndicators();

            UpdateCarousel();
            HighlightCurrentIndicator();

            InvokeRepeating(nameof(SlideToNextItem), SlideInterval, SlideInterval);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(SlideToNextItem));
        }

        private void OnDeleteAccount()
        {
            _log.Debug("clicking delete account");

            Visible = false;
            OnUIEditProfile(false);
            Model.ShowAccountDeletionConfirmation(Model.AuthService.RecentAccount);
        }

        private void OnMoreOptionsButtonClick(PointerUpEvent evt)
        {
            _log.Debug("clicking more options button");

            ToggleMoreOptionsMenu();
            evt.StopPropagation();
        }

        private void OnGuestConnectButtonClick(PointerUpEvent evt)
        {
            _log.Debug("clicking guest connect button");

            View.visible = false;

            Model.PushNavigation(() => Model.ShowUserCenter());
            Model.ShowLoginOptions();

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
            _log.Debug("clicking user center view");

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
            _log.Debug("clicking more options menu item");

            View.Q<VisualElement>("MoreOptionsMenu").AddToClassList("hide");
        }

        private void BindListView()
        {
            var credentialFiltered = Utility.ContainsFlag(_globalConfig?.Noctua?.Flags, "VNLegalPurpose") ? _credentials.Where(c => c.CredentialProvider == CredentialProvider.Email).ToList() : _credentials;

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

            switch (credential.CredentialProvider)
            {
                case CredentialProvider.Email:
                    Model.ShowEmailRegistration(true, true);

                    break;
                case CredentialProvider.Google:
                    StartCoroutine(SocialLinkAsync("google").ToCoroutine());
                    break;
                case CredentialProvider.Facebook:
                    StartCoroutine(SocialLinkAsync("facebook").ToCoroutine());
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
            var stayConnect = View.Q<Label>("ConnectAccountLabel");
            var containerStayConnect = View.Q<VisualElement>("ContainerStayConnect");
            var moreOptionsButton = View.Q<Button>("MoreOptionsButton");
            var editProfilebutton = View.Q<VisualElement>("EditProfile");
            var switchProfileButton = View.Q<VisualElement>("SwitchProfile");
            var deleteAccountButton = View.Q<VisualElement>("DeleteAccount");
            var logoutAccountButton = View.Q<VisualElement>("LogoutAccount");
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
            }
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
            _currentIndex = (_currentIndex + 1) % _carouselItems.Length;
            UpdateCarousel();
            HighlightCurrentIndicator();
        }

        private void UpdateCarousel()
        {
            if (carouselTranslate == null) return;
            //var regionCode = _globalConfig?.Noctua?.Region ?? "";

            _carouselLabel.text = carouselTranslate[_currentIndex];
            _carouselVE.style.backgroundImage = new StyleBackground(Resources.Load<Sprite>(_carouselImage[_currentIndex]));
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
        private void AdjustHideLabelElement(TextField textField)
        {
            if (string.IsNullOrEmpty(textField.value))
            {
                textField.labelElement.style.display = DisplayStyle.Flex;
                textField.Q<VisualElement>("title").AddToClassList("hide");
            }
            else
            {
                textField.labelElement.style.display = DisplayStyle.None;
                textField.Q<VisualElement>("title").RemoveFromClassList("hide");
            }
        }

        private enum CredentialProvider
        {
            Email,
            Google,
            Facebook
        }

        private class UserCredential
        {
            public string Username;
            public string CredentialIconStyle;
            public CredentialProvider CredentialProvider;
        }

    }
}
