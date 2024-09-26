using System;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using System.Text.RegularExpressions;

namespace com.noctuagames.sdk.UI
{
    internal class UserCenterPresenter : Presenter<AuthenticationModel>
    {
        private VisualTreeAsset _itemTemplate;
        private Texture2D _defaultAvatar;
        private ListView _credentialListView;
        private Label _stayConnect;
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
        private DropdownField _currencyTF;
        private VisualElement _profileImage;
        private VisualElement _playerImage;
        private Button _changeProfile;
        private Label _userIDLabel;
        private string _newProfileUrl;
        private string _userIDValue;
        private ProfileOptionData _profileDataOptions;
        private List<string> _countryOptions = new List<string> { "Select Country" };
        private List<string> _languageOptions = new List<string> { "Select Languages" };
        private List<string> _currencyOptions = new List<string> { "Select Currency" };

        //Date Picker

        // Regular expression to match the DD/MM/YYYY format
        private readonly string datePattern = @"^([0-2][0-9]|(3)[0-1])/((0)[0-9]|(1)[0-2])/((19|20)\d\d)$";

        // Suggest Bind UI
        private VisualElement _guestContainer;
        private Label _carouselLabel;
        private VisualElement _indicatorContainer;

        private readonly string[] _carouselItems = { 
            "Unlock special benefits by owning a Noctua account", 
            "Protect your hard-earned progress and achievements",
            "Enjoy the flexibility to play your game on any device"
            };
        private int _currentIndex  = 0;
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
                if (Model.AuthService.RecentAccount == null)
                {
                    throw new NoctuaException(NoctuaErrorCode.Authentication, "No account is logged in.");
                }
                
                var user = await Model.AuthService.GetCurrentUser();
                var isGuest = user?.IsGuest == true;
                
                Debug.Log($"GetCurrentUser: {user?.Id} {user?.Nickname}");

                View.Q<Label>("PlayerName").text = isGuest ? "Guest " + user.Id  : user?.Nickname;
                View.Q<Label>("UserIdLabel").text = user?.Id.ToString() ?? "";
                _userIDValue = user?.Id.ToString() ?? "";

                //Edit Profile - Setup Data
                if(!isGuest) 
                {
                    var profileOptions = await Model.AuthService.GetProfileOptions();
                    _profileDataOptions = profileOptions;
                    
                    setupDropdownUI();
                    
                    _nicknameTF.value = user?.Nickname;
                    _newProfileUrl = user?.PictureUrl;

                    
                    bool validDate = DateTime.TryParse(user?.DateOfBirth, null, DateTimeStyles.RoundtripKind, out DateTime dateTime);
                    string formattedDate = validDate ? dateTime.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "";

                    string genderOriginal = user?.Gender;
                    string genderUpperChar = char.ToUpper(genderOriginal[0]) + genderOriginal.Substring(1);
                    
                    _birthDateTF.value = formattedDate;
                    _genderTF.value = genderUpperChar;


                    int indexCountry = _profileDataOptions.Countries.FindIndex(item => item.IsoCode.ToLower() == user?.Country.ToLower());
                    if (indexCountry != -1)
                    {
                        _countryTF.value = _countryOptions[indexCountry];
                    }

                    int indexLanguage = _profileDataOptions.Languages.FindIndex(item => item.IsoCode.ToLower() == user?.Language.ToLower());
                    if (indexLanguage != -1)
                    {
                        _languageTF.value = _languageOptions[indexLanguage];
                    }

                    int indexCurrency = _profileDataOptions.Currencies.FindIndex(item => item.IsoCode.ToLower() == user?.Currency.ToLower());
                    if (indexCurrency != -1)
                    {
                        _currencyTF.value = _currencyOptions[indexCountry];
                    }
                }

                _isGuestUser = user?.IsGuest ?? false;
                UpdateUIGuest(isGuest);
                
