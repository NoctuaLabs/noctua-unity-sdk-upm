using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using com.noctuagames.sdk.Events;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Manages local account storage, loading, and persistence. Tracks all known user accounts
    /// and determines the most recent account for the current game.
    /// </summary>
    public class AccountContainer
    {
        /// <summary>
        /// Gets all known user accounts (current game and other games combined).
        /// </summary>
        public IReadOnlyList<UserBundle> Accounts => _accounts;

        /// <summary>
        /// Gets accounts that have player data matching the current game's bundle ID.
        /// </summary>
        public IReadOnlyList<UserBundle> CurrentGameAccounts =>
            _accounts
                .Where(x => x.PlayerAccounts.Any(y => y.BundleId == _bundleId))
                .ToList();

        /// <summary>
        /// Gets accounts that only have player data for other games (not the current game).
        /// </summary>
        public IReadOnlyList<UserBundle> OtherGamesAccounts =>
            _accounts
                .Where(x => x.PlayerAccounts.All(y => y.BundleId != _bundleId))
                .ToList();

        /// <summary>
        /// Gets or sets the most recently used account for the current game. Setting fires <see cref="OnAccountChanged"/> when the user or player changes.
        /// </summary>
        public UserBundle RecentAccount
        {
            get => _recentAccount;

            private set
            {
                var oldUser = _recentAccount;
                _recentAccount = value;
                
                if (oldUser?.User?.Id == _recentAccount?.User?.Id && oldUser?.Player?.Id == _recentAccount?.Player?.Id)
                {
                    return;
                }
                
                _log.Debug($"account changed to '{_recentAccount?.User?.Id}-{_recentAccount?.Player?.Id}-{_recentAccount?.Credential?.DisplayText}'");

                UniTask.Void(async () => OnAccountChanged?.Invoke(RecentAccount));
            }
        }

        private readonly List<UserBundle> _accounts = new();
        private readonly AccountStoreWithFallback _accountStore;
        private readonly ILogger _log = new NoctuaLogger(typeof(AccountContainer));
        private readonly string _bundleId;
        private UserBundle _recentAccount;
        private NoctuaLocale _locale;

        /// <summary>
        /// Initializes a new account container with native storage and fallback support.
        /// </summary>
        /// <param name="nativeAccountStore">Native platform account storage implementation.</param>
        /// <param name="bundleId">The current game's bundle identifier.</param>
        /// <param name="locale">Optional locale provider for language preferences.</param>
        public AccountContainer(INativeAccountStore nativeAccountStore, string bundleId, NoctuaLocale locale = null)
        {
            if (string.IsNullOrEmpty(bundleId))
            {
                throw new ArgumentNullException(nameof(bundleId));
            }

            var mainAccountStore = nativeAccountStore ?? throw new ArgumentNullException(nameof(nativeAccountStore));
            var fallbackAccountStore = new DefaultNativePlugin();
            _accountStore = new AccountStoreWithFallback(mainAccountStore, fallbackAccountStore);
            _bundleId = bundleId;
            _locale = locale;
        }

        /// <summary>
        /// Fires when the active account changes (user ID or player ID differs from previous).
        /// </summary>
        public event Action<UserBundle> OnAccountChanged;

        /// <summary>
        /// Loads all accounts from native storage, clears the in-memory list, and sets the recent account.
        /// </summary>
        public void Load()
        {
            var nativeAccounts = _accountStore.GetAccounts();
            var accounts = FromNativeAccounts(nativeAccounts);
            
            _log.Debug($"loaded {accounts.Count} accounts from native account store");

            _accounts.Clear();
            _accounts.AddRange(accounts);
            
            RecentAccount = Accounts.FirstOrDefault(x => x.IsRecent);
        }

        /// <summary>
        /// Updates the recent account from a player token response by transforming it to a user bundle.
        /// </summary>
        /// <param name="playerToken">The player token response from authentication.</param>
        public void UpdateRecentAccount(PlayerToken playerToken)
        {
            var userBundle = AccountContainer.TransformTokenResponseToUserBundle(playerToken);

            UpdateRecentAccount(userBundle);
        }

        /// <summary>
        /// Persists the given user bundle as the recent account, with automatic retry and fallback to PlayerPrefs.
        /// </summary>
        /// <param name="newUser">The user bundle to save as the recent account.</param>
        /// <exception cref="NoctuaException">Thrown when the account cannot be saved after all retry attempts.</exception>
        public void UpdateRecentAccount(UserBundle newUser)
        {
            if (newUser is null)
            {
                _log.Warning("attempted to save null account");
                
                return;
            }
            
            if (newUser.Player?.BundleId != _bundleId)
            {
                _log.Warning("attempted to save account for another game");

                return;
            }

            _locale?.SetUserPrefsLanguage(newUser.User.Language);

            try
            {
                Save(newUser);
                Load();
                
                bool IsNewUser(UserBundle x) => x.Player?.Id == newUser.Player.Id && x.Player?.AccessToken == newUser.Player?.AccessToken;

                if (_accounts.Any(x => IsNewUser(x))) return;

                _log.Warning("failed to save account, retrying");

                Save(newUser);
                Load();

                if (_accounts.Any(x => IsNewUser(x))) return;
                
                _log.Warning("failed to save account, fallback to PlayerPrefs");
                _accountStore.EnableFallback();
            }
            catch (Exception e)
            {
                _log.Warning($"saving account throws exception, fallback to PlayerPrefs: {e.Message}");
                _accountStore.EnableFallback();
            }
            
            Save(newUser);
            Load();
            
            if (_accounts.Count != 0) return;

            _log.Error("failed to save account, fallback failed");
                
            throw new NoctuaException(NoctuaErrorCode.AccountStorage, "failed to save account");
        }

        private void Save(UserBundle userBundle)
        {
            _log.Debug($"saving account {userBundle.User.Id}-{userBundle.Player.Id}-{userBundle.Player.BundleId}");

            _accountStore.PutAccount(ToNativeAccount(userBundle));
        }

        private static UserBundle TransformTokenResponseToUserBundle(PlayerToken playerTokenResponse)
        {
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
                PlayerAccounts = new List<Player> { playerTokenResponse.Player }
            };

            userBundle.Player.BundleId = playerTokenResponse.GamePlatform?.BundleId;
            userBundle.Player.GameId = playerTokenResponse.Game?.Id                 ?? 0;
            userBundle.Player.GamePlatformId = playerTokenResponse.GamePlatform?.Id ?? 0;
            userBundle.Player.GamePlatform = playerTokenResponse.GamePlatform?.Platform;
            userBundle.Player.GameOS = playerTokenResponse.GamePlatform?.OS;
            userBundle.Player.AccessToken = playerTokenResponse.AccessToken;
            userBundle.Player.User = userBundle.User;
            userBundle.Player.UserId = userBundle.User.Id;

            return userBundle;
        }

        /// <summary>
        /// Clears the recent account reference (sets it to null).
        /// </summary>
        public void Logout()
        {
            RecentAccount = null;
        }

        /// <summary>
        /// Deletes all stored accounts for the current game from native storage and clears in-memory state.
        /// </summary>
        public void ResetAccounts()
        {
            if (string.IsNullOrEmpty(_bundleId))
            {
                _log.Warning("_bundleId is null or empty");
            }
            else
            {
                // best attempt to reset accounts for the current game
                var accounts = Accounts
                    .SelectMany(x => x.PlayerAccounts)
                    .Where(x => x.BundleId == _bundleId);

                foreach (var account in accounts)
                {
                    _accountStore.DeleteAccount(
                        new NativeAccount
                        {
                            PlayerId = account.Id,
                            GameId = account.GameId
                        }
                    );
                }
            }

            _accounts.Clear();
            RecentAccount = null;
        }

        private NativeAccount ToNativeAccount(UserBundle userBundle)
        {
            if (userBundle.User == null || userBundle.Player == null || userBundle.Credential == null)
            {
                throw new ArgumentNullException(nameof(userBundle));
            }
            
            var data = new NativeAccountData
            {
                User = userBundle.User.ShallowCopy(),
                Player = userBundle.Player.ShallowCopy(),
                Credential = userBundle.Credential.ShallowCopy()
            };
            
            // clean up unused data
            data.User.Credentials = null;
            data.Player.User = null;

            var rawData = JsonConvert.SerializeObject(data);

            return new NativeAccount
            {
                PlayerId = userBundle.Player.Id,
                GameId = userBundle.Player.GameId,
                RawData = rawData
            };
        }

        private List<UserBundle> FromNativeAccounts(List<NativeAccount> accounts)
        {
            var userBundleMap = new Dictionary<long, UserBundle>();
            var playerIds = new HashSet<long>();

            foreach (var account in accounts)
            {
                NativeAccountData data;
                
                try
                {
                    data = JsonConvert.DeserializeObject<NativeAccountData>(account.RawData);
                }
                catch (Exception e)
                {
                    _log.Error($"failed to parse account data: {e.Message}");
                    
                    continue;
                }

                _log.Debug($"loaded account {data.Credential?.DisplayText}-{data.User?.Id}-{data.Player?.Id}-{data.Player?.BundleId}");

                if (data?.User == null || data.Player == null || data.Credential == null)
                {
                    continue;
                }

                var isGuest = data.User.IsGuest || data.Credential.Provider == "device_id";
                var isCurrentGame = data.Player.BundleId == _bundleId;

                if (isGuest && !isCurrentGame)
                {
                    continue;
                }
                
                if (!playerIds.Add(data.Player.Id)) // skip duplicate players
                {
                    continue;
                }
                
                var accountLastUsed = DateTimeOffset.FromUnixTimeMilliseconds(account.LastUpdated);

                if (!userBundleMap.TryGetValue(data.User.Id, out var userBundle))
                {
                    userBundle = new UserBundle
                    {
                        PlayerAccounts = new List<Player>(),
                        User = data.User,
                        Credential = data.Credential,
                        LastUsed = accountLastUsed,
                    };

                    userBundleMap[data.User.Id] = userBundle;
                }
                
                if (accountLastUsed > userBundle.LastUsed)
                {
                    userBundle.User = data.User;
                    userBundle.Credential = data.Credential;
                    userBundle.LastUsed = accountLastUsed;
                }

                userBundle.PlayerAccounts.Add(data.Player);
            }

            // repopulate self referential data
            foreach (var userBundle in userBundleMap.Values)
            {
                foreach (var player in userBundle.PlayerAccounts)
                {
                    if (player.BundleId == _bundleId)
                    {
                        userBundle.Player = player;
                    }

                    player.User = userBundle.User;
                }
            }

            var userBundleList = userBundleMap.Values.ToList();

            var currentGameUsers = userBundleList.Where(x => x.Player?.BundleId == _bundleId).ToList();
            currentGameUsers.Sort((a, b) => b.LastUsed.CompareTo(a.LastUsed));

            if (currentGameUsers.Count > 0)
            {
                currentGameUsers[0].IsRecent = true;
            }

            var otherGameUsers = userBundleList.Except(currentGameUsers).ToList();
            otherGameUsers.Sort((a, b) => b.LastUsed.CompareTo(a.LastUsed));

            return currentGameUsers.Concat(otherGameUsers).ToList();
        }

        /// <summary>
        /// Deletes the most recent account from native storage and reloads the account list.
        /// </summary>
        public void DeleteRecentAccount()
        {
            if (RecentAccount == null)
            {
                return;
            }

            _accountStore.DeleteAccount(
                new NativeAccount
                {
                    PlayerId = RecentAccount.Player.Id,
                    GameId = RecentAccount.Player.GameId
                }
            );

            Load();
        }

        [Preserve]
        private class NativeAccountData
        {
            [JsonProperty("user")] public User User;
            [JsonProperty("player")] public Player Player;
            [JsonProperty("credential")] public Credential Credential;
        }

        private class AccountStoreWithFallback
        {
            private readonly ILogger _log = new NoctuaLogger();
            private readonly INativeAccountStore _mainStore;
            private readonly INativeAccountStore _fallbackStore;
            private bool _useFallback;
            
            public void EnableFallback()
            {
                _useFallback = true;
                PlayerPrefs.SetInt("NoctuaAccountContainer.UseFallback", 1);
                PlayerPrefs.Save();
            }
            
            public AccountStoreWithFallback(INativeAccountStore mainStore, INativeAccountStore fallbackStore)
            {
                _mainStore = mainStore ?? throw new ArgumentNullException(nameof(mainStore));
                _fallbackStore = fallbackStore ?? throw new ArgumentNullException(nameof(fallbackStore));
                
                _useFallback = PlayerPrefs.GetInt("NoctuaAccountContainer.UseFallback", 0) == 1; 
            }
            
            public NativeAccount GetAccount(long userId, long gameId)
            {
                try
                {
                    return _useFallback ? _fallbackStore.GetAccount(userId, gameId) : _mainStore.GetAccount(userId, gameId);
                }
                catch (Exception e)
                {
                    EnableFallback();

                    return _fallbackStore.GetAccount(userId, gameId);
                }
            }
            
            public List<NativeAccount> GetAccounts()
            {
                try
                {
                    return _useFallback ? _fallbackStore.GetAccounts() : _mainStore.GetAccounts();
                }
                catch (Exception e)
                {
                    _log.Error($"failed to get accounts: {e.Message}, fallback enabled");
                    
                    EnableFallback();

                    return _fallbackStore.GetAccounts();
                }
            }
            
            public void PutAccount(NativeAccount account)
            {
                if (!_useFallback)
                {
                    _mainStore.PutAccount(account);
                }

                _fallbackStore.PutAccount(account);
            }
            
            public int DeleteAccount(NativeAccount account)
            {
                return _useFallback ? _fallbackStore.DeleteAccount(account) : _mainStore.DeleteAccount(account);
            }
        }
    }
}
