using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    public static class Constants
    {
        public const string PlayerPrefsKeyAccountContainer = "NoctuaAccountContainer";
        // GAME_ID and USER_ID need to be replaced before use
        public const string CustomerServiceBaseUrl = "https://noctua.gg/embed-webview?url=https%3A%2F%2Fgo.crisp.chat%2Fchat%2Fembed%2F%3Fwebsite_id%3Dc4e95a3a-1fd1-49a2-92ea-a7cb5427bcd9&reason=general&vipLevel=";
    }


    [Preserve, JsonConverter(typeof(StringEnumConverter), typeof(SnakeCaseNamingStrategy))]
    public enum PaymentType
    {
        Unknown,
        Applestore,
        Playstore,
        Noctuawallet
    }
    
    [Preserve]
    public class User
    {
        [JsonProperty("id")]
        public long Id;

        [JsonProperty("nickname")]
        public string Nickname;

        [JsonProperty("email_address")]
        public string EmailAddress;

        [JsonProperty("phone_number")]
        public string PhoneNumbers;

        [JsonProperty("picture_url")]
        public string PictureUrl;
        
        [JsonProperty("credentials")]
        public List<Credential> Credentials;
        
        [JsonProperty("is_guest")]
        public bool IsGuest;

        [JsonProperty("date_of_birth")]
        public string DateOfBirth;

        [JsonProperty("gender")]
        public string Gender;

        [JsonProperty("language")]
        public string Language;

        [JsonProperty("country")]
        public string Country;

        [JsonProperty("currency")]
        public string Currency;

        [JsonProperty("payment_type")]
        public PaymentType PaymentType;
        
        public User ShallowCopy()
        {
            return (User) MemberwiseClone();
        }
    }

    [Preserve]
    public class Credential {
        [JsonProperty("id")]
        public long Id;

        [JsonProperty("provider")]
        public string Provider;
        
        [JsonProperty("display_text")]
        public string DisplayText;
        
        public Credential ShallowCopy()
        {
            return (Credential) MemberwiseClone();
        }
    }

    [Preserve]
    public class Player
    {
        [JsonProperty("access_token")]
        public string AccessToken;

        [JsonProperty("id")]
        public long Id;

        [JsonProperty("role_id")]
        public string RoleId;

        [JsonProperty("server_id")]
        public string ServerId;

        [JsonProperty("username")] // in-game
        public string Username;

        [JsonProperty("game_id")]
        public long GameId;

        [JsonProperty("game_name")]
        public string GameName;

        [JsonProperty("game_platform_id")]
        public long GamePlatformId;

        [JsonProperty("game_platform")]
        public string GamePlatform;

        [JsonProperty("game_os")]
        public string GameOS;

        [JsonProperty("bundle_id")]
        public string BundleId;

        [JsonProperty("user")]
        public User User;

        [JsonProperty("user_id")]
        public long UserId;
        
        public Player ShallowCopy()
        {
            return (Player) MemberwiseClone();
        }
    }

    [Preserve]
    public class Game
    {
        [JsonProperty("id")]
        public long Id;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("platform_id")]
        public long GamePlatformId;

    }

    [Preserve]
    public class GamePlatform
    {
        [JsonProperty("id")]
        public long Id;

        [JsonProperty("os")]
        public string OS;

        [JsonProperty("platform")]
        public string Platform;

        [JsonProperty("bundle_id")]
        public string BundleId;
    }

    [Preserve]
    public class ExchangeTokenRequest {
        // Used for token exchange
        [JsonProperty("next_bundle_id")]
        public string NextBundleId;

        [JsonProperty("init_player")]
        public bool InitPlayer;
    }

    [Preserve]
    public class PlayerToken
    {
        [JsonProperty("access_token")]
        public string AccessToken;


        [JsonProperty("player")]
        public Player Player;

        [JsonProperty("user")]
        public User User;

        [JsonProperty("credential")]
        public Credential Credential;

        [JsonProperty("game")]
        public Game Game;

        [JsonProperty("game_platform")]
        public GamePlatform GamePlatform;
    }


    [Preserve]
    public class UserBundle
    {
        [JsonProperty("user")]
        public User User;

        [JsonProperty("credential")]
        public Credential Credential;

        [JsonProperty("player")]
        public Player Player;

        [JsonProperty("player_accounts")]
        public List<Player> PlayerAccounts;

        [JsonProperty("last_used")]
        public DateTime LastUsed;

        [JsonProperty("is_recent")]
        public bool IsRecent;

        [JsonIgnore]
        public bool IsGuest => User?.IsGuest ?? Credential?.Provider == "device_id";

        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                return this switch
                {
                    {Player: {Username: {Length: > 0}}} => Player.Username,
                    {User: {Nickname: {Length: > 0}}} => User.Nickname,
                    {Credential: {Provider: "device_id"}} => "Guest " + User?.Id,
                    {Credential: {DisplayText: { Length: > 0 } }} => Credential.DisplayText,
                    {User: {Id: > 0}} => "User " + User.Id,
                    _ => "Noctua Player"
                };
            }
        }
    }

    [Preserve]
    public class LoginAsGuestRequest
    {
        [JsonProperty("device_id")]
        public string DeviceId;

        [JsonProperty("bundle_id")]
        public string BundleId;
    }

    [Preserve]
    public class SocialRedirectUrlResponse
    {
        [JsonProperty("redirect_url")]
        public string RedirectUrl;
    }

    [Preserve]
    public class DeletePlayerAccountResponse
    {
        [JsonProperty("is_deleted")]
        public bool IsDeleted;
    }

    [Preserve]
    public class SocialLoginRequest
    {
        [JsonProperty("code")]
        public string Code;

        [JsonProperty("state")]
        public string State;

        [JsonProperty("redirect_uri")]
        public string RedirectUri;
    }

    [Preserve]
    public class SocialLinkRequest
    {
        [JsonProperty("code")]
        public string Code;

        [JsonProperty("state")]
        public string State;

        [JsonProperty("redirect_uri")]
        public string RedirectUri;
    }

    [Preserve]
    public class BindRequest
    {
        [JsonProperty("guest_token")]
        public string GuestToken;
    }

    [Preserve]
    public class CredPair
    {
        [JsonProperty("cred_key")]
        public string CredKey;

        [JsonProperty("cred_secret")]
        public string CredSecret;

        [JsonProperty("provider")]
        public string Provider;
        
        [JsonProperty("reg_extra")]
        public Dictionary<string, string>? RegExtra;
    }

    [Preserve]
    public class CredentialVerification
    {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("code")]
        public string Code;

        [JsonProperty("new_password")] // Used for password reset
        public string NewPassword;
    }

    [Preserve]
    public class PlayerAccountData
    {
        [JsonProperty("ingame_username")]
        public string IngameUsername;

        [JsonProperty("ingame_server_id")]
        public string IngameServerId;

        [JsonProperty("ingame_role_id")]
        public string IngameRoleId;

        [JsonProperty("extra")]
        public Dictionary<string, string> Extra;
    }

    [Preserve]
    public class UpdateUserRequest
    {
        [JsonProperty("nickname")]
        public string Nickname;

        [JsonProperty("date_of_birth")]
        public DateTime? DateOfBirth;
        
        [JsonProperty("gender")]
        public string Gender;

        [JsonProperty("picture_url")]
        public string PictureUrl;

        [JsonProperty("language")]
        public string Language;

        [JsonProperty("country")]
        public string Country;

        [JsonProperty("currency")]
        public string Currency;
        
        [JsonProperty("payment_type")]
        public PaymentType PaymentType;
    }

    [Preserve]
    public class ProfileOptionData
    {
        [JsonProperty("countries")]
        public List<GeneralProfileData> Countries;
        
        [JsonProperty("languages")]
        public List<GeneralProfileData> Languages;

        [JsonProperty("currencies")]
        public List<GeneralProfileData> Currencies;
    }

    [Preserve]
    public class GeneralProfileData
    {
        [JsonProperty("iso_code")]
        public string IsoCode;

        [JsonProperty("native_name")]
        public string NativeName;

        [JsonProperty("english_name")]
        public string EnglishName;
    }
    
    internal class NoctuaAuthenticationService
    {
        public IReadOnlyList<UserBundle> AccountList => _accountContainer.Accounts;

        public IReadOnlyList<UserBundle> CurrentGameAccountList => _accountContainer.CurrentGameAccounts;
        
        public IReadOnlyList<UserBundle> OtherGamesAccountList => _accountContainer.OtherGamesAccounts;

        public bool IsAuthenticated => !string.IsNullOrEmpty(_accountContainer.RecentAccount?.Player?.AccessToken);

        public UserBundle RecentAccount => _accountContainer.RecentAccount;

        public event Action<UserBundle> OnAccountChanged {
            add => _accountContainer.OnAccountChanged += value;
            remove => _accountContainer.OnAccountChanged -= value;
        }

        public event Action<Player> OnAccountDeleted;

        private readonly ILogger _log = new NoctuaUnityDebugLogger();
        private readonly string _clientId;
        private readonly string _baseUrl;
        private readonly AccountContainer _accountContainer;
        private OauthRedirectListener _oauthOauthRedirectListener;

        internal NoctuaAuthenticationService(string baseUrl, string clientId, INativeAccountStore nativeAccountStore)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new ArgumentNullException(nameof(baseUrl));
            }
            
            if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentNullException(nameof(clientId));
            }
            
            _clientId = clientId;
            _baseUrl = baseUrl;
            _accountContainer = new AccountContainer(nativeAccountStore, Application.identifier);
        }

        public async UniTask<UserBundle> LoginAsGuestAsync()
        {
            if (string.IsNullOrEmpty(Application.identifier))
            {
                throw new ApplicationException($"App id for platform {Application.platform} is not set");
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/guest/login")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithJsonBody(
                    new LoginAsGuestRequest
                    {
                        DeviceId = SystemInfo.deviceUniqueIdentifier,
                        BundleId = Application.identifier
                    }
                );


            var response = await request.Send<PlayerToken>();
            
            _accountContainer.UpdateRecentAccount(response);

            return _accountContainer.RecentAccount;
        }

        public async UniTask<UserBundle> ExchangeTokenAsync(string accessToken)
        {
            if (string.IsNullOrEmpty(Application.identifier))
            {
                throw new ApplicationException($"App id for platform {Application.platform} is not set");
            }

            var exchangeToken = new ExchangeTokenRequest
            {
                NextBundleId = Application.identifier,
                InitPlayer = true
            };

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/token-exchange")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + accessToken)
                .WithJsonBody(exchangeToken);


            var response = await request.Send<PlayerToken>();

            _accountContainer.UpdateRecentAccount(response);

            return _accountContainer.RecentAccount;
        }

        public async UniTask<string> GetSocialAuthRedirectURLAsync(string provider, string redirectUri = "")
        {
            if (!string.IsNullOrEmpty(redirectUri))
            {
                redirectUri = $"?redirect_uri={HttpUtility.UrlEncode(redirectUri)}";
            }

            var request = new HttpRequest(HttpMethod.Get, $"{_baseUrl}/auth/{provider}/login/redirect{redirectUri}")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier);

            var redirectUrlResponse = await request.Send<SocialRedirectUrlResponse>();

            return redirectUrlResponse?.RedirectUrl;
        }

        public async UniTask<UserBundle> SocialLoginAsync(string provider, SocialLoginRequest payload)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/{provider}/login/callback")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithJsonBody(payload);

            if (!string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken) && RecentAccount.IsGuest)
            {
                request.WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);
            }

            var response = await request.Send<PlayerToken>();
            
            _accountContainer.UpdateRecentAccount(response);

            return _accountContainer.RecentAccount;
        }

        // TODO: Add support for phone
        public async UniTask<UserBundle> LoginWithEmailAsync(string email, string password)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/login")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithJsonBody(
                    new CredPair
                    {
                        CredKey = email,
                        CredSecret = password
                    }
                );

            if (!string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken) && RecentAccount.IsGuest)
            {
                request.WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);
            }

            var response = await request.Send<PlayerToken>();

            _accountContainer.UpdateRecentAccount(response);

            return _accountContainer.RecentAccount;
        }
        
        // TODO: Add support for phone
        public async UniTask<CredentialVerification> RegisterWithEmailAsync(string email, string password, Dictionary<string, string> regExtra = null)
        {
            
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/register")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithJsonBody(
                    new CredPair
                    {
                        CredKey = email,
                        CredSecret = password,
                        Provider = "email",
                        RegExtra = regExtra
                    }
                );

            if (!string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken) && RecentAccount.User.IsGuest)
            {
                request.WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);
            }

            return await request.Send<CredentialVerification>();
        }

        public async UniTask<UserBundle> VerifyEmailRegistrationAsync(int id, string code)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/verify-registration")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithJsonBody(
                    new CredentialVerification
                    {
                        Id = id,
                        Code = code
                    }
                );

            if (!string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken) && RecentAccount.IsGuest)
            {
                request.WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);
            }

            var response = await request.Send<PlayerToken>();

            _accountContainer.UpdateRecentAccount(response);

            return _accountContainer.RecentAccount;
        }

        // TODO: Add support for phone

        public async UniTask<CredentialVerification> RequestResetPasswordAsync(string email)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/reset-password")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithJsonBody(
                    new CredPair
                    {
                        CredKey = email
                    }
                );

            var response = await request.Send<CredentialVerification>();
            
            return response;
        }

        // TODO: Add support for phone

        public async UniTask<UserBundle> ConfirmResetPasswordAsync(int id, string code, string newPassword)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/verify-reset-password")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithJsonBody(
                    new CredentialVerification
                    {
                        Id = id,
                        Code = code,
                        NewPassword = newPassword,
                    }
                );

            var response = await request.Send<PlayerToken>();
            
            _accountContainer.UpdateRecentAccount(response);

            return _accountContainer.RecentAccount;
        }

        public async UniTask<Credential> SocialLinkAsync(string provider, SocialLinkRequest payload)
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken)) {
                throw NoctuaException.MissingAccessToken;
            }

            if (RecentAccount.IsGuest) {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Guest account cannot link email");
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/{provider}/link/callback")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken)
                .WithJsonBody(payload);

            return await request.Send<Credential>();
        }

        // TODO: Add support for phone

        public async UniTask<CredentialVerification> LinkWithEmailAsync(string email, string password)
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken)) {
                throw NoctuaException.MissingAccessToken;
            }

            if (RecentAccount.IsGuest) {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Guest account cannot link email");
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/link")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken)
                .WithJsonBody(
                    new CredPair
                    {
                        CredKey = email,
                        CredSecret = password,
                        Provider = "email"
                    }
                );

            return await request.Send<CredentialVerification>();
        }

        public async UniTask<Credential> VerifyEmailLinkingAsync(int id, string code)
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken)) {
                throw NoctuaException.MissingAccessToken;
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/verify-link")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken)
                .WithJsonBody(
                    new CredentialVerification
                    {
                        Id = id,
                        Code = code
                    }
                );

            return await request.Send<Credential>();
        }

        public async UniTask<PlayerToken> Bind(BindRequest payload)
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken)) {
                throw NoctuaException.MissingAccessToken;
            }
            
            _log.Log("Bind: " + payload.GuestToken);

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/bind")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken)
                .WithJsonBody(payload);

            return await request.Send<PlayerToken>();
        }

        public async UniTask<UserBundle> LogoutAsync()
        {
            _accountContainer.ResetAccounts();

            return await LoginAsGuestAsync(); 
        }

        public async UniTask<User> GetUserAsync()
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken)) {
                throw NoctuaException.MissingAccessToken;
            }
            
            var request = new HttpRequest(HttpMethod.Get, $"{_baseUrl}/user/profile")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            return await request.Send<User>();
        }

        public async UniTask UpdateUserAsync(UpdateUserRequest updateUserRequest)
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken)) {
                throw NoctuaException.MissingAccessToken;
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/user/profile")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken)
                .WithJsonBody(updateUserRequest);
            
            _ = await request.Send<object>();
        }

        /// <summary>
        /// Authenticates the user and returns the user bundle.
        /// 
        /// If the user bundle is not detected, it will show the account selection UI.
        /// 
        /// After authentication, it will show a welcome toast for the user.
        /// 
        /// Returns the authenticated user bundle.
        /// </summary>
        /// <returns>A UserBundle object representing the selected account.</returns>
        public async UniTask<UserBundle> AuthenticateAsync()
        {
            if (IsAuthenticated)
            {
                return RecentAccount;
            }

            _accountContainer.Load();
            
            if (_accountContainer.Accounts.Count == 0)
            {
                return await LoginAsGuestAsync();
            }
            
            var firstUser = _accountContainer.Accounts.First();
            
            if (firstUser.Player != null)
            {
                _accountContainer.UpdateRecentAccount(firstUser);
            }
            
            var firstPlayer = firstUser.PlayerAccounts.FirstOrDefault();
            
            if (firstPlayer == null)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "No player account found");
            }
            
            return await ExchangeTokenAsync(firstPlayer.AccessToken);
        }

        public async UniTask SwitchAccountAsync(UserBundle user)
        {
            var targetUser = AccountList.FirstOrDefault(x => x.User.Id == user.User.Id);
            
            if (targetUser == null)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, $"User {user.User.Id} not found in account list");
            }
            
            if (targetUser.Player == null)
            {
                targetUser = await ExchangeTokenAsync(user.PlayerAccounts.First().AccessToken);
            }
            
            _accountContainer.UpdateRecentAccount(targetUser);
        }
        
        public void ResetAccounts()
        {
            _accountContainer.ResetAccounts();
        }

        public async UniTask UpdatePlayerAccountAsync(PlayerAccountData playerAccountData)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/players/sync")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken)
                .WithJsonBody(playerAccountData);
            
            _ = await request.Send<object>();
        }

        public async UniTask DeletePlayerAccountAsync()
        {
            Debug.Log("Delete player account");

            var currentPlayer = RecentAccount.Player;

            var request = new HttpRequest(HttpMethod.Delete, $"{_baseUrl}/players/destroy")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            _ = await request.Send<DeletePlayerAccountResponse>();

            OnAccountDeleted?.Invoke(currentPlayer);
        }

        public async UniTask<string> FileUploader(string filePath)
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken)) {
                throw NoctuaException.MissingAccessToken;
            }

            if (!System.IO.File.Exists(filePath))
            {
                throw new Exception($"File not found at {filePath}");
            }

            byte[] fileData = System.IO.File.ReadAllBytes(filePath);

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/user/profile-image")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken)
                .WithRawBody(fileData);

            string response = await request.SendRaw();
            Newtonsoft.Json.Linq.JObject jObject = Newtonsoft.Json.Linq.JObject.Parse(response);
            string fileUrl = jObject["data"]?["url"]?.ToString();

            return fileUrl;
        }

        public async UniTask<ProfileOptionData> GetProfileOptions()
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken)) {
                throw NoctuaException.MissingAccessToken;
            }

            var request = new HttpRequest(HttpMethod.Get, $"{_baseUrl}/user/profile-options")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            return await request.Send<ProfileOptionData>();
        }

        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
        }
    }
}