                if (!string.IsNullOrEmpty(user?.PictureUrl))
                {
                    var www = UnityWebRequestTexture.GetTexture(user.PictureUrl);

                    await www.SendWebRequest().ToUniTask();
                
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        var picture = DownloadHandlerTexture.GetContent(www);
                        View.Q<VisualElement>("PlayerAvatar").style.backgroundImage = new StyleBackground(picture);
                    }
                }
                else
                {
                    View.Q<VisualElement>("PlayerAvatar").style.backgroundImage = new StyleBackground(_defaultAvatar);
                }

                if (!isGuest) {
                    foreach (var t in _credentials)
                    {
                        Debug.Log($"Credential: {t.CredentialProvider} {t.Username}");
                        
                        var credential = user?.Credentials.Find(c => c.Provider == t.CredentialProvider.ToString().ToLower());
                        t.Username = credential?.DisplayText ?? "";
                    }

                    _credentialListView.Rebuild();
                }
                
                Visible = true;
                SetOrientation();
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
                
                _credentials.ForEach(c => c.Username = "");
                Model.ShowGeneralNotificationError(e.Message);
            }
        }

        private void Awake()
        {
            _defaultAvatar = Resources.Load<Texture2D>("PlayerProfileBackground");

            _stayConnect = View.Q<Label>("ConnectAccountLabel");
            _hiTextContainer = View.Q<VisualElement>("HiText");
            _playerName = View.Q<Label>("PlayerName");
            _moreOptionsMenuButton = View.Q<Button>("MoreOptionsButton");
            _helpButton = View.Q<Button>("HelpButton");
            _copyIcon = View.Q<VisualElement>("CopyIcon");
            _connectAccountFooter = View.Q<VisualElement>("ConnectAccountFooter");

            View.Q<VisualElement>("MoreOptionsMenu").RegisterCallback<PointerUpEvent>(OnMoreOptionsMenuSelected);
            View.Q<VisualElement>("EditProfile").RegisterCallback<PointerUpEvent>(_ => OnEditProfile());
            View.Q<VisualElement>("BackEditProfileHeader").RegisterCallback<PointerUpEvent>(_carouselItems => OnBackEditProfile());
            View.Q<VisualElement>("SwitchProfile").RegisterCallback<PointerUpEvent>(_ => OnSwitchProfile());
            View.Q<VisualElement>("LogoutAccount").RegisterCallback<PointerUpEvent>(_ => OnLogout());

            //Suggest Bind UI
            _guestContainer = View.Q<VisualElement>("UserGuestUI");
            _guestContainer.AddToClassList("hide");

            //Edit Profile UI
            SetupEditProfileUI();

            View.Q<Button>("SaveButton").RegisterCallback<PointerUpEvent>(_ => OnSaveEditProfile());
        }

        private void SetupDatePickerUI()
        {
            _birthDateTF = View.Q<TextField>("BirthdateTF");

            // Register the callback for each text change
            _birthDateTF.RegisterCallback<ChangeEvent<string>>(evt => OnDateFieldChanged(evt.newValue));

            // Register the callback for when the field loses focus
            _birthDateTF.RegisterCallback<FocusOutEvent>(evt => ValidateDate());

        }

        private void OnDateFieldChanged(string newValue)
        {
            string formattedValue = AutoFormatDate(newValue);

            AdjustHideLabelElement(_birthDateTF);
            
            // To prevent the callback from triggering an infinite loop, we need to check if the value has changed
            if (_birthDateTF.value != formattedValue)
            {
                _birthDateTF.value = formattedValue;
            }
        }

        private string AutoFormatDate(string input)
        {
            // Remove all non-numeric characters
            string digitsOnly = Regex.Replace(input, @"[^0-9]", "");

            // Format as DD/MM/YYYY
            if (digitsOnly.Length > 8)
                digitsOnly = digitsOnly.Substring(0, 8); // Limit input length to 8 digits

            switch (digitsOnly.Length)
            {
                case > 6:
                    return $"{digitsOnly.Substring(0, 2)}/{digitsOnly.Substring(2, 2)}/{digitsOnly.Substring(4, 4)}";
                case > 4:
                    return $"{digitsOnly.Substring(0, 2)}/{digitsOnly.Substring(2, 2)}/{digitsOnly.Substring(4)}";
                case > 2:
                    return $"{digitsOnly.Substring(0, 2)}/{digitsOnly.Substring(2)}";
                default:
                    return digitsOnly;
            }
        }

        private void ValidateDate()
        {
            string dateText = _birthDateTF.value;

            if (IsDateValid(dateText))
            {
                _birthDateTF.style.borderTopColor = Color.green;
                _birthDateTF.style.borderBottomColor = Color.green;
                _birthDateTF.style.borderLeftColor = Color.green;
                _birthDateTF.style.borderRightColor = Color.green;
            }
            else
            {
                _birthDateTF.style.borderTopColor = Color.red;
                _birthDateTF.style.borderBottomColor = Color.red;
                _birthDateTF.style.borderLeftColor = Color.red;
                _birthDateTF.style.borderRightColor = Color.red;

                Debug.LogError("Invalid date format! Please use DD/MM/YYYY.");
            }
        }

        private bool IsDateValid(string dateText)
        {
            // Use regular expression to check if the input matches the DD/MM/YYYY format
            return Regex.IsMatch(dateText, datePattern);
        }

        private void SetupEditProfileUI() 
        {
            _editProfileContainer = View.Q<VisualElement>("EditProfileBox");
            _editProfileContainer.RemoveFromClassList("show");
            _editProfileContainer.AddToClassList("hide");

            _nicknameTF = View.Q<TextField>("NicknameTF");
            _genderTF = View.Q<DropdownField>("GenderTF");
            _countryTF = View.Q<DropdownField>("CountryTF");
            _languageTF = View.Q<DropdownField>("LanguageTF");
            _currencyTF = View.Q<DropdownField>("CurrencyTF");
            _changeProfile = View.Q<Button>("ChangePictureButton");
            _profileImage = View.Q<VisualElement>("ProfileImage");
            _playerImage = View.Q<VisualElement>("PlayerAvatar");
            _userIDLabel = View.Q<Label>("UserIdLabel");

            _nicknameTF.RegisterValueChangedCallback(evt => OnTextChanged(_nicknameTF));
            _changeProfile.RegisterCallback<PointerUpEvent>(OnChangeProfile);

            setupDropdownUI();
        }

        private async void setupDropdownUI() {
            var genderChoices = new List<string> {"Male", "Female"};

            if(_profileDataOptions != null)
            {
                _countryOptions.Clear();
                _languageOptions.Clear();
                _currencyOptions.Clear();

                foreach(GeneralProfileData _country in _profileDataOptions.Countries)
                {
                    _countryOptions.Add(_country.EnglishName);
                }

                foreach(GeneralProfileData _languages in _profileDataOptions.Languages)
                {
                    _languageOptions.Add(_languages.EnglishName);
                }

                foreach(GeneralProfileData _currency in _profileDataOptions.Currencies)
                {
                    _currencyOptions.Add(_currency.EnglishName);
                }

            }

            _genderTF.choices = genderChoices; 
            _genderTF.RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                _genderTF.value = evt.newValue;
                _genderTF.labelElement.style.display = DisplayStyle.None;
            });

            _countryTF.choices = _countryOptions; 
            _countryTF.RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                _countryTF.value = evt.newValue;
                _countryTF.labelElement.style.display = DisplayStyle.None;
            });

            _languageTF.choices = _languageOptions;
            _languageTF.RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                _languageTF.value = evt.newValue;
                _languageTF.labelElement.style.display = DisplayStyle.None;
            });

            _currencyTF.choices = _currencyOptions;
            _currencyTF.RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                _currencyTF.value = evt.newValue;
                _currencyTF.labelElement.style.display = DisplayStyle.None;
            });

        }

        private async void OnChangeProfile(PointerUpEvent evt)
        {
            if(NativeGallery.IsMediaPickerBusy())
            {
                return;
            }

            RequestPermissionAsynchronously(NativeGallery.PermissionType.Read, NativeGallery.MediaType.Image);
        }

        private async void RequestPermissionAsynchronously(NativeGallery.PermissionType permissionType, NativeGallery.MediaType mediaTypes)
        {
            NativeGallery.Permission permission = await NativeGallery.RequestPermissionAsync(permissionType, mediaTypes);
            if(permission == NativeGallery.Permission.Granted) 
            {
                PickImage();
            }
        }

        private void PickImage()
        {
            NativeGallery.Permission permission = NativeGallery.GetImageFromGallery( ( path ) =>
            {
                if( path != null )
                {
                   FileUploader(path);
                }
            } );

            Debug.Log( "Permission result: " + permission );
        }

        private async void FileUploader(string filePath) {
            try
            {
                _newProfileUrl = await Model.AuthService.FileUploader(filePath);
                StartCoroutine(LoadImageFromUrl(_newProfileUrl));
            }
            catch (Exception e)
            {
                Model.ShowGeneralNotificationError(e.Message);
            }
        }

        private IEnumerator LoadImageFromUrl(string url)
        {
            using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError(www.error);
                }
                else
                {
                    Texture2D texture = ((DownloadHandlerTexture)www.downloadHandler).texture;
                    _profileImage.style.backgroundImage = new StyleBackground(texture);
                }
            }
        }

        private void OnEditProfile() 
        {
            //remove class
            _guestContainer.RemoveFromClassList("show");
            _stayConnect.RemoveFromClassList("show");
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
            _connectAccountFooter.AddToClassList("hide");
            _playerImage.AddToClassList("profile-menu-image");
            _playerImage.style.backgroundImage = Resources.Load<Texture2D>("EditProfileImage");

            _editProfileContainer.AddToClassList("show");

            _userIDLabel.text = "Edit Profile";
            _userIDLabel.style.fontSize = 16;
        }

        private void OnBackEditProfile() 
        {
            //remove class
            _editProfileContainer.RemoveFromClassList("show");
            _guestContainer.RemoveFromClassList("hide");
            _stayConnect.RemoveFromClassList("hide");
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
            _playerImage.style.backgroundImage = _defaultAvatar;

            _userIDLabel.text = _userIDValue;
            _userIDLabel.style.fontSize = 12;

            if(_isGuestUser) {
                _credentialListView.AddToClassList("hide");
                _stayConnect.AddToClassList("hide");
                _guestContainer.AddToClassList("show");
            } else {
                _guestContainer.AddToClassList("hide");
                _credentialListView.AddToClassList("show");
                _stayConnect.AddToClassList("show");
            }
            
        }

        private void OnTextChanged(TextField textField)
        {
            AdjustHideLabelElement(textField);
        }

        private async void OnSaveEditProfile()
        {
            try
            {
                EditProfileRequest editProfileRequest = new EditProfileRequest();

                var _dob = _birthDateTF.value;
                string format = "dd/MM/yyyy";
                
                // Convert to DateTime object using ParseExact method
                DateTime _dateTime = DateTime.ParseExact(_dob, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

                // Set the time to 00:00:00 and the kind to UTC
                _dateTime = new DateTime(_dateTime.Year, _dateTime.Month, _dateTime.Day, 0, 0, 0, DateTimeKind.Utc);

                editProfileRequest.Nickname = _nicknameTF.value;
                editProfileRequest.DateOfBirth = _dateTime;
                editProfileRequest.Gender = _genderTF.value.ToLower();
                editProfileRequest.PictureUrl = _newProfileUrl;

                int indexCountry = _profileDataOptions.Countries.FindIndex(item => item.EnglishName.ToLower() == _countryTF.value.ToLower());
                int indexLanguage = _profileDataOptions.Languages.FindIndex(item => item.EnglishName.ToLower() == _languageTF.value.ToLower());
                int indexCurrency = _profileDataOptions.Currencies.FindIndex(item => item.EnglishName.ToLower() == _currencyTF.value.ToLower());

                editProfileRequest.Country = _profileDataOptions.Countries[indexCountry].IsoCode;
                editProfileRequest.Language = _profileDataOptions.Languages[indexLanguage].IsoCode;
                editProfileRequest.Currency = _profileDataOptions.Currencies[indexCurrency].IsoCode;

                await Model.AuthService.EditProfile(editProfileRequest);

                Debug.Log("Update profile success");
            }
            catch (Exception e)
            {
                Model.ShowGeneralNotificationError(e.Message);
            }
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
            _carouselLabel = View.Q<Label>("TextCarousel");
            _indicatorContainer = View.Q<VisualElement>("IndicatorContainer");

            View.Q<Button>("ExitButton").RegisterCallback<PointerUpEvent>(_ => { Visible = false; });
            View.Q<Button>("MoreOptionsButton").RegisterCallback<PointerUpEvent>(OnMoreOptionsButtonClick);
            View.Q<Button>("GuestConnectButton").RegisterCallback<PointerUpEvent>(OnGuestConnectButtonClick);
            View.Q<VisualElement>("DeleteAccount").RegisterCallback<PointerUpEvent>(_ => OnDeleteAccount());
            View.RegisterCallback<GeometryChangedEvent>(_ => SetOrientation());
            
            View.RegisterCallback<PointerDownEvent>(OnViewClicked);

            SetupDatePickerUI();
            
            BindListView();
            SetupIndicators();

            UpdateCarouselText();
            HighlightCurrentIndicator();

            InvokeRepeating(nameof(SlideToNextItem), SlideInterval, SlideInterval);
        }
        
        private void OnDisable() 
        {
            _editProfileContainer.RemoveFromClassList("show");
            _editProfileContainer.AddToClassList("hide");

            CancelInvoke(nameof(SlideToNextItem));
        }

        private void OnDeleteAccount()
        {
            Visible = false;
            Model.ShowAccountDeletionConfirmation(Model.AuthService.RecentAccount);
        }

        private void OnMoreOptionsButtonClick(PointerUpEvent evt)
        {
            Debug.Log("More options clicked");
            ToggleMoreOptionsMenu();
            evt.StopPropagation();
        }

        private void OnGuestConnectButtonClick(PointerUpEvent evt)
        {
            Debug.Log("Guest connect clicked");

            View.visible = false;
            
            Model.PushNavigation(() => Model.ShowAccountSelection());
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
                    Model.ShowEmailRegistration(true);
                    
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
                _ = await Model.SocialLinkAsync(provider);
            }
            catch (Exception e)
            {
                Model.ShowGeneralNotificationError(e.Message);
            }
                        
            Model.ShowUserCenter();
        }

        private void UpdateUIGuest(bool isGuest) {
            var moreOptionsButton = View.Q<Button>("MoreOptionsButton");
            var guestContainer = View.Q<VisualElement>("UserGuestUI");
            var stayConnect = View.Q<Label>("ConnectAccountLabel");

            if(isGuest) {
                moreOptionsButton.AddToClassList("hide");
                moreOptionsButton.RemoveFromClassList("show");
                _credentialListView.AddToClassList("hide");
                _credentialListView.RemoveFromClassList("show");
                stayConnect.AddToClassList("hide");
                stayConnect.RemoveFromClassList("show");
                guestContainer.AddToClassList("show");
                guestContainer.RemoveFromClassList("hide");
            } else {
                moreOptionsButton.AddToClassList("show");
                moreOptionsButton.RemoveFromClassList("hide");
                _credentialListView.AddToClassList("show");
                _credentialListView.RemoveFromClassList("hide");
                stayConnect.AddToClassList("show");
                stayConnect.RemoveFromClassList("hide");
                guestContainer.AddToClassList("hide");
                guestContainer.RemoveFromClassList("show");
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
            UpdateCarouselText();
            HighlightCurrentIndicator();
        }

        private void UpdateCarouselText()
        {
            _carouselLabel.text = _carouselItems[_currentIndex];
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
         private void AdjustHideLabelElement(TextField textField) {
            if(string.IsNullOrEmpty(textField.value)) {
                textField.labelElement.style.display = DisplayStyle.Flex;
            } else {
                textField.labelElement.style.display = DisplayStyle.None;
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