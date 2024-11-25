using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace Tests.Runtime
{
    public class AccountContainerTest
    {
        private class MockNativeAccountStore : INativeAccountStore
        {
            private readonly List<NativeAccount> _accounts = new();

            public NativeAccount GetAccount(long userId, long gameId)
            {
                return _accounts.First(account => account.PlayerId == userId && account.GameId == gameId);
            }

            public List<NativeAccount> GetAccounts()
            {
                return new List<NativeAccount>(_accounts);
            }

            public void PutAccount(NativeAccount account)
            {
                account.LastUpdated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _accounts.RemoveAll(a => a.PlayerId == account.PlayerId && a.GameId == account.GameId);
                _accounts.Add(account);
            }

            public int DeleteAccount(NativeAccount account)
            {
                _accounts.RemoveAll(a => a.PlayerId == account.PlayerId && a.GameId == account.GameId);

                return 1;
            }
        }

        [UnityTest]
        public IEnumerator EmptyContainer_NoAccountsAndNoRecentAccount()
        {
            var mockStore = new MockNativeAccountStore();
            var accountContainer = new AccountContainer(mockStore, Application.identifier);

            var accounts = accountContainer.Accounts;

            Assert.IsEmpty(accountContainer.Accounts);
            Assert.IsNull(accountContainer.RecentAccount);
            Assert.IsFalse(accounts.Any(a => a.IsRecent));

            yield return null;
        }

        [UnityTest]
        public IEnumerator EmptyContainer_LoadAccountsWithDuplicates_SkippedDuplicates()
        {
            var mockStore = new MockNativeAccountStore();

            var account1 = new NativeAccount
            {
                PlayerId = 1,
                GameId = 1,
                RawData = @"{
                      ""user"": {
                        ""id"": 1,
                        ""nickname"": ""User1""
                      },
                      ""player"": {
                        ""id"": 1,
                        ""username"": ""Player1"",
                        ""bundle_id"": ""example.noctuagames.android.game1""
                      },
                      ""credential"": {
                        ""id"": 1,
                        ""provider"": ""email"",
                        ""display_text"": ""User 1""
                      }
                    }"
            };
            
            var account2 = new NativeAccount
            {
                PlayerId = 2,
                GameId = 1,
                RawData = @"{
                      ""user"": {
                        ""id"": 2,
                        ""nickname"": ""User2""
                      },
                      ""player"": {
                        ""id"": 2,
                        ""username"": ""Player2"",
                        ""bundle_id"": ""example.noctuagames.android.game1""
                      },
                      ""credential"": {
                        ""id"": 2,
                        ""provider"": ""email"",
                        ""display_text"": ""User 1""
                      }
                    }"
            };

            mockStore.PutAccount(account1);
            mockStore.PutAccount(account1);
            mockStore.PutAccount(account2);

            var accountContainer = new AccountContainer(mockStore, "example.noctuagames.android.game1");

            accountContainer.Load();
            
            var accounts = accountContainer.Accounts;

            Assert.AreEqual(2, accounts.Count);
            Assert.AreEqual(1, accounts.Count(a => a.Player.Id == 1));
            Assert.AreEqual(1, accounts.Count(a => a.Player.Id == 2));

            yield return null;
        }

        [UnityTest]
        public IEnumerator EmptyContainer_AddGuestAccount_AccountIsRecent()
        {
            var mockStore = new MockNativeAccountStore();
            var accountContainer = new AccountContainer(mockStore, Application.identifier);

            var playerToken = new PlayerToken
            {
                AccessToken = "accessToken",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1",
                    IsGuest = true
                },
                Player = new Player
                {
                    Id = 1,
                    Username = "Player1",
                    GameId = 1,
                    UserId = 1
                },
                Credential = new Credential
                {
                    Id = 1,
                    Provider = "device_id",
                    DisplayText = "DeviceID1"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            accountContainer.UpdateRecentAccount(playerToken);

            var accounts = accountContainer.Accounts;

            // Account is recent

            Assert.AreEqual(1, accounts.Count);

            Assert.AreEqual(playerToken.User.Id, accountContainer.RecentAccount.User.Id);
            Assert.AreEqual(playerToken.User.Nickname, accountContainer.RecentAccount.User.Nickname);

            Assert.AreEqual(playerToken.AccessToken, accountContainer.RecentAccount.Player.AccessToken);
            Assert.AreEqual(playerToken.Player.Id, accountContainer.RecentAccount.Player.Id);
            Assert.AreEqual(playerToken.Player.Username, accountContainer.RecentAccount.Player.Username);

            Assert.AreEqual(playerToken.Credential.Id, accountContainer.RecentAccount.Credential.Id);
            Assert.AreEqual(playerToken.Credential.DisplayText, accountContainer.RecentAccount.Credential.DisplayText);

            Assert.IsTrue(accounts.First().IsRecent);

            Assert.AreEqual(accountContainer.RecentAccount.Player.User.Id, accountContainer.RecentAccount.User.Id);

            Assert.AreEqual(
                accountContainer.RecentAccount.Player.User.Nickname,
                accountContainer.RecentAccount.User.Nickname
            );

            // Account is saved

            var accountsFromStore = mockStore.GetAccounts();

            Assert.AreEqual(1, accountsFromStore.Count);
            Assert.AreEqual(playerToken.User.Id, accountsFromStore.First().PlayerId);

            yield return null;
        }


        [UnityTest]
        public IEnumerator ContainerWithGuestAccount_AddAccountWithTheSamePlayer_GuestRemoved()
        {
            var mockStore = new MockNativeAccountStore();
            var accountContainer = new AccountContainer(mockStore, Application.identifier);

            var playerToken = new PlayerToken
            {
                AccessToken = "accessToken",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1",
                    IsGuest = true
                },
                Player = new Player
                {
                    Id = 1,
                    Username = "Player1",
                    GameId = 1,
                    UserId = 1
                },
                Credential = new Credential
                {
                    Id = 1,
                    Provider = "device_id",
                    DisplayText = "DeviceID1"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            accountContainer.UpdateRecentAccount(playerToken);

            yield return new WaitForSeconds(0.01f);

            var playerToken2 = new PlayerToken
            {
                AccessToken = "accessToken",
                User = new User
                {
                    Id = 2,
                    Nickname = "User1"
                },
                Player = new Player
                {
                    Id = 1,
                    Username = "Player1",
                    GameId = 1,
                    UserId = 1
                },
                Credential = new Credential
                {
                    Id = 2,
                    Provider = "email",
                    DisplayText = "mbuh@guweg.example"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            accountContainer.UpdateRecentAccount(playerToken2);
            var accounts = accountContainer.Accounts;

            // guest account disappears
            Assert.AreEqual(1, accounts.Count);

            // they are not the same user
            Assert.AreNotEqual(playerToken.User.Id, accountContainer.RecentAccount.User.Id);
            Assert.AreEqual(playerToken2.User.Id, accountContainer.RecentAccount.User.Id);

            // but the same player
            Assert.AreEqual(playerToken.Player.Id, accountContainer.RecentAccount.Player.Id);
            Assert.AreEqual(playerToken2.Player.Id, accountContainer.RecentAccount.Player.Id);

            // recent account is not a guest
            Assert.AreNotEqual(playerToken.Credential.Id, accountContainer.RecentAccount.Credential.Id);

            Assert.AreNotEqual(
                playerToken.Credential.DisplayText,
                accountContainer.RecentAccount.Credential.DisplayText
            );

            // but an  email account
            Assert.AreEqual(playerToken2.Credential.Id, accountContainer.RecentAccount.Credential.Id);
            Assert.AreEqual(playerToken2.Credential.DisplayText, accountContainer.RecentAccount.Credential.DisplayText);

            // recent account is updated
            Assert.AreEqual(playerToken2.AccessToken, accountContainer.RecentAccount.Player.AccessToken);
            Assert.AreEqual(playerToken2.Player.Id, accountContainer.RecentAccount.Player.Id);
            Assert.AreEqual(playerToken2.Player.Username, accountContainer.RecentAccount.Player.Username);

            Assert.IsTrue(accounts.First().IsRecent);

            yield return null;
        }

        [UnityTest]
        public IEnumerator EmptyContainer_AddAccountFromThisGame_AccountIsRecent()
        {
            var mockStore = new MockNativeAccountStore();
            var accountContainer = new AccountContainer(mockStore, Application.identifier);

            var playerToken = new PlayerToken
            {
                AccessToken = "accessToken",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1"
                },
                Player = new Player
                {
                    Id = 1,
                    Username = "Player1",
                    GameId = 1,
                    UserId = 1
                },
                Credential = new Credential
                {
                    Id = 1,
                    Provider = "email",
                    DisplayText = "User 1"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            accountContainer.UpdateRecentAccount(playerToken);

            var accounts = accountContainer.Accounts;

            Assert.AreEqual(1, accounts.Count);

            Assert.AreEqual(playerToken.User.Id, accountContainer.RecentAccount.User.Id);
            Assert.AreEqual(playerToken.User.Nickname, accountContainer.RecentAccount.User.Nickname);

            Assert.AreEqual(playerToken.AccessToken, accountContainer.RecentAccount.Player.AccessToken);
            Assert.AreEqual(playerToken.Player.Id, accountContainer.RecentAccount.Player.Id);
            Assert.AreEqual(playerToken.Player.Username, accountContainer.RecentAccount.Player.Username);

            Assert.AreEqual(playerToken.Credential.Id, accountContainer.RecentAccount.Credential.Id);
            Assert.AreEqual(playerToken.Credential.DisplayText, accountContainer.RecentAccount.Credential.DisplayText);

            Assert.IsTrue(accounts.First().IsRecent);

            Assert.AreEqual(accountContainer.RecentAccount.Player.User.Id, accountContainer.RecentAccount.User.Id);

            Assert.AreEqual(
                accountContainer.RecentAccount.Player.User.Nickname,
                accountContainer.RecentAccount.User.Nickname
            );

            yield return null;
        }

        [UnityTest]
        public IEnumerator EmptyContainer_AddAccountFromOtherGame_AccountIsNotRecentAndPlayerIsNull()
        {
            var mockStore = new MockNativeAccountStore();
            var accountContainer = new AccountContainer(mockStore, Application.identifier);
            var accountContainer2 = new AccountContainer(mockStore, Application.identifier + "2");

            var playerToken = new PlayerToken
            {
                AccessToken = "accessToken",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1"
                },
                Player = new Player
                {
                    Id = 1,
                    Username = "Player1",
                },
                Credential = new Credential
                {
                    Id = 1,
                    Provider = "email",
                    DisplayText = "User 1"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier + "2",
                    OS = "Android"
                }
            };

            accountContainer2.UpdateRecentAccount(playerToken);
            
            accountContainer.Load();

            var accounts = accountContainer.Accounts;

            Assert.AreEqual(1, accounts.Count);

            Assert.IsNull(accountContainer.RecentAccount);
            Assert.AreEqual(playerToken.User.Id, accountContainer.Accounts[0].User.Id);
            Assert.AreEqual(playerToken.User.Nickname, accountContainer.Accounts[0].User.Nickname);

            Assert.AreEqual(playerToken.AccessToken, accountContainer.Accounts[0].PlayerAccounts[0].AccessToken);
            Assert.IsNull(accountContainer.Accounts[0].Player);

            Assert.AreEqual(playerToken.Credential.Id, accountContainer.Accounts[0].Credential.Id);
            Assert.AreEqual(playerToken.Credential.DisplayText, accountContainer.Accounts[0].Credential.DisplayText);

            Assert.IsFalse(accounts.First().IsRecent);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ContainerWithRecentAccount_AddAccountFromThisGame_AccountIsRecent()
        {
            var mockStore = new MockNativeAccountStore();
            var accountContainer = new AccountContainer(mockStore, Application.identifier);

            var playerToken = new PlayerToken
            {
                AccessToken = "accessToken",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1"
                },
                Player = new Player
                {
                    Id = 1,
                    Username = "Player1",
                    GameId = 1,
                    UserId = 1
                },
                Credential = new Credential
                {
                    Id = 1,
                    Provider = "email",
                    DisplayText = "User 1"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            accountContainer.UpdateRecentAccount(playerToken);

            yield return new WaitForSeconds(0.01f);

            var playerToken2 = new PlayerToken
            {
                AccessToken = "accessToken2",
                User = new User
                {
                    Id = 2,
                    Nickname = "User2"
                },
                Player = new Player
                {
                    Id = 2,
                    Username = "Player2",
                    UserId = 2
                },
                Credential = new Credential
                {
                    Id = 2,
                    Provider = "email",
                    DisplayText = "User 2"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            accountContainer.UpdateRecentAccount(playerToken2);

            var accounts = accountContainer.Accounts;

            Assert.AreEqual(2, accounts.Count);

            Assert.AreEqual(playerToken2.User.Id, accountContainer.RecentAccount.User.Id);
            Assert.AreEqual(playerToken2.Player.Id, accountContainer.RecentAccount.Player.Id);
            Assert.AreEqual(playerToken2.AccessToken, accountContainer.RecentAccount.Player.AccessToken);
            Assert.AreEqual(playerToken2.Credential.Id, accountContainer.RecentAccount.Credential.Id);

            Assert.IsTrue(accounts.First().IsRecent);
            Assert.AreEqual(accounts.First().User.Id, accountContainer.RecentAccount.User.Id);

            Assert.AreEqual(accountContainer.RecentAccount.Player.User.Id, accountContainer.RecentAccount.User.Id);

            Assert.AreEqual(
                accountContainer.RecentAccount.Player.User.Nickname,
                accountContainer.RecentAccount.User.Nickname
            );

            Assert.AreEqual(
                accountContainer.Accounts[1].PlayerAccounts[0].User.Id,
                accountContainer.Accounts[1].User.Id
            );

            Assert.AreEqual(
                accountContainer.Accounts[1].PlayerAccounts[0].User.Nickname,
                accountContainer.Accounts[1].User.Nickname
            );

            yield return null;
        }

        [UnityTest]
        public IEnumerator ContainerWithRecentAccount_AddSameUserFromOtherGame_AddedToPlayerAccounts()
        {
            var mockStore = new MockNativeAccountStore();
            var accountContainer = new AccountContainer(mockStore, Application.identifier);
            var accountContainer2 = new AccountContainer(mockStore, Application.identifier + "2");

            var playerToken = new PlayerToken
            {
                AccessToken = "accessToken",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1"
                },
                Player = new Player
                {
                    Id = 1,
                    Username = "Player1",
                },
                Credential = new Credential
                {
                    Id = 1,
                    Provider = "email",
                    DisplayText = "User 1"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            accountContainer.UpdateRecentAccount(playerToken);

            yield return new WaitForSeconds(0.01f);

            var playerToken2 = new PlayerToken
            {
                AccessToken = "accessToken2",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1"
                },
                Player = new Player
                {
                    Id = 2,
                    Username = "Player2",
                },
                Credential = new Credential
                {
                    Id = 1,
                    Provider = "email",
                    DisplayText = "User 1"
                },
                Game = new Game
                {
                    Id = 2,
                    Name = "Game2"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier + "2",
                    OS = "Android"
                }
            };

            accountContainer2.UpdateRecentAccount(playerToken2);

            accountContainer.Load();
            
            var accounts = accountContainer.Accounts;

            Assert.AreEqual(1, accounts.Count);
            Assert.AreEqual(2, accountContainer.RecentAccount.PlayerAccounts.Count);

            Assert.AreEqual(playerToken.User.Id, accountContainer.RecentAccount.User.Id);
            Assert.AreEqual(playerToken.User.Nickname, accountContainer.RecentAccount.User.Nickname);
            Assert.AreEqual(playerToken2.User.Id, accountContainer.RecentAccount.User.Id);
            Assert.AreEqual(playerToken2.User.Nickname, accountContainer.RecentAccount.User.Nickname);

            Assert.AreEqual(playerToken.Player.Id, accountContainer.RecentAccount.PlayerAccounts[0].Id);
            Assert.AreEqual(playerToken.AccessToken, accountContainer.RecentAccount.PlayerAccounts[0].AccessToken);

            Assert.AreEqual(playerToken2.Player.Id, accountContainer.RecentAccount.PlayerAccounts[1].Id);
            Assert.AreEqual(playerToken2.AccessToken, accountContainer.RecentAccount.PlayerAccounts[1].AccessToken);

            Assert.IsTrue(accounts.First().IsRecent);
            Assert.AreEqual(accounts.First().User.Id, accountContainer.RecentAccount.User.Id);

            Assert.AreEqual(
                accountContainer.Accounts[0].PlayerAccounts[0].User.Id,
                accountContainer.Accounts[0].User.Id
            );

            Assert.AreEqual(
                accountContainer.Accounts[0].PlayerAccounts[0].User.Nickname,
                accountContainer.Accounts[0].User.Nickname
            );

            Assert.AreEqual(
                accountContainer.Accounts[0].PlayerAccounts[1].User.Id,
                accountContainer.Accounts[0].User.Id
            );

            Assert.AreEqual(
                accountContainer.Accounts[0].PlayerAccounts[1].User.Nickname,
                accountContainer.Accounts[0].User.Nickname
            );


            yield return null;
        }

        [UnityTest]
        public IEnumerator ContainerWithRecentAccount_AddSameUserFromThisGame_PlayerUpdated()
        {
            var mockStore = new MockNativeAccountStore();
            var accountContainer = new AccountContainer(mockStore, Application.identifier);

            var playerToken = new PlayerToken
            {
                AccessToken = "accessToken",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1"
                },
                Player = new Player
                {
                    Id = 1,
                    Username = "Player1",
                },
                Credential = new Credential
                {
                    Id = 1,
                    Provider = "email",
                    DisplayText = "User 1"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            accountContainer.UpdateRecentAccount(playerToken);

            yield return new WaitForSeconds(0.01f);

            var playerToken2 = new PlayerToken
            {
                AccessToken = "accessToken2",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1"
                },
                Player = new Player
                {
                    Id = 1,
                    Username = "Player2a",
                    ServerId = "ABC",
                },
                Credential = new Credential
                {
                    Id = 1,
                    Provider = "noctua",
                    DisplayText = "User 1"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            accountContainer.UpdateRecentAccount(playerToken2);

            var accounts = accountContainer.Accounts;

            Assert.AreEqual(1, accounts.Count);
            Assert.AreEqual(1, accountContainer.RecentAccount.PlayerAccounts.Count);

            Assert.AreEqual(playerToken2.User.Id, accountContainer.RecentAccount.User.Id);
            Assert.AreEqual(playerToken2.User.Nickname, accountContainer.RecentAccount.User.Nickname);

            Assert.AreEqual(playerToken2.Player.Id, accountContainer.RecentAccount.PlayerAccounts[0].Id);
            Assert.AreEqual(playerToken2.AccessToken, accountContainer.RecentAccount.PlayerAccounts[0].AccessToken);
            Assert.AreEqual(playerToken2.Player.Username, accountContainer.RecentAccount.PlayerAccounts[0].Username);
            Assert.AreEqual(playerToken2.Player.ServerId, accountContainer.RecentAccount.PlayerAccounts[0].ServerId);

            Assert.IsTrue(accounts.First().IsRecent);
            Assert.AreEqual(accounts.First().User.Id, accountContainer.RecentAccount.User.Id);

            Assert.AreEqual(
                accountContainer.Accounts[0].PlayerAccounts[0].User.Id,
                accountContainer.Accounts[0].User.Id
            );

            Assert.AreEqual(
                accountContainer.Accounts[0].PlayerAccounts[0].User.Nickname,
                accountContainer.Accounts[0].User.Nickname
            );

            yield return null;
        }

        [UnityTest]
        public IEnumerator ContainerWithRecentAccount_AddGuestFromThisGame_AccountIsGuestAndRecent()
        {
            var mockStore = new MockNativeAccountStore();
            var accountContainer = new AccountContainer(mockStore, Application.identifier);

            var playerToken = new PlayerToken
            {
                AccessToken = "accessToken",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1"
                },
                Player = new Player
                {
                    Id = 1,
                    Username = "Player1",
                    GameId = 1,
                    UserId = 1
                },
                Credential = new Credential
                {
                    Id = 1,
                    Provider = "email",
                    DisplayText = "User 1"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            accountContainer.UpdateRecentAccount(playerToken);

            yield return new WaitForSeconds(0.01f);

            var playerToken2 = new PlayerToken
            {
                AccessToken = "accessToken2",
                User = new User
                {
                    Id = 2,
                    Nickname = "User2"
                },
                Player = new Player
                {
                    Id = 2,
                    Username = "Player2",
                    UserId = 2
                },
                Credential = new Credential
                {
                    Id = 2,
                    Provider = "device_id",
                    DisplayText = "Guest 2"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            accountContainer.UpdateRecentAccount(playerToken2);

            var accounts = accountContainer.Accounts;

            Assert.AreEqual(2, accounts.Count);

            Assert.AreEqual(playerToken2.Credential.Id, accountContainer.RecentAccount.Credential.Id);
            Assert.AreEqual("device_id", accountContainer.RecentAccount.Credential.Provider);

            Assert.AreEqual(playerToken2.User.Id, accountContainer.RecentAccount.User.Id);
            Assert.AreEqual(playerToken2.Player.Id, accountContainer.RecentAccount.Player.Id);
            Assert.AreEqual(playerToken2.AccessToken, accountContainer.RecentAccount.Player.AccessToken);
            Assert.AreEqual(playerToken2.Credential.Id, accountContainer.RecentAccount.Credential.Id);

            Assert.IsTrue(accounts.First().IsRecent);
            Assert.AreEqual(accounts.First().User.Id, accountContainer.RecentAccount.User.Id);

            Assert.AreEqual(accountContainer.RecentAccount.Player.User.Id, accountContainer.RecentAccount.User.Id);

            Assert.AreEqual(
                accountContainer.RecentAccount.Player.User.Nickname,
                accountContainer.RecentAccount.User.Nickname
            );

            Assert.AreEqual(
                accountContainer.Accounts[1].PlayerAccounts[0].User.Id,
                accountContainer.Accounts[1].User.Id
            );

            Assert.AreEqual(
                accountContainer.Accounts[1].PlayerAccounts[0].User.Nickname,
                accountContainer.Accounts[1].User.Nickname
            );

            yield return null;
        }

        [UnityTest]
        public IEnumerator ContainerWithAccountsFromDifferentGames_ResetAccounts_OnlyDeletesCurrentGameButClearsMemory()
        {
            var mockStore = new MockNativeAccountStore();
            var accountContainer = new AccountContainer(mockStore, Application.identifier);
            var accountContainer2 = new AccountContainer(mockStore, Application.identifier + "2");

            var playerToken1 = new PlayerToken
            {
                AccessToken = "accessToken1",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1"
                },
                Player = new Player
                {
                    Id = 1,
                    Username = "Player1",
                },
                Credential = new Credential
                {
                    Id = 1,
                    Provider = "email",
                    DisplayText = "User 1"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            accountContainer.UpdateRecentAccount(playerToken1);

            yield return new WaitForSeconds(0.01f);

            var playerToken2 = new PlayerToken
            {
                AccessToken = "accessToken2",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1"
                },
                Player = new Player
                {
                    Id = 2,
                    Username = "Player2",
                },
                Credential = new Credential
                {
                    Id = 1,
                    Provider = "email",
                    DisplayText = "User 1"
                },
                Game = new Game
                {
                    Id = 2,
                    Name = "Game2"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier + "2",
                    OS = "Android"
                }
            };

            accountContainer2.UpdateRecentAccount(playerToken2);

            yield return new WaitForSeconds(0.01f);

            var playerToken3 = new PlayerToken
            {
                AccessToken = "accessToken3",
                User = new User
                {
                    Id = 2,
                    Nickname = "User2"
                },
                Player = new Player
                {
                    Id = 3,
                    Username = "Player3",
                },
                Credential = new Credential
                {
                    Id = 2,
                    Provider = "email",
                    DisplayText = "User 2"
                },
                Game = new Game
                {
                    Id = 2,
                    Name = "Game2"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier + "2",
                    OS = "Android"
                }
            };

            accountContainer2.UpdateRecentAccount(playerToken3);

            yield return new WaitForSeconds(0.01f);

            var playerToken4 = new PlayerToken
            {
                AccessToken = "accessToken4",
                User = new User
                {
                    Id = 2,
                    Nickname = "User2"
                },
                Player = new Player
                {
                    Id = 4,
                    Username = "Player4",
                },
                Credential = new Credential
                {
                    Id = 2,
                    Provider = "email",
                    DisplayText = "User 2"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            accountContainer.UpdateRecentAccount(playerToken4);

            accountContainer.ResetAccounts();

            var accounts = accountContainer.Accounts;

            // no accounts in memory

            Assert.AreEqual(0, accounts.Count);

            // only 2 accounts in store

            var accountsFromStore = mockStore.GetAccounts();

            Assert.AreEqual(2, accountsFromStore.Count);
            Assert.Contains(2, accountsFromStore.Select(a => a.PlayerId).ToList());
            Assert.Contains(3, accountsFromStore.Select(a => a.PlayerId).ToList());

            yield return null;
        }

        [UnityTest]
        public IEnumerator ContainerWithGuestAccountsInDifferentGames_AccountIsNotLoaded()
        {
            var mockStore = new MockNativeAccountStore();
            var accountContainer = new AccountContainer(mockStore, Application.identifier);
            var accountContainer2 = new AccountContainer(mockStore, Application.identifier + "2");
            var accountContainer3 = new AccountContainer(mockStore, Application.identifier + "3");
            var accountContainer4 = new AccountContainer(mockStore, Application.identifier + "4");

            var playerToken1 = new PlayerToken
            {
                AccessToken = "accessToken1",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1",
                    IsGuest = true
                },
                Player = new Player
                {
                    Id = 1,
                    Username = "Player1",
                    GameId = 1,
                    UserId = 1
                },
                Credential = new Credential
                {
                    Id = 1,
                    Provider = "device_id",
                    DisplayText = "Guest 1"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier + "2",
                    OS = "Android"
                }
            };

            var playerToken2 = new PlayerToken
            {
                AccessToken = "accessToken2",
                User = new User
                {
                    Id = 2,
                    Nickname = "User2",
                    IsGuest = true
                },
                Player = new Player
                {
                    Id = 2,
                    Username = "Player2",
                    GameId = 2,
                    UserId = 2
                },
                Credential = new Credential
                {
                    Id = 2,
                    Provider = "device_id",
                    DisplayText = "Guest 2"
                },
                Game = new Game
                {
                    Id = 2,
                    Name = "Game2"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier + "3",
                    OS = "Android"
                }
            };

            var playerToken3 = new PlayerToken
            {
                AccessToken = "accessToken3",
                User = new User
                {
                    Id = 3,
                    Nickname = "User3"
                },
                Player = new Player
                {
                    Id = 3,
                    Username = "Player3",
                    GameId = 3,
                    UserId = 3
                },
                Credential = new Credential
                {
                    Id = 3,
                    Provider = "email",
                    DisplayText = "User 3"
                },
                Game = new Game
                {
                    Id = 3,
                    Name = "Game3"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier + "4",
                    OS = "Android"
                }
            };

            accountContainer2.UpdateRecentAccount(playerToken1);
            accountContainer3.UpdateRecentAccount(playerToken2);
            accountContainer4.UpdateRecentAccount(playerToken3);

            accountContainer.Load();

            var accounts = accountContainer.Accounts;

            Assert.AreEqual(1, accounts.Count);
            
            Assert.IsNull(accountContainer.RecentAccount);
            Assert.AreEqual(playerToken3.User.Id, accounts[0].User.Id);
            Assert.AreEqual(playerToken3.User.Nickname, accounts[0].User.Nickname);

            yield return null;
        }
        
        private class FaultyMockNativeAccountStore : INativeAccountStore
        {
            private readonly List<NativeAccount> _accounts = new();

            public int FailedSaveCount { get; private set; }
            public int FailedLoadCount { get; private set; }
            
            private int _numFailures;
            private bool _throwExceptionAtSave;
            private bool _throwExceptionAtLoad;

            public void EnableFailedSave(int numFailures)
            {
                _numFailures = numFailures;
            }
            
            public void EnableThrowExceptionAtSave(bool throwException)
            {
                _throwExceptionAtSave = throwException;
            }
            
            public void EnableThrowExceptionAtLoad(bool throwException)
            {
                _throwExceptionAtLoad = throwException;
            }

            public NativeAccount GetAccount(long userId, long gameId)
            {
                if (!_throwExceptionAtLoad)
                    return _accounts.First(account => account.PlayerId == userId && account.GameId == gameId);

                FailedLoadCount++;
                throw new Exception("Failed to load account");
            }

            public List<NativeAccount> GetAccounts()
            {
                if (!_throwExceptionAtLoad) return new List<NativeAccount>(_accounts);

                FailedLoadCount++;
                throw new Exception("Failed to load account");

            }

            public void PutAccount(NativeAccount account)
            {
                if (_throwExceptionAtSave)
                {
                    FailedSaveCount++;
                    throw new Exception("Failed to save account");
                }
                
                if (_numFailures > 0)
                {
                    FailedSaveCount++;
                    _numFailures--;
                    return;
                }
                
                account.LastUpdated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _accounts.RemoveAll(a => a.PlayerId == account.PlayerId && a.GameId == account.GameId);
                _accounts.Add(account);
            }

            public int DeleteAccount(NativeAccount account)
            {
                _accounts.RemoveAll(a => a.PlayerId == account.PlayerId && a.GameId == account.GameId);

                return 1;
            }
        }
        
        [UnityTest]
        public IEnumerator AccountStoreWithFallback_OnThrowExceptionAtLoad_SwitchToPlayerPrefs()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
                
            var mockStore = new FaultyMockNativeAccountStore();
            var accountContainer = new AccountContainer(mockStore, Application.identifier);

            var playerToken = new PlayerToken
            {
                AccessToken = "accessToken",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1"
                },
                Player = new Player
                {
                    Id = 1,
                    Username = "Player1",
                    GameId = 1,
                    UserId = 1
                },
                Credential = new Credential
                {
                    Id = 1,
                    Provider = "email",
                    DisplayText = "User 1"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            accountContainer.UpdateRecentAccount(playerToken);
            
            mockStore.EnableThrowExceptionAtLoad(true);
            
            accountContainer.Load();

            var accounts = accountContainer.Accounts;
            var useFallback = PlayerPrefs.GetInt("NoctuaAccountContainer.UseFallback");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");
            
            Assert.AreEqual(1, accounts.Count);
            Assert.AreEqual(1, mockStore.FailedLoadCount);
            Assert.AreEqual(1, useFallback);
            
            yield return null;
        }
        
        [UnityTest]
        public IEnumerator AccountStoreWithFallback_OnThrowExceptionAtSave_SwitchToPlayerPrefs()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
            
            var mockStore = new FaultyMockNativeAccountStore();
            var accountContainer = new AccountContainer(mockStore, Application.identifier);
            
            var playerToken = new PlayerToken
            {
                AccessToken = "accessToken",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1"
                },
                Player = new Player
                {
                    Id = 1,
                    Username = "Player1",
                    GameId = 1,
                    UserId = 1
                },
                Credential = new Credential
                {
                    Id = 1,
                    Provider = "email",
                    DisplayText = "User 1"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            mockStore.EnableThrowExceptionAtSave(true);

            accountContainer.UpdateRecentAccount(playerToken);

            var accounts = accountContainer.Accounts;
            
            var useFallback = PlayerPrefs.GetInt("NoctuaAccountContainer.UseFallback");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
            
            Assert.AreEqual(1, accounts.Count);
            Assert.AreEqual(1, mockStore.FailedSaveCount);
            Assert.AreEqual(1, useFallback);
            
            yield return null;
        }
        

        [UnityTest]
        public IEnumerator AccountStoreWithFallback_OnFail1x_DontSwitchToPlayerPrefs()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
                
            var mockStore = new FaultyMockNativeAccountStore();
            var accountContainer = new AccountContainer(mockStore, Application.identifier);

            var playerToken = new PlayerToken
            {
                AccessToken = "accessToken",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1"
                },
                Player = new Player
                {
                    Id = 1,
                    Username = "Player1",
                    GameId = 1,
                    UserId = 1
                },
                Credential = new Credential
                {
                    Id = 1,
                    Provider = "email",
                    DisplayText = "User 1"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            mockStore.EnableFailedSave(1);
            
            accountContainer.UpdateRecentAccount(playerToken);

            var accounts = accountContainer.Accounts;
            var useFallback = PlayerPrefs.GetInt("NoctuaAccountContainer.UseFallback");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");
            
            Assert.AreEqual(1, accounts.Count);
            Assert.AreEqual(1, mockStore.FailedSaveCount);
            Assert.AreEqual(0, useFallback);
            
            yield return null;
        }
        
        [UnityTest]
        public IEnumerator AccountStoreWithFallback_OnFail2x_SwitchToPlayerPrefs()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
                
            var mockStore = new FaultyMockNativeAccountStore();
            var accountContainer = new AccountContainer(mockStore, Application.identifier);

            var playerToken = new PlayerToken
            {
                AccessToken = "accessToken",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1"
                },
                Player = new Player
                {
                    Id = 1,
                    Username = "Player1",
                    GameId = 1,
                    UserId = 1
                },
                Credential = new Credential
                {
                    Id = 1,
                    Provider = "email",
                    DisplayText = "User 1"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            mockStore.EnableFailedSave(2);
            
            accountContainer.UpdateRecentAccount(playerToken);

            var accounts = accountContainer.Accounts;
            var useFallback = PlayerPrefs.GetInt("NoctuaAccountContainer.UseFallback");

            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
            
            Assert.AreEqual(1, accounts.Count);
            Assert.AreEqual(2, mockStore.FailedSaveCount);
            Assert.AreEqual(1, useFallback);
            
            yield return null;
        }
        
        [UnityTest]
        public IEnumerator AccountStoreWithFallback_OnFail2xAfterSuccess_SwitchToPlayerPrefs()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
                
            var mockStore = new FaultyMockNativeAccountStore();
            var accountContainer = new AccountContainer(mockStore, Application.identifier);

            var playerToken = new PlayerToken
            {
                AccessToken = "accessToken",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1"
                },
                Player = new Player
                {
                    Id = 1,
                    Username = "Player1",
                    GameId = 1,
                    UserId = 1
                },
                Credential = new Credential
                {
                    Id = 1,
                    Provider = "email",
                    DisplayText = "User 1"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            var playerToken2 = new PlayerToken
            {
                AccessToken = "accessToken2",
                User = new User
                {
                    Id = 2,
                    Nickname = "User2",
                    IsGuest = true
                },
                Player = new Player
                {
                    Id = 2,
                    Username = "Player2",
                    GameId = 2,
                    UserId = 2
                },
                Credential = new Credential
                {
                    Id = 2,
                    Provider = "device_id",
                    DisplayText = "Guest 2"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            accountContainer.UpdateRecentAccount(playerToken);

            mockStore.EnableFailedSave(2);
            
            accountContainer.UpdateRecentAccount(playerToken2);

            var accounts = accountContainer.Accounts;
            var useFallback = PlayerPrefs.GetInt("NoctuaAccountContainer.UseFallback");

            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
            
            Assert.AreEqual(2, accounts.Count);
            Assert.AreEqual(2, mockStore.FailedSaveCount);
            Assert.AreEqual(1, useFallback);
            
            yield return null;
        }
        
        [UnityTest]
        public IEnumerator AccountStoreWithFallback_UseFallbackAtStartup_NeverWritesToMainStore()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
            PlayerPrefs.SetInt("NoctuaAccountContainer.UseFallback", 1);
                
            var mockStore = new FaultyMockNativeAccountStore();
            var accountContainer = new AccountContainer(mockStore, Application.identifier);

            var playerToken = new PlayerToken
            {
                AccessToken = "accessToken",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1"
                },
                Player = new Player
                {
                    Id = 1,
                    Username = "Player1",
                    GameId = 1,
                    UserId = 1
                },
                Credential = new Credential
                {
                    Id = 1,
                    Provider = "email",
                    DisplayText = "User 1"
                },
                Game = new Game
                {
                    Id = 1,
                    Name = "Game1"
                },
                GamePlatform = new GamePlatform
                {
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            accountContainer.UpdateRecentAccount(playerToken);

            var accounts = accountContainer.Accounts;
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");
            
            Assert.AreEqual(1, accounts.Count);
            Assert.AreEqual(0, mockStore.FailedSaveCount);
            Assert.AreEqual(0, mockStore.GetAccounts().Count);
            
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");

            yield return null;
        }
    }
}
