using System;
using System.Collections.Generic;
using Newtonsoft.Json;
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
        noctuastore_redeem,
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
    public class CloudSaveMetadata
    {
        [JsonProperty("slot_key")]
        public string SlotKey;

        [JsonProperty("content_type")]
        public string ContentType;

        [JsonProperty("size_bytes")]
        public int SizeBytes;

        [JsonProperty("checksum")]
        public string Checksum;

        [JsonProperty("created_at")]
        public string CreatedAt;

        [JsonProperty("updated_at")]
        public string UpdatedAt;
    }

    [Preserve]
    public class CloudSaveListResponse
    {
        [JsonProperty("saves")]
        public List<CloudSaveMetadata> Saves;

        [JsonProperty("total")]
        public int Total;
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
}
