using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;
using NativeAccount = com.noctuagames.sdk.NativeAccount;

namespace Tests.Runtime
{
    public class AccountContainerTest
    {
        private class MockNativeAccountStore : INativeAccountStore
        {
            public readonly List<NativeAccount> _accounts = new();

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
                if (account.LastUpdated == 0)
                {
                    account.LastUpdated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }

                _accounts.RemoveAll(a => a.PlayerId == account.PlayerId && a.GameId == account.GameId);
                _accounts.Add(account);
            }

            public int DeleteAccount(NativeAccount account)
            {
                _accounts.RemoveAll(a => a.PlayerId == account.PlayerId && a.GameId == account.GameId);

                return 1;
            }
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");

        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");

        }

        [Test]
        public void EmptyContainer_NoAccountsAndNoRecentAccount()
        {
            var mockStore = new MockNativeAccountStore();
            var accountContainer = new AccountContainer(mockStore, Application.identifier);

            var accounts = accountContainer.Accounts;

            Assert.IsEmpty(accountContainer.Accounts);
            Assert.IsNull(accountContainer.RecentAccount);
            Assert.IsFalse(accounts.Any(a => a.IsRecent));

        }

        [Test]
        public void EmptyContainer_LoadAccountsWithDuplicates_SkippedDuplicates()
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

        }

        [Test]
        public void EmptyContainer_AddGuestAccount_AccountIsRecent()
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

        }


        [Test]
        public void ContainerWithGuestAccount_AddAccountWithTheSamePlayer_GuestRemoved()
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

        }

        [Test]
        public void EmptyContainer_AddAccountFromThisGame_AccountIsRecent()
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

        }

        [Test]
        public void EmptyContainer_AddAccountFromOtherGame_AccountIsNotRecentAndPlayerIsNull()
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

        }

        [Test]
        public void ContainerWithRecentAccount_AddAccountFromThisGame_AccountIsRecent()
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

        }

        [Test]
        public void ContainerWithRecentAccount_AddSameUserFromOtherGame_AddedToPlayerAccounts()
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


        }

        [Test]
        public void ContainerWithRecentAccount_AddSameUserFromThisGame_PlayerUpdated()
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

        }

        [Test]
        public void ContainerWithRecentAccount_AddGuestFromThisGame_AccountIsGuestAndRecent()
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

        }

        [Test]
        public void ContainerWithAccountsFromDifferentGames_ResetAccounts_OnlyDeletesCurrentGameButClearsMemory()
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

        }

        [Test]
        public void ContainerWithGuestAccountsInDifferentGames_AccountIsNotLoaded()
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

        }
        
        [Test]
        public void ContainerWithAccountsFromDifferentGames_LoadAccounts_SortedByLastUpdated()
        {
            var now = 1736140000000;
            var mockStore = new MockNativeAccountStore();
            mockStore.PutAccount(
                new NativeAccount
                {
                    PlayerId = 2,
                    GameId = 1,
                    RawData = @"{
                      ""user"": {
                        ""id"": 1,
                        ""nickname"": ""User1""
                      },
                      ""player"": {
                        ""id"": 2,
                        ""username"": ""Player2"",
                        ""bundle_id"": ""example.noctuagames.android.game1"",
                        ""game_id"": 1
                      },
                      ""credential"": {
                        ""id"": 1,
                        ""provider"": ""email"",
                        ""display_text"": ""Credential1""
                      }
                    }",
                    LastUpdated = now
                }
            );
            mockStore.PutAccount(
                new NativeAccount
                {
                    PlayerId = 3,
                    GameId = 1,
                    RawData = @"{
                      ""user"": {
                        ""id"": 2,
                        ""nickname"": ""User2 Latest""
                      },
                      ""player"": {
                        ""id"": 3,
                        ""username"": ""Player3"",
                        ""bundle_id"": ""example.noctuagames.android.game1"",
                        ""game_id"": 1
                       },
                      ""credential"": {
                        ""id"": 12,
                        ""provider"": ""google"",
                        ""display_text"": ""Credential2 Google""
                      }
                    }",
                    LastUpdated = now + 1000
                }
            );
            mockStore.PutAccount(
                new NativeAccount
                {
                    PlayerId = 4,
                    GameId = 2,
                    RawData = @"{
                      ""user"": {
                        ""id"": 2,
                        ""nickname"": ""User2""
                      },
                      ""player"": {
                        ""id"": 4,
                        ""username"": ""Player4"",
                        ""bundle_id"": ""example.noctuagames.android.game2"",
                        ""game_id"": 1
                      },
                      ""credential"": {
                        ""id"": 2,
                        ""provider"": ""email"",
                        ""display_text"": ""Credential2 Email""
                      }
                    }",
                    LastUpdated = now - 1000
                }
            );
            
            var accountContainer = new AccountContainer(mockStore, "example.noctuagames.android.game1");
            
            accountContainer.Load();
            
            Assert.AreEqual(2, accountContainer.Accounts.Count);
            Assert.AreEqual(2, accountContainer.RecentAccount.User.Id);
            Assert.AreEqual("User2 Latest", accountContainer.RecentAccount.User.Nickname);
            Assert.AreEqual("google", accountContainer.RecentAccount.Credential.Provider);
            Assert.AreEqual(3, accountContainer.RecentAccount.Player.Id);

        }
        
        [Test]
        public void ContainerWithAccountsFromTheSameUsers_LoadAccounts_UseLatestUserData()
        {
            var now = 1736140000000;
            var mockStore = new MockNativeAccountStore();
            mockStore.PutAccount(
                new NativeAccount
                {
                    PlayerId = 2,
                    GameId = 1,
                    RawData = @"{
                      ""user"": {
                        ""id"": 1,
                        ""nickname"": ""User1""
                      },
                      ""player"": {
                        ""id"": 2,
                        ""username"": ""Player2"",
                        ""bundle_id"": ""example.noctuagames.android.game1"",
                        ""game_id"": 1
                      },
                      ""credential"": {
                        ""id"": 1,
                        ""provider"": ""email"",
                        ""display_text"": ""User 1""
                      }
                    }",
                    LastUpdated = now
                }
            );
            mockStore.PutAccount(
                new NativeAccount
                {
                    PlayerId = 3,
                    GameId = 1,
                    RawData = @"{
                      ""user"": {
                        ""id"": 2,
                        ""nickname"": ""User2 old""
                      },
                      ""player"": {
                        ""id"": 3,
                        ""username"": ""Player3"",
                        ""bundle_id"": ""example.noctuagames.android.game1"",
                        ""game_id"": 1
                       },
                      ""credential"": {
                        ""id"": 2,
                        ""provider"": ""email"",
                        ""display_text"": ""User 2""
                      }
                    }",
                    LastUpdated = now + 1000
                }
            );
            mockStore.PutAccount(
                new NativeAccount
                {
                    PlayerId = 4,
                    GameId = 2,
                    RawData = @"{
                      ""user"": {
                        ""id"": 2,
                        ""nickname"": ""User2 new""
                      },
                      ""player"": {
                        ""id"": 4,
                        ""username"": ""Player4"",
                        ""bundle_id"": ""example.noctuagames.android.game2"",
                        ""game_id"": 1
                      },
                      ""credential"": {
                        ""id"": 2,
                        ""provider"": ""email"",
                        ""display_text"": ""User 2""
                      }
                    }",
                    LastUpdated = now + 2000
                }
            );
            
            var accountContainer = new AccountContainer(mockStore, "example.noctuagames.android.game2");
            
            accountContainer.Load();
            
            Assert.AreEqual(2, accountContainer.Accounts.Count);
            Assert.AreEqual(2, accountContainer.RecentAccount.User.Id);
            Assert.AreEqual(4, accountContainer.RecentAccount.Player.Id);
            Assert.AreEqual("User2 new", accountContainer.RecentAccount.User.Nickname);

        }
    }

    public class AccountStoreWithFallbackTest
    {
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
        
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");

        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");

        }

        [Test]
        public void AccountStoreWithFallback_OnThrowExceptionAtLoad_SwitchToPlayerPrefs()
        {
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

            Assert.AreEqual(1, accounts.Count);
            Assert.AreEqual(1, mockStore.FailedLoadCount);
            Assert.AreEqual(1, useFallback);
            
        }
        
        [Test]
        public void AccountStoreWithFallback_OnThrowExceptionAtSave_SwitchToPlayerPrefs()
        {
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
            
            Assert.AreEqual(1, accounts.Count);
            Assert.AreEqual(1, mockStore.FailedSaveCount);
            Assert.AreEqual(1, useFallback);
            
        }
        

        [Test]
        public void AccountStoreWithFallback_OnFail1x_DontSwitchToPlayerPrefs()
        {
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
            
            Assert.AreEqual(1, accounts.Count);
            Assert.AreEqual(1, mockStore.FailedSaveCount);
            Assert.AreEqual(0, useFallback);
            
        }
        
        [Test]
        public void AccountStoreWithFallback_OnFail2x_SwitchToPlayerPrefs()
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
            
            Assert.AreEqual(1, accounts.Count);
            Assert.AreEqual(2, mockStore.FailedSaveCount);
            Assert.AreEqual(1, useFallback);
            
        }
        
        [Test]
        public void AccountStoreWithFallback_OnFail2xAfterSuccess_SwitchToPlayerPrefs()
        {
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
                    Nickname = "User2"
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
            
        }
        
        [Test]
        public void EmptyContainer_UpdatingPlayer_DontSwitchToFallback()
        {
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
                    UserId = 1,
                    AccessToken = "accessToken"
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
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            var playerToken2 = new PlayerToken
            {
                AccessToken = "accessToken2",
                User = new User
                {
                    Id = 1,
                    Nickname = "User1",
                },
                Player = new Player
                {
                    Id = 2,
                    Username = "Player2",
                    GameId = 2,
                    UserId = 1,
                    AccessToken = "updatedAccessToken"
                },
                Credential = new Credential
                {
                    Id = 2,
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
                    BundleId = Application.identifier,
                    OS = "Android"
                }
            };

            accountContainer.UpdateRecentAccount(playerToken);
            accountContainer.UpdateRecentAccount(playerToken2);

            var accounts = accountContainer.Accounts;
            var useFallback = PlayerPrefs.GetInt("NoctuaAccountContainer.UseFallback");
            
            Assert.AreEqual(1, accounts.Count);
            Assert.AreEqual(2, accounts.First().PlayerAccounts.Count);
            Assert.AreEqual(0, useFallback);
            
        }
        
        [Test]
        public void AccountStoreWithFallback_UseFallbackAtStartup_NeverWritesToMainStore()
        {
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

            Assert.AreEqual(1, accounts.Count);
            Assert.AreEqual(0, mockStore.FailedSaveCount);
            Assert.AreEqual(0, mockStore.GetAccounts().Count);

        }
    }

    public class AccountContainerConstructorTest
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");

        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");

        }

        [Test]
        public void Constructor_NullNativeAccountStore_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AccountContainer(null, "com.example.game"),
                "Null nativeAccountStore must throw ArgumentNullException"
            );
        }

        [Test]
        public void Constructor_NullBundleId_ThrowsArgumentNullException()
        {
            var mockStore = new MockNativeAccountStore();

            Assert.Throws<ArgumentNullException>(
                () => new AccountContainer(mockStore, null),
                "Null bundleId must throw ArgumentNullException"
            );
        }

        [Test]
        public void Constructor_EmptyBundleId_ThrowsArgumentNullException()
        {
            var mockStore = new MockNativeAccountStore();

            Assert.Throws<ArgumentNullException>(
                () => new AccountContainer(mockStore, ""),
                "Empty bundleId must throw ArgumentNullException"
            );
        }

        private class MockNativeAccountStore : INativeAccountStore
        {
            public NativeAccount GetAccount(long userId, long gameId) => null;
            public List<NativeAccount> GetAccounts() => new List<NativeAccount>();
            public void PutAccount(NativeAccount account) { }
            public int DeleteAccount(NativeAccount account) => 0;
        }
    }

    public class AccountContainerBehaviourTest
    {
        private class MockNativeAccountStore : INativeAccountStore
        {
            public readonly List<NativeAccount> _accounts = new();

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
                if (account.LastUpdated == 0)
                {
                    account.LastUpdated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }

                _accounts.RemoveAll(a => a.PlayerId == account.PlayerId && a.GameId == account.GameId);
                _accounts.Add(account);
            }

            public int DeleteAccount(NativeAccount account)
            {
                _accounts.RemoveAll(a => a.PlayerId == account.PlayerId && a.GameId == account.GameId);
                return 1;
            }
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");

        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerPrefs.DeleteKey("NoctuaAccountContainer");
            PlayerPrefs.DeleteKey("NoctuaAccountContainer.UseFallback");

        }

        private static PlayerToken MakePlayerToken(
            long userId = 1, long playerId = 1, string bundleId = null,
            bool isGuest = false, string provider = "email")
        {
            return new PlayerToken
            {
                AccessToken = "token_" + playerId,
                User = new User { Id = userId, Nickname = "User" + userId, IsGuest = isGuest },
                Player = new Player { Id = playerId, Username = "Player" + playerId, GameId = 1, UserId = userId },
                Credential = new Credential { Id = userId, Provider = provider, DisplayText = "cred_" + userId },
                Game = new Game { Id = 1, Name = "Game1" },
                GamePlatform = new GamePlatform
                {
                    BundleId = bundleId ?? Application.identifier,
                    OS = "Android"
                }
            };
        }

        // ── UpdateRecentAccount(UserBundle) — guard branches ─────────────────

        [Test]
        public void UpdateRecentAccount_NullUserBundle_ReturnsEarlyNoAccountAdded()
        {
            var mockStore = new MockNativeAccountStore();
            var container = new AccountContainer(mockStore, Application.identifier);

            container.UpdateRecentAccount((UserBundle)null);

            Assert.IsEmpty(container.Accounts, "Null user bundle must not add an account");
            Assert.IsNull(container.RecentAccount);

        }

        [Test]
        public void UpdateRecentAccount_WrongBundleId_ReturnsEarlyNoAccountAdded()
        {
            var mockStore = new MockNativeAccountStore();
            var container = new AccountContainer(mockStore, Application.identifier);

            var wrongBundle = new UserBundle
            {
                User = new User { Id = 99, Nickname = "WrongGame" },
                Player = new Player { Id = 99, BundleId = Application.identifier + ".other", UserId = 99 },
                Credential = new Credential { Id = 99, Provider = "email", DisplayText = "wrong@game.com" },
                PlayerAccounts = new List<Player>()
            };

            container.UpdateRecentAccount(wrongBundle);

            Assert.IsEmpty(container.Accounts, "Account with wrong bundle ID must not be added");
            Assert.IsNull(container.RecentAccount);

        }

        // ── UpdateRecentAccount(PlayerToken) — null argument guards ──────────

        [Test]
        public void UpdateRecentAccount_NullPlayerToken_ThrowsArgumentNullException()
        {
            var mockStore = new MockNativeAccountStore();
            var container = new AccountContainer(mockStore, Application.identifier);

            Assert.Throws<ArgumentNullException>(
                () => container.UpdateRecentAccount((PlayerToken)null),
                "Null PlayerToken must throw ArgumentNullException"
            );
        }

        [Test]
        public void UpdateRecentAccount_PlayerTokenNullUser_ThrowsArgumentNullException()
        {
            var mockStore = new MockNativeAccountStore();
            var container = new AccountContainer(mockStore, Application.identifier);

            var token = MakePlayerToken();
            token.User = null;

            Assert.Throws<ArgumentNullException>(
                () => container.UpdateRecentAccount(token),
                "PlayerToken with null User must throw ArgumentNullException"
            );
        }

        [Test]
        public void UpdateRecentAccount_PlayerTokenNullPlayer_ThrowsArgumentNullException()
        {
            var mockStore = new MockNativeAccountStore();
            var container = new AccountContainer(mockStore, Application.identifier);

            var token = MakePlayerToken();
            token.Player = null;

            Assert.Throws<ArgumentNullException>(
                () => container.UpdateRecentAccount(token),
                "PlayerToken with null Player must throw ArgumentNullException"
            );
        }

        [Test]
        public void UpdateRecentAccount_PlayerTokenNullCredential_ThrowsArgumentNullException()
        {
            var mockStore = new MockNativeAccountStore();
            var container = new AccountContainer(mockStore, Application.identifier);

            var token = MakePlayerToken();
            token.Credential = null;

            Assert.Throws<ArgumentNullException>(
                () => container.UpdateRecentAccount(token),
                "PlayerToken with null Credential must throw ArgumentNullException"
            );
        }

        // ── Logout ────────────────────────────────────────────────────────────

        [Test]
        public void Logout_WithRecentAccount_SetsRecentAccountToNull()
        {
            var mockStore = new MockNativeAccountStore();
            var container = new AccountContainer(mockStore, Application.identifier);
            container.UpdateRecentAccount(MakePlayerToken());

            Assert.IsNotNull(container.RecentAccount, "Pre-condition: should have recent account");

            container.Logout();

            Assert.IsNull(container.RecentAccount, "Logout must clear the recent account");

        }

        [Test]
        public void Logout_AccountsRemainsInMemory()
        {
            var mockStore = new MockNativeAccountStore();
            var container = new AccountContainer(mockStore, Application.identifier);
            container.UpdateRecentAccount(MakePlayerToken());

            container.Logout();

            // Accounts list itself is not cleared by Logout — only RecentAccount reference
            Assert.AreEqual(1, container.Accounts.Count,
                "Logout must not clear the in-memory accounts list");

        }

        [Test]
        public void Logout_FiresOnAccountChangedEvent()
        {
            var mockStore = new MockNativeAccountStore();
            var container = new AccountContainer(mockStore, Application.identifier);
            container.UpdateRecentAccount(MakePlayerToken());

            UserBundle receivedBundle = new UserBundle(); // sentinel non-null
            container.OnAccountChanged += bundle => receivedBundle = bundle;

            container.Logout();

            yield return new WaitForSeconds(0.05f);

            Assert.IsNull(receivedBundle,
                "Logout must fire OnAccountChanged with null (the new RecentAccount value)");
        }

        // ── DeleteRecentAccount ───────────────────────────────────────────────

        [Test]
        public void DeleteRecentAccount_WhenRecentAccountIsNull_DoesNotThrow()
        {
            var mockStore = new MockNativeAccountStore();
            var container = new AccountContainer(mockStore, Application.identifier);

            Assert.IsNull(container.RecentAccount, "Pre-condition: no recent account");

            Assert.DoesNotThrow(() => container.DeleteRecentAccount(),
                "DeleteRecentAccount with no recent account must not throw");

        }

        [Test]
        public void DeleteRecentAccount_WithRecentAccount_RemovesItAndReloads()
        {
            var mockStore = new MockNativeAccountStore();
            var container = new AccountContainer(mockStore, Application.identifier);
            container.UpdateRecentAccount(MakePlayerToken(userId: 1, playerId: 1));

            yield return new WaitForSeconds(0.01f);

            container.UpdateRecentAccount(MakePlayerToken(userId: 2, playerId: 2));

            Assert.AreEqual(2, container.Accounts.Count, "Pre-condition: two accounts");

            container.DeleteRecentAccount();

            // recent was the last one added (userId=2). After delete, it should no longer be there.
            Assert.IsTrue(
                container.Accounts.All(a => a.User.Id != 2),
                "Deleted player must not appear in accounts after DeleteRecentAccount"
            );

        }

        // ── CurrentGameAccounts / OtherGamesAccounts properties ──────────────

        [Test]
        public void CurrentGameAccounts_ReturnsOnlyAccountsForCurrentGame()
        {
            var mockStore = new MockNativeAccountStore();
            var container1 = new AccountContainer(mockStore, Application.identifier);
            var container2 = new AccountContainer(mockStore, Application.identifier + ".other");

            container1.UpdateRecentAccount(MakePlayerToken(userId: 1, playerId: 1));
            container2.UpdateRecentAccount(MakePlayerToken(
                userId: 2, playerId: 2, bundleId: Application.identifier + ".other"));

            container1.Load();

            var currentGame = container1.CurrentGameAccounts;
            var otherGames = container1.OtherGamesAccounts;

            Assert.AreEqual(1, currentGame.Count,
                "CurrentGameAccounts must include only accounts matching current bundle ID");
            Assert.AreEqual(1, otherGames.Count,
                "OtherGamesAccounts must include only accounts NOT matching current bundle ID");
            Assert.AreEqual(1, currentGame[0].User.Id);

        }

        // ── OnAccountChanged event wiring ─────────────────────────────────────

        [Test]
        public void UpdateRecentAccount_FiresOnAccountChangedWhenUserChanges()
        {
            var mockStore = new MockNativeAccountStore();
            var container = new AccountContainer(mockStore, Application.identifier);

            var changedAccounts = new List<UserBundle>();
            container.OnAccountChanged += bundle => changedAccounts.Add(bundle);

            container.UpdateRecentAccount(MakePlayerToken(userId: 1, playerId: 1));

            yield return new WaitForSeconds(0.05f);

            Assert.AreEqual(1, changedAccounts.Count,
                "OnAccountChanged must fire once when a new account is set");
            Assert.AreEqual(1L, changedAccounts[0].User.Id);
        }

        [Test]
        public void UpdateRecentAccount_SameUserAndPlayer_DoesNotFireOnAccountChanged()
        {
            var mockStore = new MockNativeAccountStore();
            var container = new AccountContainer(mockStore, Application.identifier);

            container.UpdateRecentAccount(MakePlayerToken(userId: 1, playerId: 1));

            yield return new WaitForSeconds(0.05f);

            int changeCount = 0;
            container.OnAccountChanged += _ => changeCount++;

            // Update same user+player — RecentAccount setter should early-return without firing
            container.UpdateRecentAccount(MakePlayerToken(userId: 1, playerId: 1));

            yield return new WaitForSeconds(0.05f);

            Assert.AreEqual(0, changeCount,
                "OnAccountChanged must NOT fire when the same user+player is set again");
        }

        // ── FromNativeAccounts — malformed / incomplete raw data skipped ──────

        [Test]
        public void Load_MalformedJsonInRawData_SkipsEntryAndLoadsRest()
        {
            var mockStore = new MockNativeAccountStore();

            mockStore._accounts.Add(new NativeAccount
            {
                PlayerId = 1,
                GameId = 1,
                RawData = "NOT VALID JSON {{{",
                LastUpdated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            mockStore._accounts.Add(new NativeAccount
            {
                PlayerId = 2,
                GameId = 1,
                RawData = @"{
                    ""user"":       { ""id"": 2, ""nickname"": ""GoodUser"" },
                    ""player"":     { ""id"": 2, ""username"": ""GoodPlayer"",
                                      ""bundle_id"": """ + Application.identifier + @""", ""game_id"": 1 },
                    ""credential"": { ""id"": 2, ""provider"": ""email"", ""display_text"": ""good@example.com"" }
                }",
                LastUpdated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            var container = new AccountContainer(mockStore, Application.identifier);
            container.Load();

            Assert.AreEqual(1, container.Accounts.Count,
                "Malformed JSON entry must be skipped; valid entry must still load");
            Assert.AreEqual(2L, container.Accounts[0].User.Id);

        }

        [Test]
        public void Load_NullUserInParsedData_SkipsEntry()
        {
            var mockStore = new MockNativeAccountStore();

            // JSON that parses fine but results in null user field
            mockStore._accounts.Add(new NativeAccount
            {
                PlayerId = 1,
                GameId = 1,
                RawData = @"{
                    ""player"":     { ""id"": 1, ""username"": ""P1"",
                                      ""bundle_id"": """ + Application.identifier + @""" },
                    ""credential"": { ""id"": 1, ""provider"": ""email"", ""display_text"": ""x"" }
                }",
                LastUpdated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            var container = new AccountContainer(mockStore, Application.identifier);
            container.Load();

            Assert.IsEmpty(container.Accounts,
                "Entry with null user in parsed data must be skipped");

        }
    }
}
