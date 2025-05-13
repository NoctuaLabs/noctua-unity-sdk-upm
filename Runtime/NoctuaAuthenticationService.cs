using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using com.noctuagames.sdk.Events;
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

    [Preserve]
    public enum PaymentType
    {
        unknown,
        appstore,
        playstore,
        noctuastore,
        direct
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
            return (User)MemberwiseClone();
        }
    }

    [Preserve]
    public class Credential
    {
        [JsonProperty("id")]
        public long Id;

        [JsonProperty("provider")]
        public string Provider;

        [JsonProperty("display_text")]
        public string DisplayText;

        public Credential ShallowCopy()
        {
            return (Credential)MemberwiseClone();
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
            return (Player)MemberwiseClone();
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
    public class ExchangeTokenRequest
    {
        // Used for token exchange
        [JsonProperty("next_bundle_id")]
        public string NextBundleId;

        [JsonProperty("init_player")]
        public bool InitPlayer;

        [JsonProperty("next_distribution_platform")]
        public string NextDistributionPlatform;
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
        public DateTimeOffset LastUsed;

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
                    { User: { Nickname: { Length: > 0 } } } => User.Nickname,
                    { Credential: { Provider: "device_id" } } => "Guest " + User?.Id,
                    { Credential: { DisplayText: { Length: > 0 } } } => Credential.DisplayText,
                    { User: { Id: > 0 } } => "User " + User.Id,
                    _ => "Noctua Player"
                };
            }
        }

        public static UserBundle Empty => new()
        {
            User = null,
            Credential = null,
            Player = null,
            PlayerAccounts = new List<Player>(),
            LastUsed = default,
            IsRecent = false
        };
    }

    [Preserve]
    public class LoginAsGuestRequest
    {
        [JsonProperty("device_id")]
        public string DeviceId;

        [JsonProperty("bundle_id")]
        public string BundleId;

        [JsonProperty("distribution_platform")]
        public string DistributionPlatform;
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

        [JsonProperty("no_bind_guest")]
        public bool NoBindGuest;
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

        [JsonProperty("no_bind_guest")]
        public bool NoBindGuest;

        [JsonProperty("reg_extra")]
        public Dictionary<string, string> RegExtra;
    }

    [Preserve]
    public class CredentialVerification
    {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("code")]
        public string Code;

        [JsonProperty("no_bind_guest")]
        public bool NoBindGuest;

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

    // To support VN legal purpose
    [Preserve]
    public class RegisterWithEmailSendPhoneNumberVerification
    {
        [JsonProperty("phone_number")]
        public string PhoneNumber;
    }

    [Preserve]
    public class RegisterWithEmailSendPhoneNumberVerificationResponse
    {
        [JsonProperty("id")]
        public string VerificationId;
    }

    // To support VN legal purpose
    [Preserve]
    public class RegisterWithEmailVerifyPhoneNumberVerification
    {
        [JsonProperty("id")]
        public string VerificationId;

        [JsonProperty("code")]
        public string Code;
    }

    [Preserve]
    public class RegisterWithEmailVerifyPhoneNumberVerificationResponse
    {
    }

    public class NoctuaAuthenticationService
    {
        public IReadOnlyList<UserBundle> AccountList => _accountContainer.Accounts;

        public IReadOnlyList<UserBundle> CurrentGameAccountList => _accountContainer.CurrentGameAccounts;

        public IReadOnlyList<UserBundle> OtherGamesAccountList => _accountContainer.OtherGamesAccounts;

        public bool IsAuthenticated => !string.IsNullOrEmpty(_accountContainer.RecentAccount?.Player?.AccessToken);

        public UserBundle RecentAccount => _accountContainer.RecentAccount;

        public event Action<UserBundle> OnAccountChanged
        {
            add => _accountContainer.OnAccountChanged += value;
            remove => _accountContainer.OnAccountChanged -= value;
        }

        public event Action<Player> OnAccountDeleted;

        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaAuthenticationService));
        private readonly string _clientId;
        private readonly string _baseUrl;
        private readonly string _bundleId;
        private readonly NoctuaLocale _locale;
        private readonly AccountContainer _accountContainer;
        private readonly EventSender _eventSender;
        private OauthRedirectListener _oauthOauthRedirectListener;

        public NoctuaAuthenticationService(
            string baseUrl,
            string clientId,
            INativeAccountStore nativeAccountStore,
            NoctuaLocale locale = null,
            string bundleId = null,
            EventSender eventSender = null
        )
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new ArgumentNullException(nameof(baseUrl));
            }

            if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentNullException(nameof(clientId));
            }

            if (string.IsNullOrEmpty(bundleId))
            {
                throw new ArgumentNullException(nameof(bundleId));
            }

            _clientId = clientId;
            _baseUrl = baseUrl;
            _locale = locale;
            _bundleId = bundleId;
            _eventSender = eventSender;
            _accountContainer = new AccountContainer(nativeAccountStore, bundleId, _locale);
        }

        public async UniTask<UserBundle> LoginAsGuestAsync()
        {
            if (string.IsNullOrEmpty(Application.identifier))
            {
                throw new ApplicationException($"App id for platform {Application.platform} is not set");
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/guest/login")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new LoginAsGuestRequest
                    {
                        DeviceId = SystemInfo.deviceUniqueIdentifier,
                        BundleId = Application.identifier,
                        DistributionPlatform = Utility.GetStoreName()
                    }
                );


            var response = await request.Send<PlayerToken>();

            _accountContainer.UpdateRecentAccount(response);

            SetEventProperties(response);
            SendEvent("account_authenticated");

            return _accountContainer.RecentAccount;
        }

        public async UniTask<UserBundle> ExchangeTokenAsync(string accessToken)
        {
            var exchangeToken = new ExchangeTokenRequest
            {
                NextBundleId = _bundleId,
                InitPlayer = true,
                NextDistributionPlatform = Utility.GetStoreName()
            };

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/token-exchange")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
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
                redirectUri = $"?redirect_uri={WebUtility.UrlEncode(redirectUri)}";
            }

            var request = new HttpRequest(HttpMethod.Get, $"{_baseUrl}/auth/{provider}/login/redirect{redirectUri}")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId);

            var redirectUrlResponse = await request.Send<SocialRedirectUrlResponse>();

            return redirectUrlResponse?.RedirectUrl;
        }
        public async UniTask<UserBundle> SocialLoginAsync(string provider, SocialLoginRequest payload)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/{provider}/login/callback")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(payload);

            if (!string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken) && RecentAccount.IsGuest)
            {
                request.WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);
            }

            var response = await request.Send<PlayerToken>();

            _accountContainer.UpdateRecentAccount(response);

            SetEventProperties(response);
            SendEvent("account_authenticated");
            SendEvent("account_authenticated_by_sso");

            return _accountContainer.RecentAccount;
        }

        // TODO: Add support for phone
        public async UniTask<UserBundle> LoginWithEmailAsync(string email, string password)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/login")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new CredPair
                    {
                        CredKey = email,
                        CredSecret = password
                    }
                )
                .NoVerboseLog();

            var response = await request.Send<PlayerToken>();

            _accountContainer.UpdateRecentAccount(response);


            SetEventProperties(response);
            SendEvent("account_authenticated");
            SendEvent("account_authenticated_by_email");

            return _accountContainer.RecentAccount;
        }

        // TODO: Add support for phone
        public async UniTask<CredentialVerification> RegisterWithEmailAsync(string email, string password, Dictionary<string, string> regExtra)
        {
            _log.Debug("RegisterWithEmailAsync");

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/register")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new CredPair
                    {
                        CredKey = email,
                        CredSecret = password,
                        Provider = "email",
                        RegExtra = regExtra
                    }
                )
                .NoVerboseLog();

            return await request.Send<CredentialVerification>();
        }

        // This API is a subset of email register to support VN legal purpose, not a full registration
        // That is why it has RegisterWithEmail prefix. RegisterWithPhoneNumber will have its own API in the future.
        public async UniTask<RegisterWithEmailSendPhoneNumberVerificationResponse> RegisterWithEmailSendPhoneNumberVerificationAsync(string phoneNumber)
        {
            _log.Debug("RegisterWithEmailSendPhoneNumberVerificationAsync");

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/register-phone-number")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new RegisterWithEmailSendPhoneNumberVerification
                    {
                        PhoneNumber = phoneNumber
                    }
                )
                .NoVerboseLog();

            return await request.Send<RegisterWithEmailSendPhoneNumberVerificationResponse>();
        }

        public async UniTask<RegisterWithEmailVerifyPhoneNumberVerificationResponse> RegisterWithEmailVerifyPhoneNumberAsync(string id, string code)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/verify-phone-number")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new RegisterWithEmailVerifyPhoneNumberVerification
                    {
                        VerificationId = id,
                        Code = code
                    }
                )
                .NoVerboseLog();

            return await request.Send<RegisterWithEmailVerifyPhoneNumberVerificationResponse>();
        }

        public async UniTask<UserBundle> VerifyEmailRegistrationAsync(int id, string code)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/verify-registration")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new CredentialVerification
                    {
                        Id = id,
                        Code = code,
                        NoBindGuest = true
                    }
                );

            var response = await request.Send<PlayerToken>();

            _accountContainer.UpdateRecentAccount(response);

            SetEventProperties(response);
            SendEvent("account_created");
            SendEvent("account_created_by_email");

            return _accountContainer.RecentAccount;
        }

        // TODO: Add support for phone

        public async UniTask<CredentialVerification> RequestResetPasswordAsync(string email)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/reset-password")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new CredPair
                    {
                        CredKey = email
                    }
                );

            var response = await request.Send<CredentialVerification>();

            SendEvent("reset_password_requested");

            return response;
        }

        // TODO: Add support for phone

        public async UniTask<PlayerToken> ConfirmResetPasswordAsync(int id, string code, string newPassword)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/verify-reset-password")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new CredentialVerification
                    {
                        Id = id,
                        Code = code,
                        NewPassword = newPassword,
                    }
                )
                .NoVerboseLog();

            var response = await request.Send<PlayerToken>();

            SendEvent("reset_password_completed");

            return response;
        }

        public async UniTask<Credential> SocialLinkAsync(string provider, SocialLinkRequest payload)
        {
            _log.Debug("SocialLinkAsync");
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw NoctuaException.MissingAccessToken;
            }

            if (RecentAccount.IsGuest)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Guest account cannot link email");
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/{provider}/link/callback")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken)
                .WithJsonBody(payload);

            var response = await request.Send<Credential>();

            SendEvent(
                "credential_added",
                new()
                {
                    { "new_credential_provider", response.Provider },
                    { "new_credential_id", response.Id }
                }
            );

            return response;
        }

        // TODO: Add support for phone

        public async UniTask<CredentialVerification> LinkWithEmailAsync(string email, string password)
        {
            _log.Debug("LinkWithEmailAsync");
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw NoctuaException.MissingAccessToken;
            }

            if (RecentAccount.IsGuest)
            {
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
                )
                .NoVerboseLog();

            return await request.Send<CredentialVerification>();
        }

        public async UniTask<Credential> VerifyEmailLinkingAsync(int id, string code)
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw NoctuaException.MissingAccessToken;
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/verify-link")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken)
                .WithJsonBody(
                    new CredentialVerification
                    {
                        Id = id,
                        Code = code
                    }
                );

            var response = await request.Send<Credential>();

            SendEvent(
                "credential_added",
                new()
                {
                    { "new_credential_provider", response.Provider },
                    { "new_credential_id", response.Id }
                }
            );

            return response;
        }

        public async UniTask<PlayerToken> BeginVerifyEmailRegistrationAsync(int id, string code)
        {
            if (!RecentAccount.IsGuest)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Account is not a guest account");
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/verify-registration")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new CredentialVerification
                    {
                        Id = id,
                        Code = code,
                        NoBindGuest = true
                    }
                );

            request.WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            return await request.Send<PlayerToken>();
        }

        public async UniTask<PlayerToken> BeginVerifyEmailLinkingAsync(int id, string code)
        {
            if (!RecentAccount.IsGuest)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Account is not a guest account");
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/verify-link")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new CredentialVerification
                    {
                        Id = id,
                        Code = code,
                        NoBindGuest = true
                    }
                );

            request.WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            return await request.Send<PlayerToken>();
        }

        public async UniTask<PlayerToken> GetSocialLoginTokenAsync(string provider, SocialLoginRequest payload)
        {
            if (!RecentAccount.IsGuest)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Account is not a guest account");
            }

            payload.NoBindGuest = true;

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/{provider}/login/callback")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(payload);

            request.WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            return await request.Send<PlayerToken>();
        }

        // TODO: Add support for phone
        public async UniTask<PlayerToken> GetEmailLoginTokenAsync(string email, string password)
        {
            if (!RecentAccount.IsGuest)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Account is not a guest account");
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/login")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new CredPair
                    {
                        CredKey = email,
                        CredSecret = password,
                        NoBindGuest = true
                    }
                )
                .NoVerboseLog();

            request.WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            return await request.Send<PlayerToken>();
        }

        public void LoginWithToken(PlayerToken playerToken)
        {
            _accountContainer.UpdateRecentAccount(playerToken);

            SetEventProperties(playerToken);

            SendEvent("account_authenticated");

            var eventName = RecentAccount.Credential?.Provider switch
            {
                "email" => "account_authenticated_by_email",
                "device_id" => "account_authenticated_by_guest",
                _ => "account_authenticated_by_sso"
            };

            SendEvent(eventName);
        }

        public async UniTask<UserBundle> BindGuestAndLoginAsync(PlayerToken targetPlayer)
        {
            if (!RecentAccount.IsGuest)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Account is not a guest account");
            }

            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "origin access token is missing");
            }

            if (string.IsNullOrEmpty(targetPlayer?.AccessToken))
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "target access token is missing");
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/bind")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + targetPlayer?.AccessToken)
                .WithJsonBody(new BindRequest { GuestToken = RecentAccount.Player.AccessToken });

            var response = await request.Send<PlayerToken>();

            _accountContainer.UpdateRecentAccount(response);

            SetEventProperties(response);

            SendEvent("account_bound");

            if (RecentAccount.Credential?.Provider == "email")
            {
                SendEvent("account_bound_by_email");
            }
            else if (RecentAccount.Credential?.Provider != "device_id")
            {
                SendEvent("account_bound_by_sso");
            }

            return _accountContainer.RecentAccount;
        }

        public async UniTask<UserBundle> LogoutAsync()
        {
            return await LoginAsGuestAsync(); // will always back to guest
        }

        public async UniTask<User> GetUserAsync()
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw NoctuaException.MissingAccessToken;
            }

            var request = new HttpRequest(HttpMethod.Get, $"{_baseUrl}/user/profile")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            return await request.Send<User>();
        }

        public async UniTask UpdateUserAsync(UpdateUserRequest updateUserRequest)
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw NoctuaException.MissingAccessToken;
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/user/profile")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken)
                .WithJsonBody(updateUserRequest);

            _ = await request.Send<object>();

            await ExchangeTokenAsync(RecentAccount.Player.AccessToken);
            // Update the user language in player prefs
            // so user does not have to restart twice to load the translation
            _locale?.SetUserPrefsLanguage(updateUserRequest.Language);

            SendEvent(
                "profile_updated",
                new()
                {
                    { "new_country", updateUserRequest.Country },
                    { "new_currency", updateUserRequest.Currency },
                    { "new_language", updateUserRequest.Language },
                }
            );

        }

        /// <summary>
        /// Authenticates the user and returns the user bundle.
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
                SetEventProperties(RecentAccount);
                SendEvent("account_detected");

                return RecentAccount;
            }

            // 3.b and 4.a.i.2: Invalid data will not be loaded

            _accountContainer.Load();

            // 2.a: If there is no account, login as guest

            if (_accountContainer.Accounts.Count == 0)
            {
                await LoginAsGuestAsync();

                SetEventProperties(RecentAccount);
                SendEvent("account_detected");

                return RecentAccount;
            }

            var firstUser = _accountContainer.Accounts.First();

            // Recent accounts are accounts that have played the game, they always match this game
            // 3.a: If there is already a recent account, reuse token

            if (firstUser.Player != null)
            {
                await ExchangeTokenAsync(firstUser.Player.AccessToken);

                SetEventProperties(RecentAccount);
                SendEvent("account_detected");

                return RecentAccount;
            }

            // Non recent accounts are accounts that have not played the game, they always don't match this game

            var firstPlayer = firstUser.PlayerAccounts.FirstOrDefault();

            // This isn't supposed to happen, because a user will at least have a player account

            if (firstPlayer == null)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "No player account found");
            }

            // 4.a.i.2, 4.a.ii.1.b: If there is no recent account, exchange token 

            await ExchangeTokenAsync(firstPlayer.AccessToken);

            SetEventProperties(RecentAccount);
            SendEvent("account_detected");

            return RecentAccount;
        }

        public async UniTask SwitchAccountAsync(UserBundle user)
        {
            var targetUser = AccountList.FirstOrDefault(x => x.User.Id == user.User.Id);

            if (targetUser == null)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, $"User {user.User.Id} not found in account list");
            }

            await ExchangeTokenAsync(user.PlayerAccounts.First().AccessToken);

            SetEventProperties(_accountContainer.RecentAccount);
            SendEvent("account_switched");
        }

        public void ResetAccounts()
        {
            _accountContainer.ResetAccounts();
        }

        public async UniTask UpdatePlayerAccountAsync(PlayerAccountData playerAccountData)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/players/sync")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken)
                .WithJsonBody(playerAccountData);

            _ = await request.Send<object>();

            SendEvent(
                "role_updated",
                new()
                {
                    { "ingame_username", playerAccountData.IngameUsername },
                    { "ingame_server_id", playerAccountData.IngameServerId },
                    { "ingame_role_id", playerAccountData.IngameRoleId }
                }
            );
        }

        public async UniTask DeletePlayerAccountAsync()
        {
            var currentPlayer = RecentAccount.Player;

            var request = new HttpRequest(HttpMethod.Delete, $"{_baseUrl}/players/destroy")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            _ = await request.Send<DeletePlayerAccountResponse>();

            _accountContainer.DeleteRecentAccount();

            SendEvent("account_deleted");

            OnAccountDeleted?.Invoke(currentPlayer);
        }

        public async UniTask<string> FileUploader(string filePath)
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw NoctuaException.MissingAccessToken;
            }

            if (!System.IO.File.Exists(filePath))
            {
                throw new Exception($"File not found at {filePath}");
            }

            byte[] fileData = System.IO.File.ReadAllBytes(filePath);

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/user/profile-image")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken)
                .WithRawBody(fileData);

            string response = await request.SendRaw();
            Newtonsoft.Json.Linq.JObject jObject = Newtonsoft.Json.Linq.JObject.Parse(response);
            string fileUrl = jObject["data"]?["url"]?.ToString();

            return fileUrl;
        }

        public async UniTask<ProfileOptionData> GetProfileOptions()
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw NoctuaException.MissingAccessToken;
            }

            var request = new HttpRequest(HttpMethod.Get, $"{_baseUrl}/user/profile-options")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            return await request.Send<ProfileOptionData>();
        }

        private void SetEventProperties(UserBundle newUser)
        {
            _eventSender?.SetProperties(
                userId: newUser.User?.Id,
                playerId: newUser.Player?.Id,
                credentialId: newUser.Credential?.Id,
                credentialProvider: newUser.Credential?.Provider,
                gameId: newUser.Player?.GameId,
                gamePlatformId: newUser.Player?.GamePlatformId
            );
        }

        private void SetEventProperties(PlayerToken newUser)
        {
            _eventSender?.SetProperties(
                userId: newUser.User?.Id,
                playerId: newUser.Player?.Id,
                credentialId: newUser.Credential?.Id,
                credentialProvider: newUser.Credential?.Provider,
                gameId: newUser.Player?.GameId,
                gamePlatformId: newUser.Player?.GamePlatformId
            );
        }

        private void SendEvent(string eventName, Dictionary<string, IConvertible> data = null)
        {
            _eventSender?.Send(eventName, data ?? new Dictionary<string, IConvertible>());
        }

        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
        }
    }
}
