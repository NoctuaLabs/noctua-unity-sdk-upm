﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using UnityEngine.Scripting;
using ArgumentNullException = System.ArgumentNullException;

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
        public int Id;

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
    }

    [Preserve]
    public class Credential {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("provider")]
        public string Provider;
        
        [JsonProperty("display_text")]
        public string DisplayText;
    }

    [Preserve]
    public class Player
    {
        [JsonProperty("access_token")]
        public string AccessToken;

        [JsonProperty("id")]
        public int Id;

        [JsonProperty("role_id")]
        public string RoleId;

        [JsonProperty("server_id")]
        public string ServerId;

        [JsonProperty("username")] // in-game
        public string Username;

        [JsonProperty("game_id")]
        public int GameId;

        [JsonProperty("game_name")]
        public string GameName;

        [JsonProperty("game_platform_id")]
        public int GamePlatformId;

        [JsonProperty("game_platform")]
        public string GamePlatform;

        [JsonProperty("game_os")]
        public string GameOS;

        [JsonProperty("bundle_id")]
        public string BundleId;

        [JsonProperty("user")]
        public User User;

        [JsonProperty("user_id")]
        public int UserId;
    }

    [Preserve]
    public class Game
    {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("platform_id")]
        public int GamePlatformId;

    }

    [Preserve]
    public class GamePlatform
    {
        [JsonProperty("id")]
        public int Id;

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

        [JsonProperty("is_guest")]
        public bool IsGuest;

        [JsonProperty("is_recent")]
        public bool IsRecent;

        public string DisplayName
        {
            get
            {
                return this switch
                {
                    {Player: {Username: {Length: > 0}}} => Player.Username,
                    {User: {Nickname: {Length: > 0}}} => User.Nickname,
                    {Credential: {Provider: "device_id"}} => "Guest " + User?.Id,
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
    public class AccountContainer // Used by account container prefs and account detection logic
    {
        [JsonProperty("accounts")]
        public List<UserBundle> Accounts;
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
        public Dictionary<string,UserBundle> AccountList { get; private set; } = new();

        public bool IsAuthenticated => !string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken);

        public UserBundle RecentAccount { get; private set; }

        public event Action<UserBundle> OnAccountChanged;
        public event Action<Player> OnAccountDeleted;

        private readonly string _clientId;
        private readonly string _baseUrl;
        private OauthRedirectListener _oauthOauthRedirectListener;

        internal NoctuaAuthenticationService(string baseUrl, string clientId)
        {
            _clientId = clientId;
            _baseUrl = baseUrl;
        }

        public async UniTask<UserBundle> LoginAsGuestAsync()
        {
            Debug.Log("LoginAsGuest: " + Application.identifier + " " + SystemInfo.deviceUniqueIdentifier);
            if (string.IsNullOrEmpty(Application.identifier))
            {
                throw new ApplicationException($"App id for platform {Application.platform} is not set");
            }

            Debug.Log("ClientId: " + _clientId);
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

            var accountContainer = ReadPlayerPrefsAccountContainer();
            var recentAccount = TransformTokenResponseToUserBundle(response);
            UpdateRecentAccount(recentAccount, accountContainer);

            return recentAccount;
        }

        public async UniTask<UserBundle> ExchangeTokenAsync(string accessToken)
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken)) {
                throw NoctuaException.MissingAccessToken;
            }
            
            Debug.Log("LoginAsGuest: " + Application.identifier + " " + SystemInfo.deviceUniqueIdentifier);
            if (string.IsNullOrEmpty(Application.identifier))
            {
                throw new ApplicationException($"App id for platform {Application.platform} is not set");
            }


            ExchangeTokenRequest exchangeToken = new ExchangeTokenRequest();
            exchangeToken.NextBundleId = Application.identifier;
            string json = JsonConvert.SerializeObject(exchangeToken);
            Debug.Log("ExchangeTokenRequest: " + json);

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/token-exchange")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + accessToken)
                .WithJsonBody(exchangeToken);


            var response = await request.Send<PlayerToken>();

            var accountContainer = ReadPlayerPrefsAccountContainer();
            var recentAccount = TransformTokenResponseToUserBundle(response);
            UpdateRecentAccount(recentAccount, accountContainer);

            return recentAccount;
        }

        public async UniTask<string> GetSocialAuthRedirectURLAsync(string provider, string redirectUri = "")
        {
            Debug.Log("GetSocialLoginURL provider: " + provider);

            if (!string.IsNullOrEmpty(redirectUri))
            {
                redirectUri = $"?redirect_uri={HttpUtility.UrlEncode(redirectUri)}";
            }

            var request = new HttpRequest(HttpMethod.Get, $"{_baseUrl}/auth/{provider}/login/redirect{redirectUri}")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier);

            var redirectUrlResponse = await request.Send<SocialRedirectUrlResponse>();

            Debug.Log("GetSocialLoginURL result: " + redirectUrlResponse?.RedirectUrl);

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
            
            var accountContainer = ReadPlayerPrefsAccountContainer();
            var recentAccount = TransformTokenResponseToUserBundle(response);
            UpdateRecentAccount(recentAccount, accountContainer);

            return recentAccount;
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

            var accountContainer = ReadPlayerPrefsAccountContainer();
            var recentAccount = TransformTokenResponseToUserBundle(response);
            UpdateRecentAccount(recentAccount, accountContainer);

            return recentAccount;
        }
        
        // TODO: Add support for phone
        public async UniTask<CredentialVerification> RegisterWithEmailAsync(string email, string password)
        {
            
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/register")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithJsonBody(
                    new CredPair
                    {
                        CredKey = email,
                        CredSecret = password,
                        Provider = "email"
                    }
                );

            if (!string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken) && RecentAccount.IsGuest)
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

            var accountContainer = ReadPlayerPrefsAccountContainer();
            var recentAccount = TransformTokenResponseToUserBundle(response);
            UpdateRecentAccount(recentAccount, accountContainer);

            return recentAccount;
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
            
            var accountContainer = ReadPlayerPrefsAccountContainer();
            var recentAccount = TransformTokenResponseToUserBundle(response);
            UpdateRecentAccount(recentAccount, accountContainer);
            
            return recentAccount;
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
            
            Debug.Log("Bind: " + payload.GuestToken);

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/bind")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken)
                .WithJsonBody(payload);

            return await request.Send<PlayerToken>();
        }

        public async UniTask<UserBundle> LogoutAsync()
        {
            Debug.Log("Reset");
            PlayerPrefs.SetString("NoctuaAccountContainer", "{}");
            PlayerPrefs.Save();
            AccountList = new Dictionary<string, UserBundle>();
            RecentAccount = null;

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
            // 1. The SDK will try to look up at (shared) account container, to check whether an account exists
            Debug.Log("AccountDetection: Try to read player prefs for account container");
            var accountContainer = ReadPlayerPrefsAccountContainer();

            // Check if the account container is entirely empty
            if (accountContainer == null ||
                (accountContainer != null && (
                    accountContainer.Accounts == null || (
                        accountContainer.Accounts != null &&
                        accountContainer.Accounts.Count == 0)))
            )
            {
                // 2.a - 2.c If there is no existing account, try to login as guest
                Debug.Log("AccountDetection: Account not found, try to login as guest");
                var response = await LoginAsGuestAsync();
                
                return response;
            } else {
                // Sort by last used to get the recent account
                accountContainer.Accounts.Sort((x, y) => y.LastUsed.CompareTo(x.LastUsed));
                var recentAccount = accountContainer.Accounts[0];

                // Existing account is not empty

                // 3.a.i Check if there is recent account for this particular game
                Debug.Log("AccountDetection: try to check if there is recent account for this particular game.");
                if (recentAccount != null && recentAccount.Player != null
                    && recentAccount.Player.BundleId == Application.identifier) {
                        Debug.Log("AccountDetection: player matched, use this user bundle.");
                
                        UpdateRecentAccount(recentAccount, accountContainer);
                        
                        return recentAccount;
                } else {
                    // Recent player is not matched with this game,
                    // Try to lookup in the players array of this user
                    Debug.Log("AccountDetection: no matched player, try to lookup in the players array of this user");
                    for (int i = 0; i < recentAccount.PlayerAccounts.Count; i++) {
                        if (recentAccount.PlayerAccounts[i].BundleId == Application.identifier) {
                            // 3.a.i.1. The bundle ID matched
                            Debug.Log("AccountDetection: Found recent account that match with this game., return the user bundle immediately");
                            // Update the recent player, including the player's access token
                            recentAccount.Player = recentAccount.PlayerAccounts[i];
                            UpdateRecentAccount(recentAccount, accountContainer);
                            
                            return recentAccount;
                        }
                    }
                }
                // 3.a.i.2. The bundle ID IS NOT matched, try to borrow the existing token
                // This logic is linear with 4.a, so let's continue to 4.a

                // 4.a. If there are existing accounts but without any matched player
                Debug.Log("AccountDetection: There is no recent account for this game, try to count the non-guest account first.");
                int selectedAccountIndex = -1;
                int count = 0;
                for (int i = 0; i < accountContainer.Accounts.Count; i++) {
                    if (!accountContainer.Accounts[i].Credential.Provider.Equals("device_id")) {
                        selectedAccountIndex = i;
                        count++;
                    }
                }
                
                if (count == 1) {
                    // 4.a.i.1 If there is only one non-guest account
                    Debug.Log("AccountDetection: One non-guest account found, return the user bundle");
                    recentAccount = accountContainer.Accounts[selectedAccountIndex];
                    string borrowedAccessToken = null;
                    for (int i = 0; i < recentAccount.PlayerAccounts.Count; i++) {
                        borrowedAccessToken = recentAccount.PlayerAccounts[i].AccessToken;
                        if (recentAccount.PlayerAccounts[i].BundleId == Application.identifier) {
                            // 4.a.i.1. Bundle ID is matched
                            Debug.Log("AccountDetection: Found recent account that match with this game., return the user bundle immediately");
                            // Update the recent player, including the player's access token
                            recentAccount.Player = recentAccount.PlayerAccounts[i];
                            UpdateRecentAccount(recentAccount, accountContainer);
                            
                            return recentAccount;
                        }
                    }
                    // 4.a.i.2. Bundle ID is NOT matched, try to borrow token for exchange
                    Debug.Log("AccountDetection: There is no account match with this game in the recent players, then exchange first");
                    Debug.Log("AccountDetection: borrowed access token: " + borrowedAccessToken);

                    var exchangedAccount = await ExchangeTokenAsync(borrowedAccessToken);

                    return exchangedAccount;
                } else if (count > 1) {
                    // 4.a.ii.1 If there are more than one non-guest account
                    bool found = false;
                    
                    for (int i = 0; i < accountContainer?.Accounts?.Count; i++) {
                        if (accountContainer.Accounts[i]?.Player?.BundleId == Application.identifier) {
                            // Matched user player's bundle id found
                            found = true;
                            recentAccount = accountContainer.Accounts[i];
                            recentAccount.Player = accountContainer?.Accounts[i]?.Player;
                            UpdateRecentAccount(recentAccount, accountContainer);
                        
                            return recentAccount;
                        } else {
                            // No player's bundle id found, try to lookup in the user's players array
                            for (int j = 0; j < accountContainer.Accounts[i].PlayerAccounts.Count; j++) {
                                if (accountContainer.Accounts[i].PlayerAccounts[j].BundleId == Application.identifier) {
                                    found = true;
                                    recentAccount = accountContainer.Accounts[i];
                                    recentAccount.Player = accountContainer?.Accounts[i]?.Player;
                                    UpdateRecentAccount(recentAccount, accountContainer);
                                    
                                    return recentAccount;
                                }
                            }
                        }
                    }

                    if (!found) {
                        // 4.a.ii.1.1. No player's bundle id found, then create new guest
                        // Either create new guest or ask user to choose one of them.
                        return await LoginAsGuestAsync();
                    }
                }
            }

            // Should not reach this point. Fallback to create new guest if it happens
            return await LoginAsGuestAsync();
        }

        private void UpdateRecentAccount(UserBundle userBundle, AccountContainer accountContainer)
        {
            Debug.Log("UpdateRecentAccount");
            if (accountContainer == null) {
                Debug.Log("UpdateRecentAccount, accountContainer is null");
                accountContainer = new AccountContainer(){
                    Accounts = new List<UserBundle>()
                };
            } else if (accountContainer.Accounts == null) {
                Debug.Log("UpdateRecentAccount, accountContainer is not null, but the Accounts is null, initiate it");
                accountContainer.Accounts = new List<UserBundle>(){userBundle};
            }
            if (userBundle == null) {
                Debug.Log("UpdateRecentAccount, userBundle is null");
            } else {
                Debug.Log("UpdateRecentAccount, userBundle is not null");
            }
            // Update the LastUsed to mark the recent account
            userBundle.LastUsed = DateTime.UtcNow;
            userBundle.IsRecent = true;
            userBundle.IsGuest = userBundle?.Credential?.Provider == "device_id";

            bool found = false;
            for (int i = 0; i < accountContainer?.Accounts?.Count; i++) {

                accountContainer.Accounts[i].IsGuest = accountContainer.Accounts[i]?.Credential?.Provider == "device_id";

                if (accountContainer?.Accounts[i].Player.Id == userBundle.Player.Id
                || accountContainer?.Accounts[i].User.Id == userBundle.User.Id) {
                    Debug.Log("UpdateRecentAccount update account in accounts list");
                    accountContainer.Accounts[i] = userBundle;
                    found = true;
                    break;
                } else {
                    accountContainer.Accounts[i].IsRecent = false;
                }
            }

            if (!found) {
                Debug.Log("UpdateRecentAccount, user bundle not found in accounts list, add it");
                accountContainer?.Accounts?.Add(userBundle);
                Debug.Log("UpdateRecentAccount, added");
            }

            // Sort it again by LastUsed
            Debug.Log("UpdateRecentAccount, sort to recent");
            accountContainer.Accounts.Sort((x, y) => y.LastUsed.CompareTo(x.LastUsed));

            // Write-sync to PlayerPrefs
            Debug.Log("UpdateRecentAccount, sync against player prefs");
            SyncPlayerPrefsAccountContainer(accountContainer);

            // Assign to class
            Debug.Log("UpdateRecentAccount, assign to class's recent account");
            this.RecentAccount = userBundle;
            // Convert array of object to dictionary so it's easier to use in UI rendering
            Debug.Log("UpdateRecentAccount, assign to class's account list");
            this.AccountList = new Dictionary<string, UserBundle>();
            for (int i = 0; i < accountContainer.Accounts.Count; i++) {
                this.AccountList[accountContainer.Accounts[i].User.Id + ":"+ accountContainer.Accounts[i].Player.Id] = accountContainer.Accounts[i];
            }
            
            UniTask.Void(async () => OnAccountChanged?.Invoke(userBundle));
        }

        private void SyncPlayerPrefsAccountContainer(AccountContainer accountContainer)
        {
                // Convert accountContainer to JSON string
                var json = JsonConvert.SerializeObject(accountContainer);
                if (string.IsNullOrEmpty(json)) {
                    json = "{}";
                }
                // Save to Player Prefs
                Debug.Log("AccountDetection: Sync player prefs account container");
                PlayerPrefs.SetString(Constants.PlayerPrefsKeyAccountContainer, json);
                PlayerPrefs.Save();
        }

        private AccountContainer ReadPlayerPrefsAccountContainer()
        {
            string json = PlayerPrefs.GetString(Constants.PlayerPrefsKeyAccountContainer, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                json = "{}";
            }
            Debug.Log("AccountDetection: Read player prefs account container JSON: " + json);
            try {
                var accountContainer = JsonConvert.DeserializeObject<AccountContainer>(json);
                return accountContainer;
            } catch (Exception e) {
                Debug.Log("Exception: Failed to parse the account container JSON: " + e);
                return null;
            }
        }

        private UserBundle TransformTokenResponseToUserBundle(PlayerToken playerTokenResponse)
        {
            Debug.Log("TransformTokenResponseToUserBundle");
            
            if (playerTokenResponse == null)
            {
                throw new ArgumentNullException(nameof(playerTokenResponse));
            }
            
            if (playerTokenResponse.User == null)
            {
                throw new ArgumentNullException(nameof(playerTokenResponse.User));
            }
            
            if (playerTokenResponse.Player == null)
            {
                throw new ArgumentNullException(nameof(playerTokenResponse.Player));
            }
            
            if (playerTokenResponse.Credential == null)
            {
                throw new ArgumentNullException(nameof(playerTokenResponse.Credential));
            }

            var userBundle = new UserBundle
            {
                User = playerTokenResponse.User,
                Credential = playerTokenResponse.Credential,
                Player = playerTokenResponse.Player,
                IsGuest = playerTokenResponse.Credential.Provider == "device_id",
                PlayerAccounts = new List<Player> { playerTokenResponse.Player }
            };
            
            Debug.Log("TransformTokenResponseToUserBundle Merge game related information to player");

            userBundle.Player.BundleId = playerTokenResponse.GamePlatform?.BundleId;
            userBundle.Player.GameId = playerTokenResponse.Game?.Id ?? 0;
            userBundle.Player.GamePlatformId = playerTokenResponse.GamePlatform?.Id ?? 0;
            userBundle.Player.GamePlatform = playerTokenResponse.GamePlatform?.Platform;
            userBundle.Player.GameOS = playerTokenResponse.GamePlatform?.OS;
            userBundle.Player.AccessToken = playerTokenResponse.AccessToken;
            
            return userBundle;
        }

        public void ResetAccounts() {
            Debug.Log("Reset");
            PlayerPrefs.SetString("NoctuaAccountContainer", "{}");
            PlayerPrefs.Save();
            AccountList = new Dictionary<string, UserBundle>();
            RecentAccount = null;
            
            UniTask.Void(async () => OnAccountChanged?.Invoke(null));
        }

        public void SwitchAccount(UserBundle user)
        {
            var existingUser = AccountList.FirstOrDefault(x => x.Value.User.Id == user.User.Id && x.Value.Player.Id == user.Player.Id).Value;
            
            RecentAccount = existingUser ?? throw new ArgumentException($"User {user.User.Id} not found in account list");
            
            UpdateRecentAccount(RecentAccount, ReadPlayerPrefsAccountContainer());
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