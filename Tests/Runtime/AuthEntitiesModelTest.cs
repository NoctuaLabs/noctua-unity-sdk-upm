using System;
using System.Collections.Generic;
using com.noctuagames.sdk;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// JSON round-trip and property tests for all DTOs in AuthEntities.cs.
    /// Covers: Constants, PaymentType, User, Credential, Player, Game,
    /// GamePlatform, PlayerToken, UserBundle, LoginAsGuestRequest,
    /// SocialRedirectUrlResponse, DeletePlayerAccountResponse, CloudSaveMetadata,
    /// CloudSaveListResponse, SocialLoginRequest, SocialLinkRequest, BindRequest,
    /// CredPair, CredentialVerification, PlayerAccountData, UpdateUserRequest,
    /// ProfileOptionData, GeneralProfileData, RegisterWithEmail* types.
    /// </summary>
    [TestFixture]
    public class AuthEntitiesModelTest
    {
        // ─── Constants ────────────────────────────────────────────────────────

        [Test]
        public void Constants_PlayerPrefsKey_HasExpectedValue()
        {
            Assert.AreEqual("NoctuaAccountContainer", Constants.PlayerPrefsKeyAccountContainer);
        }

        [Test]
        public void Constants_CustomerServiceBaseUrl_StartsWithHttps()
        {
            StringAssert.StartsWith("https://noctua.gg", Constants.CustomerServiceBaseUrl);
        }

        // ─── PaymentType ──────────────────────────────────────────────────────

        [Test]
        public void PaymentType_EnumValues_AllPresent()
        {
            // Ordinal stability is important for serialization compatibility
            Assert.AreEqual(0, (int)PaymentType.unknown);
            Assert.AreEqual(1, (int)PaymentType.appstore);
            Assert.AreEqual(2, (int)PaymentType.playstore);
            Assert.AreEqual(3, (int)PaymentType.noctuastore);
            Assert.AreEqual(4, (int)PaymentType.noctuastore_redeem);
            Assert.AreEqual(5, (int)PaymentType.direct);
            Assert.AreEqual(6, (int)PaymentType.editor);
        }

        // ─── User ─────────────────────────────────────────────────────────────

        [Test]
        public void User_JsonRoundTrip_PreservesAllFields()
        {
            var original = new User
            {
                Id = 42L,
                Nickname = "TestUser",
                EmailAddress = "test@example.com",
                PhoneNumbers = "+621234567890",
                PictureUrl = "https://cdn.noctua.gg/pics/42.png",
                IsGuest = false,
                DateOfBirth = "1990-01-15",
                Gender = "male",
                Language = "id",
                Country = "ID",
                Currency = "IDR",
                PaymentType = PaymentType.playstore,
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<User>(json);

            Assert.AreEqual(42L,              restored.Id);
            Assert.AreEqual("TestUser",       restored.Nickname);
            Assert.AreEqual("test@example.com", restored.EmailAddress);
            Assert.AreEqual("+621234567890",  restored.PhoneNumbers);
            Assert.AreEqual("https://cdn.noctua.gg/pics/42.png", restored.PictureUrl);
            Assert.IsFalse(restored.IsGuest);
            Assert.AreEqual("1990-01-15",     restored.DateOfBirth);
            Assert.AreEqual("male",           restored.Gender);
            Assert.AreEqual("id",             restored.Language);
            Assert.AreEqual("ID",             restored.Country);
            Assert.AreEqual("IDR",            restored.Currency);
            Assert.AreEqual(PaymentType.playstore, restored.PaymentType);
        }

        [Test]
        public void User_ShallowCopy_IsIndependentCopy()
        {
            var original = new User { Id = 10L, Nickname = "Alice" };
            var copy = original.ShallowCopy();

            copy.Nickname = "Bob";
            Assert.AreEqual("Alice", original.Nickname, "ShallowCopy must not share primitive fields");
        }

        // ─── Credential ───────────────────────────────────────────────────────

        [Test]
        public void Credential_JsonRoundTrip_PreservesAllFields()
        {
            var original = new Credential
            {
                Id = 99L,
                Provider = "google",
                DisplayText = "alice@gmail.com"
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<Credential>(json);

            Assert.AreEqual(99L,              restored.Id);
            Assert.AreEqual("google",         restored.Provider);
            Assert.AreEqual("alice@gmail.com", restored.DisplayText);
        }

        [Test]
        public void Credential_ShallowCopy_IsIndependentCopy()
        {
            var original = new Credential { Id = 5L, Provider = "email" };
            var copy = original.ShallowCopy();

            copy.Provider = "facebook";
            Assert.AreEqual("email", original.Provider);
        }

        // ─── Player ───────────────────────────────────────────────────────────

        [Test]
        public void Player_JsonRoundTrip_PreservesAllFields()
        {
            var original = new Player
            {
                Id = 7L,
                AccessToken = "jwt.token.here",
                RoleId = "mage",
                ServerId = "svr-1",
                Username = "Warrior001",
                GameId = 3L,
                GameName = "Super RPG",
                GamePlatformId = 2L,
                GamePlatform = "google",
                GameOS = "android",
                BundleId = "com.example.game",
                UserId = 42L,
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<Player>(json);

            Assert.AreEqual(7L,               restored.Id);
            Assert.AreEqual("jwt.token.here", restored.AccessToken);
            Assert.AreEqual("mage",           restored.RoleId);
            Assert.AreEqual("svr-1",          restored.ServerId);
            Assert.AreEqual("Warrior001",     restored.Username);
            Assert.AreEqual(3L,               restored.GameId);
            Assert.AreEqual("Super RPG",      restored.GameName);
            Assert.AreEqual(2L,               restored.GamePlatformId);
            Assert.AreEqual("google",         restored.GamePlatform);
            Assert.AreEqual("android",        restored.GameOS);
            Assert.AreEqual("com.example.game", restored.BundleId);
            Assert.AreEqual(42L,              restored.UserId);
        }

        [Test]
        public void Player_ShallowCopy_IsIndependent()
        {
            var original = new Player { Id = 1L, RoleId = "archer" };
            var copy = original.ShallowCopy();
            copy.RoleId = "mage";
            Assert.AreEqual("archer", original.RoleId);
        }

        // ─── Game ─────────────────────────────────────────────────────────────

        [Test]
        public void Game_JsonRoundTrip_PreservesAllFields()
        {
            var original = new Game { Id = 5L, Name = "Idle World", GamePlatformId = 2L };
            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<Game>(json);

            Assert.AreEqual(5L, restored.Id);
            Assert.AreEqual("Idle World", restored.Name);
            Assert.AreEqual(2L, restored.GamePlatformId);
        }

        // ─── GamePlatform ─────────────────────────────────────────────────────

        [Test]
        public void GamePlatform_JsonRoundTrip_PreservesAllFields()
        {
            var original = new GamePlatform
            {
                Id = 3L,
                OS = "android",
                Platform = "google",
                BundleId = "com.example.game"
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<GamePlatform>(json);

            Assert.AreEqual(3L, restored.Id);
            Assert.AreEqual("android", restored.OS);
            Assert.AreEqual("google", restored.Platform);
            Assert.AreEqual("com.example.game", restored.BundleId);
        }

        // ─── ExchangeTokenRequest ─────────────────────────────────────────────

        [Test]
        public void ExchangeTokenRequest_JsonRoundTrip_PreservesAllFields()
        {
            var original = new ExchangeTokenRequest
            {
                NextBundleId = "com.other.game",
                InitPlayer = true,
                NextDistributionPlatform = "apple"
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<ExchangeTokenRequest>(json);

            Assert.AreEqual("com.other.game", restored.NextBundleId);
            Assert.IsTrue(restored.InitPlayer);
            Assert.AreEqual("apple", restored.NextDistributionPlatform);
        }

        // ─── PlayerToken ──────────────────────────────────────────────────────

        [Test]
        public void PlayerToken_JsonRoundTrip_PreservesNestedObjects()
        {
            var original = new PlayerToken
            {
                AccessToken = "access-token-abc",
                Player = new Player { Id = 1L, Username = "Hero" },
                User = new User { Id = 2L, Nickname = "Nick" },
                Credential = new Credential { Provider = "email" },
                Game = new Game { Id = 10L, Name = "Quest" },
                GamePlatform = new GamePlatform { OS = "ios", Platform = "apple" }
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<PlayerToken>(json);

            Assert.AreEqual("access-token-abc", restored.AccessToken);
            Assert.AreEqual(1L,      restored.Player?.Id);
            Assert.AreEqual("Hero",  restored.Player?.Username);
            Assert.AreEqual(2L,      restored.User?.Id);
            Assert.AreEqual("Nick",  restored.User?.Nickname);
            Assert.AreEqual("email", restored.Credential?.Provider);
            Assert.AreEqual(10L,     restored.Game?.Id);
            Assert.AreEqual("ios",   restored.GamePlatform?.OS);
        }

        // ─── UserBundle.DisplayName ───────────────────────────────────────────

        [Test]
        public void UserBundle_DisplayName_WithNickname_UsesNickname()
        {
            var bundle = new UserBundle
            {
                User = new User { Id = 1L, Nickname = "Alice" },
                Credential = new Credential { Provider = "google", DisplayText = "alice@g.com" }
            };
            Assert.AreEqual("Alice", bundle.DisplayName);
        }

        [Test]
        public void UserBundle_DisplayName_GuestUser_UsesGuestPrefix()
        {
            var bundle = new UserBundle
            {
                User = new User { Id = 5L, Nickname = "" },
                Credential = new Credential { Provider = "device_id", DisplayText = "" }
            };
            StringAssert.StartsWith("Guest", bundle.DisplayName);
        }

        [Test]
        public void UserBundle_DisplayName_SocialWithDisplayText_UsesDisplayText()
        {
            var bundle = new UserBundle
            {
                User = new User { Id = 3L, Nickname = "" },
                Credential = new Credential { Provider = "google", DisplayText = "bob@gmail.com" }
            };
            Assert.AreEqual("bob@gmail.com", bundle.DisplayName);
        }

        [Test]
        public void UserBundle_DisplayName_NoNicknameNoDisplayText_UsesUserPrefix()
        {
            var bundle = new UserBundle
            {
                User = new User { Id = 7L, Nickname = "" },
                Credential = new Credential { Provider = "email", DisplayText = "" }
            };
            Assert.AreEqual("User 7", bundle.DisplayName);
        }

        [Test]
        public void UserBundle_DisplayName_NoUser_NoctuaPlayerFallback()
        {
            var bundle = new UserBundle { User = null, Credential = null };
            Assert.AreEqual("Noctua Player", bundle.DisplayName);
        }

        [Test]
        public void UserBundle_IsGuest_FromCredentialDeviceId_ReturnsTrue()
        {
            var bundle = new UserBundle
            {
                User = new User { IsGuest = false },
                Credential = new Credential { Provider = "device_id" }
            };
            Assert.IsTrue(bundle.IsGuest);
        }

        [Test]
        public void UserBundle_Empty_HasNullComponents()
        {
            var empty = UserBundle.Empty;
            Assert.IsNull(empty.User);
            Assert.IsNull(empty.Credential);
            Assert.IsNull(empty.Player);
            Assert.IsNotNull(empty.PlayerAccounts);
            Assert.AreEqual(0, empty.PlayerAccounts.Count);
        }

        // ─── LoginAsGuestRequest ──────────────────────────────────────────────

        [Test]
        public void LoginAsGuestRequest_JsonRoundTrip_PreservesAllFields()
        {
            var original = new LoginAsGuestRequest
            {
                DeviceId = "device-abc-123",
                BundleId = "com.game.x",
                DistributionPlatform = "google"
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<LoginAsGuestRequest>(json);

            Assert.AreEqual("device-abc-123", restored.DeviceId);
            Assert.AreEqual("com.game.x",     restored.BundleId);
            Assert.AreEqual("google",         restored.DistributionPlatform);
        }

        // ─── SocialRedirectUrlResponse ────────────────────────────────────────

        [Test]
        public void SocialRedirectUrlResponse_JsonRoundTrip()
        {
            var original = new SocialRedirectUrlResponse { RedirectUrl = "https://auth.noctua.gg/callback" };
            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<SocialRedirectUrlResponse>(json);
            Assert.AreEqual("https://auth.noctua.gg/callback", restored.RedirectUrl);
        }

        // ─── DeletePlayerAccountResponse ──────────────────────────────────────

        [Test]
        public void DeletePlayerAccountResponse_JsonRoundTrip()
        {
            var original = new DeletePlayerAccountResponse { IsDeleted = true };
            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<DeletePlayerAccountResponse>(json);
            Assert.IsTrue(restored.IsDeleted);
        }

        // ─── CloudSaveMetadata / CloudSaveListResponse ────────────────────────

        [Test]
        public void CloudSaveMetadata_JsonRoundTrip_PreservesAllFields()
        {
            var original = new CloudSaveMetadata
            {
                SlotKey = "slot_1",
                ContentType = "application/json",
                SizeBytes = 1024,
                Checksum = "sha256:abc",
                CreatedAt = "2026-01-01T00:00:00Z",
                UpdatedAt = "2026-02-01T00:00:00Z"
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<CloudSaveMetadata>(json);

            Assert.AreEqual("slot_1",              restored.SlotKey);
            Assert.AreEqual("application/json",    restored.ContentType);
            Assert.AreEqual(1024,                  restored.SizeBytes);
            Assert.AreEqual("sha256:abc",          restored.Checksum);
            Assert.AreEqual("2026-01-01T00:00:00Z", restored.CreatedAt);
            Assert.AreEqual("2026-02-01T00:00:00Z", restored.UpdatedAt);
        }

        [Test]
        public void CloudSaveListResponse_JsonRoundTrip_PreservesList()
        {
            var original = new CloudSaveListResponse
            {
                Total = 2,
                Saves = new List<CloudSaveMetadata>
                {
                    new CloudSaveMetadata { SlotKey = "a", SizeBytes = 10 },
                    new CloudSaveMetadata { SlotKey = "b", SizeBytes = 20 },
                }
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<CloudSaveListResponse>(json);

            Assert.AreEqual(2, restored.Total);
            Assert.AreEqual(2, restored.Saves.Count);
            Assert.AreEqual("a", restored.Saves[0].SlotKey);
            Assert.AreEqual("b", restored.Saves[1].SlotKey);
        }

        // ─── SocialLoginRequest ───────────────────────────────────────────────

        [Test]
        public void SocialLoginRequest_JsonRoundTrip_PreservesAllFields()
        {
            var original = new SocialLoginRequest
            {
                Code = "auth-code-xyz",
                State = "csrf-state-abc",
                RedirectUri = "myapp://oauth",
                NoBindGuest = true
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<SocialLoginRequest>(json);

            Assert.AreEqual("auth-code-xyz", restored.Code);
            Assert.AreEqual("csrf-state-abc", restored.State);
            Assert.AreEqual("myapp://oauth", restored.RedirectUri);
            Assert.IsTrue(restored.NoBindGuest);
        }

        // ─── SocialLinkRequest ────────────────────────────────────────────────

        [Test]
        public void SocialLinkRequest_JsonRoundTrip_PreservesAllFields()
        {
            var original = new SocialLinkRequest
            {
                Code = "link-code",
                State = "link-state",
                RedirectUri = "myapp://link"
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<SocialLinkRequest>(json);

            Assert.AreEqual("link-code",   restored.Code);
            Assert.AreEqual("link-state",  restored.State);
            Assert.AreEqual("myapp://link", restored.RedirectUri);
        }

        // ─── BindRequest ──────────────────────────────────────────────────────

        [Test]
        public void BindRequest_JsonRoundTrip_PreservesGuestToken()
        {
            var original = new BindRequest { GuestToken = "guest-jwt-token" };
            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<BindRequest>(json);
            Assert.AreEqual("guest-jwt-token", restored.GuestToken);
        }

        // ─── CredPair ─────────────────────────────────────────────────────────

        [Test]
        public void CredPair_JsonRoundTrip_PreservesAllFields()
        {
            var original = new CredPair
            {
                CredKey = "user@example.com",
                CredSecret = "s3cur3pass",
                Provider = "email",
                NoBindGuest = true,
                RegExtra = new Dictionary<string, string> { { "phone_verify_id", "id-123" } }
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<CredPair>(json);

            Assert.AreEqual("user@example.com", restored.CredKey);
            Assert.AreEqual("s3cur3pass",        restored.CredSecret);
            Assert.AreEqual("email",             restored.Provider);
            Assert.IsTrue(restored.NoBindGuest);
            Assert.IsTrue(restored.RegExtra.ContainsKey("phone_verify_id"));
            Assert.AreEqual("id-123",            restored.RegExtra["phone_verify_id"]);
        }

        // ─── CredentialVerification ───────────────────────────────────────────

        [Test]
        public void CredentialVerification_JsonRoundTrip_PreservesAllFields()
        {
            var original = new CredentialVerification
            {
                Id = 55,
                Code = "123456",
                NoBindGuest = false,
                NewPassword = "newPass99"
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<CredentialVerification>(json);

            Assert.AreEqual(55,          restored.Id);
            Assert.AreEqual("123456",    restored.Code);
            Assert.IsFalse(restored.NoBindGuest);
            Assert.AreEqual("newPass99", restored.NewPassword);
        }

        // ─── PlayerAccountData ────────────────────────────────────────────────

        [Test]
        public void PlayerAccountData_JsonRoundTrip_PreservesAllFields()
        {
            var original = new PlayerAccountData
            {
                IngameUsername = "Warrior007",
                IngameServerId = "svr-asia",
                IngameRoleId = "paladin",
                Extra = new Dictionary<string, string> { { "vip", "gold" } }
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<PlayerAccountData>(json);

            Assert.AreEqual("Warrior007",  restored.IngameUsername);
            Assert.AreEqual("svr-asia",    restored.IngameServerId);
            Assert.AreEqual("paladin",     restored.IngameRoleId);
            Assert.AreEqual("gold",        restored.Extra["vip"]);
        }

        // ─── UpdateUserRequest ────────────────────────────────────────────────

        [Test]
        public void UpdateUserRequest_JsonRoundTrip_PreservesAllFields()
        {
            var dob = new DateTime(1995, 6, 15, 0, 0, 0, DateTimeKind.Utc);
            var original = new UpdateUserRequest
            {
                Nickname = "NewNick",
                DateOfBirth = dob,
                Gender = "female",
                PictureUrl = "https://cdn.noctua.gg/pic.png",
                Language = "vi",
                Country = "VN",
                Currency = "VND"
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<UpdateUserRequest>(json);

            Assert.AreEqual("NewNick", restored.Nickname);
            Assert.AreEqual(dob,       restored.DateOfBirth);
            Assert.AreEqual("female",  restored.Gender);
            Assert.AreEqual("https://cdn.noctua.gg/pic.png", restored.PictureUrl);
            Assert.AreEqual("vi",      restored.Language);
            Assert.AreEqual("VN",      restored.Country);
            Assert.AreEqual("VND",     restored.Currency);
        }

        // ─── ProfileOptionData / GeneralProfileData ───────────────────────────

        [Test]
        public void ProfileOptionData_JsonRoundTrip_PreservesNestedLists()
        {
            var original = new ProfileOptionData
            {
                Countries = new List<GeneralProfileData>
                {
                    new GeneralProfileData { IsoCode = "ID", NativeName = "Indonesia", EnglishName = "Indonesia" }
                },
                Languages = new List<GeneralProfileData>
                {
                    new GeneralProfileData { IsoCode = "id", NativeName = "Bahasa Indonesia", EnglishName = "Indonesian" }
                },
                Currencies = new List<GeneralProfileData>
                {
                    new GeneralProfileData { IsoCode = "IDR", NativeName = "Rupiah", EnglishName = "Indonesian Rupiah" }
                }
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<ProfileOptionData>(json);

            Assert.AreEqual(1,             restored.Countries.Count);
            Assert.AreEqual("ID",          restored.Countries[0].IsoCode);
            Assert.AreEqual(1,             restored.Languages.Count);
            Assert.AreEqual("id",          restored.Languages[0].IsoCode);
            Assert.AreEqual(1,             restored.Currencies.Count);
            Assert.AreEqual("IDR",         restored.Currencies[0].IsoCode);
            Assert.AreEqual("Indonesian Rupiah", restored.Currencies[0].EnglishName);
        }

        // ─── RegisterWithEmail* types ─────────────────────────────────────────

        [Test]
        public void RegisterWithEmailSendPhoneNumberVerification_JsonRoundTrip()
        {
            var original = new RegisterWithEmailSendPhoneNumberVerification { PhoneNumber = "+628123456789" };
            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<RegisterWithEmailSendPhoneNumberVerification>(json);
            Assert.AreEqual("+628123456789", restored.PhoneNumber);
        }

        [Test]
        public void RegisterWithEmailSendPhoneNumberVerificationResponse_JsonRoundTrip()
        {
            var original = new RegisterWithEmailSendPhoneNumberVerificationResponse { VerificationId = "vfy-abc" };
            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<RegisterWithEmailSendPhoneNumberVerificationResponse>(json);
            Assert.AreEqual("vfy-abc", restored.VerificationId);
        }

        [Test]
        public void RegisterWithEmailVerifyPhoneNumberVerification_JsonRoundTrip()
        {
            var original = new RegisterWithEmailVerifyPhoneNumberVerification
            {
                VerificationId = "vfy-abc",
                Code = "654321"
            };
            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<RegisterWithEmailVerifyPhoneNumberVerification>(json);
            Assert.AreEqual("vfy-abc", restored.VerificationId);
            Assert.AreEqual("654321",  restored.Code);
        }

        [Test]
        public void RegisterWithEmailVerifyPhoneNumberVerificationResponse_CanBeInstantiated()
        {
            var obj = new RegisterWithEmailVerifyPhoneNumberVerificationResponse();
            Assert.IsNotNull(obj);
        }
    }
}
