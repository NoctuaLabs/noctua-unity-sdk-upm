using System;
using System.Collections;
using System.Collections.Generic;
using com.noctuagames.sdk;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Application = UnityEngine.Device.Application;
using SystemInfo = UnityEngine.Device.SystemInfo;
using Newtonsoft.Json;
using System.Collections.Generic;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
// using static UniWebView;
#endif

namespace com.noctuagames.sdk
{
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
    }

    public class Credential {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("provider")]
        public string Provider;
    }

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

    public class Game
    {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("platform_id")]
        public int GamePlatformId;

    }

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

    public class ExchangeTokenRequest {
        // Used for token exchange
        [JsonProperty("next_bundle_id")]
        public string NextBundleId;

        [JsonProperty("init_player")]
        public bool init_player;
    }

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
    }

    public class LoginAsGuestRequest
    {
        [JsonProperty("device_id")]
        public string DeviceId;

        [JsonProperty("bundle_id")]
        public string BundleId;
    }

    public class SocialLoginRedirectUrlResponse
    {
        [JsonProperty("redirect_url")]
        public string RedirectUrl;

    }

    public class SocialLoginCallbackRequest
    {
        [JsonProperty("code")]
        public string Code;

        [JsonProperty("state")]
        public string State;

        [JsonProperty("redirect_url")]
        public string RedirectUrl;
    }

    public class BindRequest
    {
        [JsonProperty("guest_token")]
        public string GuestToken;
    }

    public class AccountContainer // Used by account container prefs and account detection logic
    {

        //[JsonProperty("recent")]
        //public UserBundle Recent;

        [JsonProperty("accounts")]
        public List<UserBundle> Accounts;
    }
    
    public class NoctuaAuthService
    {
        private GameObject NoctuaGameObject = new GameObject();

        // AccountList will be synced data from AccountContainer.Accounts
        public Dictionary<string,UserBundle> AccountList { get; private set; } = new Dictionary<string, UserBundle>();

        public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

        //public Player Player { get; private set; } // Replaced with RecentAccount with UserBundle as type

        public UserBundle RecentAccount { get; private set; }

        public event Action<UserBundle> OnAuthenticated;

        private readonly Config _config;

        private string _accessToken;

        internal NoctuaAuthService(Config config)
        {
            _config = config;
        }

        public async UniTask<PlayerToken> LoginAsGuest()
        {
            Debug.Log("LoginAsGuest: " + Application.identifier + " " + SystemInfo.deviceUniqueIdentifier);
            if (string.IsNullOrEmpty(Application.identifier))
            {
                throw new ApplicationException($"App id for platform {Application.platform} is not set");
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_config.BaseUrl}/auth/guest/login")
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithJsonBody(
                    new LoginAsGuestRequest
                    {
                        DeviceId = SystemInfo.deviceUniqueIdentifier,
                        BundleId = Application.identifier
                    }
                );


            var response = await request.Send<PlayerToken>();
            _accessToken = response.AccessToken;
            return response;
        }

        public async UniTask<PlayerToken> ExchangeToken()
        {
            Debug.Log("LoginAsGuest: " + Application.identifier + " " + SystemInfo.deviceUniqueIdentifier);
            if (string.IsNullOrEmpty(Application.identifier))
            {
                throw new ApplicationException($"App id for platform {Application.platform} is not set");
            }


            ExchangeTokenRequest exchangeToken = new ExchangeTokenRequest();
            exchangeToken.NextBundleId = Application.identifier;
            string json = JsonConvert.SerializeObject(exchangeToken);
            Debug.Log("ExchangeTokenRequest: " + json);

            var request = new HttpRequest(HttpMethod.Post, $"{_config.BaseUrl}/auth/token-exchange")
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithHeader("Authorization", "Bearer " + _accessToken)
                .WithJsonBody(exchangeToken);


            var response = await request.Send<PlayerToken>();
            var player = response.Player;

            _accessToken = response.AccessToken;
            return response;
        }

        public async UniTask<string> GetSocialLoginRedirectURL(string provider)
        {
            Debug.Log("GetSocialLoginURL provider: " + provider);

            var request = new HttpRequest(HttpMethod.Get, $"{_config.BaseUrl}/auth/{provider}/login/redirect")
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithHeader("Authorization", "Bearer " + _accessToken);

            var redirectUrlResponse = await request.Send<SocialLoginRedirectUrlResponse>();

            Debug.Log("GetSocialLoginURL result: " + redirectUrlResponse?.RedirectUrl);

            return redirectUrlResponse?.RedirectUrl;
        }

        public async UniTask<PlayerToken> SocialLoginCallback(string provider, SocialLoginCallbackRequest payload)
        {
            Debug.Log("Social login callback: " + provider);

            var request = new HttpRequest(HttpMethod.Post, $"{_config.BaseUrl}/social-login/{provider}/login/callback")
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithHeader("Authorization", "Bearer " + _accessToken)
                .WithJsonBody(payload);

            var response = await request.Send<PlayerToken>();
            _accessToken = response.AccessToken;
            return response;
        }

        public async UniTask<PlayerToken> Bind(BindRequest payload)
        {
            Debug.Log("Bind: " + payload.GuestToken);

            var request = new HttpRequest(HttpMethod.Post, $"{_config.BaseUrl}/auth/bind")
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithHeader("Authorization", "Bearer " + _accessToken)
                .WithJsonBody(payload);

            var response = await request.Send<PlayerToken>();
            _accessToken = response.AccessToken;
            return response;
        }

        private NoctuaBehaviour GetNoctuaBehaviour()
        {
            NoctuaBehaviour noctuaBehaviour = NoctuaGameObject.GetComponent<NoctuaBehaviour>();
            if (noctuaBehaviour == null) {
                noctuaBehaviour = NoctuaGameObject.AddComponent<NoctuaBehaviour>();
            }
            return noctuaBehaviour;
        }

        public async UniTask<UserBundle> Authenticate()
        {
            GetNoctuaBehaviour(); // So welome box can be ready to be shown

            var userBundle = await AccountDetection();
            if (userBundle == null) {
                // Account Selection is needed
                userBundle = await TriggerAccountSelectionUI();
            }

            Debug.Log("Authenticate: userBundle user ID is " + userBundle?.User?.Id);

            Debug.Log("Authenticate: triggerOnAuthenticated");
            UniTask.Void(
                async () =>
                {
                    OnAuthenticated?.Invoke(userBundle);

                    await UniTask.Yield();
                }
            );

            return userBundle;
        }

        public async UniTask<UserBundle> SocialLogin(string provider)
        {

            Debug.Log("SocialLogin: " + provider);

            var redirectUrl = await GetSocialLoginRedirectURL(provider);

            Debug.Log("SocialLogin: " + provider + " " + redirectUrl);

            #if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            /* Example of using UniWebView plugin
            var webView = NoctuaGameObject.AddComponent<UniWebView>();
            webView.OnPageStarted += OnSocialLoginWebviewStarted;
            //webView.OnPageFinished += OnSocialLoginWebviewFinished;
            webView.Load(redirectUrl);
            */
            #endif

            var userBundle = await AccountDetection();
            return userBundle;
        }

        #if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        /*
        private void OnSocialLoginWebviewStarted(UniWebView webView, string url)
        {
            Debug.Log("URL started to load: " + url);
            // Detect URL change logic here
        }

        private void OnSocialLoginWebviewFinished(UniWebView webView, int statusCode, string url)
        {
            Debug.Log("URL finishhed to load: " + url);
            // Detect URL change logic here
        }
        */
        #endif



        public async UniTask<UserBundle> TriggerAccountSelectionUI()
        {
            var noctuaBehaviour = GetNoctuaBehaviour();
            noctuaBehaviour.SetAccountSelectionDialogVisibility(true);
            return null;
        }

        private async UniTask<UserBundle> AccountDetection()
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
                var response = await LoginAsGuest();
                Debug.Log(response.User.Id);
                Debug.Log(response.Player.Id);
                Debug.Log("AccountDetection: transform token response to user bundle");
                var newGuestAccount = TransformTokenResponseToUserBundle(response);
                Debug.Log("AccountDetection: update recent account");
                newGuestAccount = UpdateRecentAccount(newGuestAccount, accountContainer);
                return newGuestAccount;
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
                        recentAccount = UpdateRecentAccount(recentAccount, accountContainer);
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

                            recentAccount = UpdateRecentAccount(recentAccount, accountContainer);
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

                            recentAccount = UpdateRecentAccount(recentAccount, accountContainer);
                            return recentAccount;
                        }
                    }
                    // 4.a.i.2. Bundle ID is NOT matched, try to borrow token for exchange
                    Debug.Log("AccountDetection: There is no account match with this game in the recent players, then exchange first");
                    Debug.Log("AccountDetection: borrowed access token: " + borrowedAccessToken);
                    _accessToken = borrowedAccessToken;
                    var exchangedAccount = TransformTokenResponseToUserBundle(await ExchangeToken());
                    exchangedAccount = UpdateRecentAccount(exchangedAccount, accountContainer);

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

                            recentAccount = UpdateRecentAccount(recentAccount, accountContainer);
                            return recentAccount;
                        } else {
                            // No player's bundle id found, try to lookup in the user's players array
                            for (int j = 0; j < accountContainer.Accounts[i].PlayerAccounts.Count; j++) {
                                if (accountContainer.Accounts[i].PlayerAccounts[j].BundleId == Application.identifier) {
                                    found = true;
                                    recentAccount = accountContainer.Accounts[i];
                                    recentAccount.Player = accountContainer?.Accounts[i]?.Player;

                                    recentAccount = UpdateRecentAccount(recentAccount, accountContainer);
                                    return recentAccount;
                                }
                            }
                        }
                    }

                    if (!found) {
                        // 4.a.ii.1.1. No player's bundle id found, then create new guest
                        // Either create new guest or ask user to choose one of them.
                        var newGuestAccount = TransformTokenResponseToUserBundle(await LoginAsGuest());
                        newGuestAccount = UpdateRecentAccount(newGuestAccount, accountContainer);
                        return newGuestAccount;
                    }
                }
            }

            // Should not reach this point. Fallback to create new guest if it happens
            var userBundle = TransformTokenResponseToUserBundle(await LoginAsGuest());
            userBundle = UpdateRecentAccount(userBundle, accountContainer);
            return userBundle;
        }

        private UserBundle UpdateRecentAccount(UserBundle userBundle, AccountContainer accountContainer)
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
            bool found = false;
            for (int i = 0; i < accountContainer?.Accounts?.Count; i++) {
                if (accountContainer?.Accounts[i].Player.Id == userBundle.Player.Id
                || accountContainer?.Accounts[i].User.Id == userBundle.User.Id) {
                    Debug.Log("UpdateRecentAccount update account in accounts list");
                    accountContainer.Accounts[i] = userBundle;
                    found = true;
                    break;
                }
            }

            if (!found) {
                Debug.Log("UpdateRecentAccount, user bundle not found in accounts list, add it");
                accountContainer?.Accounts.Add(userBundle);
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

            return userBundle;
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
            var userBundle = new UserBundle
            {
                User = playerTokenResponse.User,
                Credential = playerTokenResponse.Credential,
                Player = playerTokenResponse.Player,
                IsGuest = (playerTokenResponse.Credential.Provider == "device_id"),
                PlayerAccounts = new List<Player>()
                {
                            playerTokenResponse.Player
                }
            };
            Debug.Log("TransformTokenResponseToUserBundle Merge game related information to player");
            userBundle.Player.BundleId = playerTokenResponse.GamePlatform.BundleId;
            userBundle.Player.GameId = playerTokenResponse.Game.Id;
            userBundle.Player.GamePlatformId = playerTokenResponse.GamePlatform.Id;
            userBundle.Player.GamePlatform = playerTokenResponse.GamePlatform.Platform;
            userBundle.Player.GameOS = playerTokenResponse.GamePlatform.OS;
            userBundle.Player.AccessToken = playerTokenResponse.AccessToken;
            return userBundle;
        }

        private PlayerToken TransformUserBundleToPlayerToken(UserBundle userBundle)
        {
            Debug.Log("TransformUserBundleToPlayerToken");
            Debug.Log(JsonConvert.SerializeObject(userBundle));
            var playerToken = new PlayerToken
            {
                AccessToken = userBundle?.Player?.AccessToken,
                User = userBundle?.User,
                Credential = userBundle?.Credential,
                Player = userBundle?.Player,
                //Game = userBundle.Game,
                //GamePlatform = userBundle.GamePlatform
            };
            return playerToken;
        }

        public void Reset() {
            Debug.Log("Reset");
            PlayerPrefs.SetString("NoctuaAccountContainer", "{}");
            PlayerPrefs.Save();
            _accessToken = null;
            this.AccountList = new Dictionary<string, UserBundle>();
            this.RecentAccount = null;
        }


        public void SimulateSingleRecentExistingAccountWithUnmatchedPlayer() {
            string json = "{\"accounts\":[{\"user\":{\"id\":1002,\"nickname\":\"Non-Guest 1002\",\"email_address\":null,\"phone_number\":null},\"credential\":{\"id\":1002,\"provider\":\"google\"},\"player\":{\"access_token\":\"eyJhbGciOiJFUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjE3MjcwMDcyNzUsImdhbWVfYnVuZGxlX2lkIjoiY29tLm5vY3R1YWdhbWVzLmFuZHJvaWQuc2Vjb25kZXhhbXBsZWdhbWUiLCJnYW1lX2lkIjoxMDEsImdhbWVfbmFtZSI6IlNlY29uZCBFeGFtcGxlIEdhbWUiLCJnYW1lX29zIjoiYW5kcm9pZCIsImdhbWVfcGxhdGZvcm0iOiJwbGF5c3RvcmUiLCJnYW1lX3BsYXRmb3JtX2lkIjoxMDAxLCJpYXQiOjE3MjQ0MTUyNzUsImlzcyI6Im5vY3R1YS5nZyIsInBsYXllcl9pZCI6MTAwNywic3ViIjoiMTAwMiJ9.lqkOKAJNJFjSwaqJOpV1KjnydX-3K2N8YdSlnWsrv7jP8G6Oo991se0CYIDLpXLJGkyH8FVHOOT46gnlmkYdPQ\",\"id\":1007,\"role_id\":null,\"server_id\":null,\"username\":null,\"game_id\":101,\"game_name\":null,\"game_platform_id\":1001,\"game_platform\":\"playstore\",\"game_os\":\"android\",\"bundle_id\":\"com.noctuagames.android.secondexamplegame\",\"user\":{\"id\":1002,\"nickname\":\"Guest 1002\",\"email_address\":null,\"phone_number\":null},\"user_id\":1002},\"player_accounts\":[{\"access_token\":\"eyJhbGciOiJFUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjE3MjcwMDcyNzUsImdhbWVfYnVuZGxlX2lkIjoiY29tLm5vY3R1YWdhbWVzLmFuZHJvaWQuc2Vjb25kZXhhbXBsZWdhbWUiLCJnYW1lX2lkIjoxMDEsImdhbWVfbmFtZSI6IlNlY29uZCBFeGFtcGxlIEdhbWUiLCJnYW1lX29zIjoiYW5kcm9pZCIsImdhbWVfcGxhdGZvcm0iOiJwbGF5c3RvcmUiLCJnYW1lX3BsYXRmb3JtX2lkIjoxMDAxLCJpYXQiOjE3MjQ0MTUyNzUsImlzcyI6Im5vY3R1YS5nZyIsInBsYXllcl9pZCI6MTAwNywic3ViIjoiMTAwMiJ9.lqkOKAJNJFjSwaqJOpV1KjnydX-3K2N8YdSlnWsrv7jP8G6Oo991se0CYIDLpXLJGkyH8FVHOOT46gnlmkYdPQ\",\"id\":1007,\"role_id\":null,\"server_id\":null,\"username\":null,\"game_id\":101,\"game_name\":null,\"game_platform_id\":1001,\"game_platform\":\"playstore\",\"game_os\":\"android\",\"bundle_id\":\"com.noctuagames.android.secondexamplegame\",\"user\":{\"id\":1002,\"nickname\":\"Guest 1002\",\"email_address\":null,\"phone_number\":null},\"user_id\":1002}],\"last_used\":\"2024-08-23T12:14:34.9354169Z\",\"is_guest\":true}]}";
            PlayerPrefs.SetString("NoctuaAccountContainer", json);
            PlayerPrefs.Save();
            _accessToken = null;
            this.AccountList = new Dictionary<string, UserBundle>();
            this.RecentAccount = null;
        }

        public void SimulateSingleRecentExistingAccountWithoutMatchedPlayer() {
        }

        public void SimulateMultipleRecentExistingAccountWithMatchedPlayer() {
        }

        public void SimulateMultipleRecentExistingAccountWithoutMatchedPlayer() {
        }

        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
        }
    }
}
