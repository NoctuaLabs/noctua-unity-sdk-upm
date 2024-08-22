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

    public class PlayerTokenResponse
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
        public Player[] PlayerAccounts;

        [JsonProperty("is_guest")]
        public bool IsGuest;

        [JsonProperty("is_recent")] // To help identify the recent on UI realm
        public bool IsRecent;
    }

    public class LoginAsGuestRequest
    {
        [JsonProperty("device_id")]
        public string DeviceId;

        [JsonProperty("bundle_id")]
        public string BundleId;
    }

    public class AccountContainer // Used by account container prefs and account detection logic
    {

        [JsonProperty("recent")]
        public UserBundle Recent;

        [JsonProperty("accounts")]
        public UserBundle[] Accounts;
    }
    
    public class NoctuaAuthService
    {
        public Dictionary<string,Player> AllPlayers { get; private set; } = new Dictionary<string, Player>();

        public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

        public Player Player { get; private set; }

        public AccountContainer AccountContainer { get; private set; }

        public event Action<UserBundle> OnAuthenticated;

        private readonly Config _config;

        private string _accessToken;

        internal NoctuaAuthService(Config config)
        {
            _config = config;
        }

        public async UniTask<PlayerTokenResponse> LoginAsGuest()
        {
            if (Application.identifier == "")
            {
                throw new ApplicationException($"App id for platform {Application.platform} is not set");
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_config.BaseUrl}/guests")
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithJsonBody(
                    new LoginAsGuestRequest
                    {
                        DeviceId = SystemInfo.deviceUniqueIdentifier,
                        BundleId = Application.identifier
                    }
                );


            var response = await request.Send<PlayerTokenResponse>();
            var player = response.Player;
            player.User = response.User;
            player.UserId = response.User.Id;
            // Assign to global
            Player = player;
            // Assign to all players
            AllPlayers[response.User.Id + ":"+ response.Game.Id] = player;

            _accessToken = response.AccessToken;
            return response;
        }

        public async UniTask<UserBundle> Authenticate()
        {
            var userBundle = await AccountDetection();
            if (userBundle == null) {
                // Account Selection is needed
                userBundle = await TriggerAccountSelectionUI();
            }

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

        private async UniTask<UserBundle> TriggerAccountSelectionUI()
        {
            return null;
        }

        private async UniTask<UserBundle> AccountDetection()
        {
            // 1. The SDK will try to look up at (shared) account container, to check whether an account exists
            Debug.Log("AccountDetection: Try to read player prefs for account container");
            var accountContainer = ReadPlayerPrefsAccountContainer();

            // Check if the account container is entirely empty
            if (accountContainer == null ||
                (accountContainer != null && accountContainer.Recent == null && (
                    accountContainer.Accounts == null || (
                        accountContainer.Accounts != null &&
                        accountContainer.Accounts.Length == 0)))
            )
            {
                // 2.a - 2.c Iff there is no existing account, try to login as guest
                Debug.Log("AccountDetection: Account not found, try to login as guest");
                return await CreateNewGuest();
            } else {
                // Existing account is not empty

                // 3.a.i Check if there is recent account for this particular game
                Debug.Log("AccountDetection: try to check if there is recent account for this particular game.");
                Debug.Log(Application.identifier);
                Debug.Log(accountContainer?.Recent?.Player?.BundleId);
                if (accountContainer.Recent != null
                && accountContainer.Recent.Player != null
                && accountContainer.Recent.Player.AccessToken != null
                && accountContainer.Recent.PlayerAccounts != null &&
                accountContainer.Recent.PlayerAccounts.Length > 0) {
                    string borrowedAccessToken = null;
                    for (int i = 0; i < accountContainer.Recent.PlayerAccounts.Length; i++) {
                        borrowedAccessToken = accountContainer.Recent.PlayerAccounts[i].AccessToken;
                        if (accountContainer.Recent.PlayerAccounts[i].BundleId == Application.identifier) {
                            // 3.a.i.1. The bundle ID matched
                            Debug.Log("AccountDetection: Found recent account that match with this game., return the user bundle immediately");
                            // Update the recent player, including the player's access token
                            accountContainer.Recent.Player = accountContainer.Recent.PlayerAccounts[i];
                            this.AccountContainer = accountContainer;
                            return accountContainer.Recent;
                        }
                    }
                    // 3.a.i.2. The bundle ID IS NOT matched, try to borrow the existing token
                    Debug.Log("AccountDetection: There is no account match with this game in the recent players, then exchange first");
                    // TODO hit exchange API by using borrowed access token
                    Debug.Log("AccountDetection: borrowed access token: " + borrowedAccessToken);
                    this.AccountContainer = accountContainer;
                    return accountContainer.Recent;
                }

                // 4.a. If there are existing accounts but without any recent account
                Debug.Log("AccountDetection: There is no recent account for this game, try to count the non-guest account first.");
                int selectedAccountIndex = -1;
                int count = 0;
                for (int i = 0; i < accountContainer.Accounts.Length; i++) {
                    if (!accountContainer.Accounts[i].Credential.Provider.Equals("device_id")) {
                        selectedAccountIndex = i;
                        count++;
                    }
                }
                if (count == 1) {
                    // 4.a.i.1 If there is only one non-guest account
                    Debug.Log("AccountDetection: One non-guest account found, return the user bundle");
                    accountContainer.Recent = accountContainer.Accounts[selectedAccountIndex];
                    string borrowedAccessToken = null;
                    for (int i = 0; i < accountContainer.Recent.PlayerAccounts.Length; i++) {
                        borrowedAccessToken = accountContainer.Recent.PlayerAccounts[i].AccessToken;
                        if (accountContainer.Recent.PlayerAccounts[i].BundleId == Application.identifier) {
                            // 4.a.i.1. Bundle ID is matched
                            Debug.Log("AccountDetection: Found recent account that match with this game., return the user bundle immediately");
                            // Update the recent player, including the player's access token
                            accountContainer.Recent.Player = accountContainer.Recent.PlayerAccounts[i];
                            this.AccountContainer = accountContainer;
                            return accountContainer.Recent;
                        }
                    }
                    // 4.a.i.2. Bundle ID is NOT matched, try to borrow token
                    Debug.Log("AccountDetection: There is no account match with this game in the recent players, then exchange first");
                    // TODO hit exchange API by using borrowed access token
                    Debug.Log("AccountDetection: borrowed access token: " + borrowedAccessToken);
                } else if (count > 1) {
                    // 4.a.ii.1 If there is only one non-guest account
                    Debug.Log("AccountDetection: Total account found: " + count);
                    string borrowedAccessToken = null;
                    for (int i = 0; i < accountContainer.Recent.PlayerAccounts.Length; i++) {
                        borrowedAccessToken = accountContainer.Recent.PlayerAccounts[i].AccessToken;
                        if (accountContainer.Recent.PlayerAccounts[i].BundleId == Application.identifier) {
                            // 4.a.ii.1.a If there is only one non-guest account
                            Debug.Log("AccountDetection: Found recent account that match with this game., return the user bundle immediately");
                            // Update the recent player, including the player's access token
                            accountContainer.Recent.Player = accountContainer.Recent.PlayerAccounts[i];
                            this.AccountContainer = accountContainer;
                            return accountContainer.Recent;
                        }
                    }

                    // TODO either create new guest or ask user to choose one of them.
                    // For now returning null to trigger account selection UI
                    return null;
                }
            }

            // Should not reach this point. Fallback to create new guest if it happens
            return await CreateNewGuest();
        }

        private async UniTask<UserBundle> CreateNewGuest()
        {
            Debug.Log("AccountDetection: Account not found, try to login as guest");
            var playerTokenResponse = await LoginAsGuest();
            var userBundle = TransformPlayerTokenResponseToUserBundle(playerTokenResponse);
            var accountContainer = new AccountContainer
            {
                Recent = userBundle,
                Accounts = new UserBundle[] { userBundle }
            };
            SyncPlayerPrefsAccountContainer(accountContainer);
            Debug.Log("AccountDetection: new guest user created, return the user bundle");
            this.AccountContainer = accountContainer;
            return accountContainer.Recent;
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

        private UserBundle TransformPlayerTokenResponseToUserBundle(PlayerTokenResponse playerTokenResponse)
        {
            var userBundle = new UserBundle
            {
                User = playerTokenResponse.User,
                Credential = playerTokenResponse.Credential,
                Player = playerTokenResponse.Player,
                IsGuest = (playerTokenResponse.Credential.Provider == "device_id"),
                IsRecent = true,
                PlayerAccounts = new Player[]
                {
                            playerTokenResponse.Player
                }
            };
            // Merge game related information to player
            userBundle.Player.BundleId = playerTokenResponse.GamePlatform.BundleId;
            userBundle.Player.GameId = playerTokenResponse.Game.Id;
            userBundle.Player.GamePlatformId = playerTokenResponse.GamePlatform.Id;
            userBundle.Player.GamePlatform = playerTokenResponse.GamePlatform.Platform;
            userBundle.Player.GameOS = playerTokenResponse.GamePlatform.OS;
            userBundle.Player.AccessToken = playerTokenResponse.AccessToken;
            return userBundle;
        }

        public void Reset() {
            Debug.Log("Reset");
            PlayerPrefs.SetString("NoctuaAccountContainer", "{}");
            PlayerPrefs.Save();
            _accessToken = null;
            AccountContainer = null;

        }

        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
        }
    }
}
