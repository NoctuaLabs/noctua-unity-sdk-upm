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
    public class AccountContainer // Used by account container prefs and account detection logic
    {
        public IReadOnlyList<UserBundle> Accounts => _accounts;
        
        public IReadOnlyList<UserBundle> CurrentGameAccounts => 
            _accounts
                .Where(x => x.PlayerAccounts.Any(y => y.BundleId == _bundleId))
                .ToList();

        public IReadOnlyList<UserBundle> OtherGamesAccounts => 
            _accounts
                .Where(x => x.PlayerAccounts.All(y => y.BundleId != _bundleId))
                .ToList();

        public UserBundle RecentAccount { get; private set; }

        private readonly List<UserBundle> _accounts = new();
        private readonly INativeAccountStore _nativeAccountStore;
        private readonly ILogger _log = new NoctuaLogger(typeof(AccountContainer));
        private readonly string _bundleId;

        public AccountContainer(INativeAccountStore nativeAccountStore, string bundleId)
        {
            if (string.IsNullOrEmpty(bundleId))
            {
                throw new ArgumentNullException(nameof(bundleId));
            }

            _nativeAccountStore = nativeAccountStore ?? throw new ArgumentNullException(nameof(nativeAccountStore));
            _bundleId = bundleId;
        }

        public event Action<UserBundle> OnAccountChanged;

        public void Load()
        {
            var nativeAccounts = _nativeAccountStore.GetAccounts();
            var accounts = FromNativeAccounts(nativeAccounts);
            
            _log.Debug($"loaded {accounts.Count} accounts from native account store");

            _accounts.Clear();
            _accounts.AddRange(accounts);

            RecentAccount = Accounts.FirstOrDefault(x => x.IsRecent);
        }

        public void UpdateRecentAccount(PlayerToken playerToken)
        {
            var userBundle = TransformTokenResponseToUserBundle(playerToken);

            UpdateRecentAccount(userBundle);
        }

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

            var oldUser = RecentAccount;

            Save(newUser);
            Load(); // reload so that we don't have to worry about caching

            if (_accounts.Count == 0)
            {
                _log.Warning("failed to save account, retrying");

                Save(newUser);
                Load(); // reload so that we don't have to worry about caching

                if (_accounts.Count == 0)
                {
                    _log.Error("failed to save account, please check native account store configuration");

                    throw new NoctuaException(NoctuaErrorCode.AccountStorage, "failed to save account");
                }
            }

            if (oldUser?.User?.Id != RecentAccount?.User?.Id || oldUser?.Player?.Id != RecentAccount?.Player.Id)
            {
                UniTask.Void(async () => OnAccountChanged?.Invoke(RecentAccount));
            }
        }

        private void Save(UserBundle userBundle)
        {
            _log.Debug($"saving account {userBundle.User.Id}-{userBundle.Player.Id}-{userBundle.Player.BundleId}");

            _nativeAccountStore.PutAccount(ToNativeAccount(userBundle));
        }

        private UserBundle TransformTokenResponseToUserBundle(PlayerToken playerTokenResponse)
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

        public void Logout()
        {
            RecentAccount = null;
        }

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
                    _nativeAccountStore.DeleteAccount(
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

                if (!userBundleMap.TryGetValue(data.User.Id, out var userBundle))
                {
                    userBundle = new UserBundle
                    {
                        PlayerAccounts = new List<Player>()
                    };

                    userBundleMap[data.User.Id] = userBundle;
                }

                if (userBundle.User == null || DateTimeOffset.FromUnixTimeMilliseconds(account.LastUpdated) > userBundle.LastUsed)
                {
                    userBundle.LastUsed = DateTimeOffset.FromUnixTimeMilliseconds(account.LastUpdated).DateTime;
                    userBundle.User = data.User;
                }

                userBundle.PlayerAccounts.Add(data.Player);
                userBundle.Credential = data.Credential;
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

        [Preserve]
        private class NativeAccountData
        {
            [JsonProperty("user")] public User User;
            [JsonProperty("player")] public Player Player;
            [JsonProperty("credential")] public Credential Credential;
        }

        public void DeleteRecentAccount()
        {
            if (RecentAccount == null)
            {
                return;
            }

            _nativeAccountStore.DeleteAccount(
                new NativeAccount
                {
                    PlayerId = RecentAccount.Player.Id,
                    GameId = RecentAccount.Player.GameId
                }
            );

            Load();
            
            RecentAccount = null;
            
            UniTask.Void(async () => OnAccountChanged?.Invoke(RecentAccount));
        }
    }
}