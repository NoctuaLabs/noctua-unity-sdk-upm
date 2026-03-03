using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Shared constants used across the Noctua SDK authentication layer.
    /// </summary>
    public static class Constants
    {
        /// <summary>PlayerPrefs key used to persist the serialized account container.</summary>
        public const string PlayerPrefsKeyAccountContainer = "NoctuaAccountContainer";
        // GAME_ID and USER_ID need to be replaced before use
        /// <summary>Base URL for the embedded customer service web view (Crisp chat).</summary>
        public const string CustomerServiceBaseUrl = "https://noctua.gg/embed-webview?url=https%3A%2F%2Fgo.crisp.chat%2Fchat%2Fembed%2F%3Fwebsite_id%3Dc4e95a3a-1fd1-49a2-92ea-a7cb5427bcd9&reason=general&vipLevel=";
    }

    /// <summary>
    /// Identifies the payment channel used for a transaction.
    /// </summary>
    [Preserve]
    public enum PaymentType
    {
        /// <summary>Payment type is not determined.</summary>
        unknown,
        /// <summary>Apple App Store payment.</summary>
        appstore,
        /// <summary>Google Play Store payment.</summary>
        playstore,
        /// <summary>Noctua store (web-based) payment.</summary>
        noctuastore,
        /// <summary>Noctua store redeem code payment.</summary>
        noctuastore_redeem,
        /// <summary>Direct payment (server-to-server).</summary>
        direct
    }

    /// <summary>
    /// Represents a Noctua platform user account with profile and credential information.
    /// </summary>
    [Preserve]
    public class User
    {
        /// <summary>Unique server-side user identifier.</summary>
        [JsonProperty("id")]
        public long Id;

        /// <summary>User-chosen display nickname.</summary>
        [JsonProperty("nickname")]
        public string Nickname;

        /// <summary>Email address associated with the user account.</summary>
        [JsonProperty("email_address")]
        public string EmailAddress;

        /// <summary>Phone number associated with the user account.</summary>
        [JsonProperty("phone_number")]
        public string PhoneNumbers;

        /// <summary>URL of the user's profile picture.</summary>
        [JsonProperty("picture_url")]
        public string PictureUrl;

        /// <summary>List of authentication credentials linked to this user.</summary>
        [JsonProperty("credentials")]
        public List<Credential> Credentials;

        /// <summary>Whether this user was created as a guest (device-id based) account.</summary>
        [JsonProperty("is_guest")]
        public bool IsGuest;

        /// <summary>User's date of birth as a string.</summary>
        [JsonProperty("date_of_birth")]
        public string DateOfBirth;

        /// <summary>User's gender.</summary>
        [JsonProperty("gender")]
        public string Gender;

        /// <summary>ISO language code preferred by the user.</summary>
        [JsonProperty("language")]
        public string Language;

        /// <summary>ISO country code of the user.</summary>
        [JsonProperty("country")]
        public string Country;

        /// <summary>ISO currency code preferred by the user.</summary>
        [JsonProperty("currency")]
        public string Currency;

        /// <summary>Default payment type for this user.</summary>
        [JsonProperty("payment_type")]
        public PaymentType PaymentType;

        /// <summary>Creates a shallow copy of this user instance.</summary>
        public User ShallowCopy()
        {
            return (User)MemberwiseClone();
        }
    }

    /// <summary>
    /// Represents a single authentication credential (e.g., email, social login, device ID) linked to a user.
    /// </summary>
    [Preserve]
    public class Credential
    {
        /// <summary>Unique server-side credential identifier.</summary>
        [JsonProperty("id")]
        public long Id;

        /// <summary>Authentication provider name (e.g., "google", "facebook", "email", "device_id").</summary>
        [JsonProperty("provider")]
        public string Provider;

        /// <summary>Human-readable display text for this credential (e.g., email address or social account name).</summary>
        [JsonProperty("display_text")]
        public string DisplayText;

        /// <summary>Creates a shallow copy of this credential instance.</summary>
        public Credential ShallowCopy()
        {
            return (Credential)MemberwiseClone();
        }
    }

    /// <summary>
    /// Represents a player account within a specific game, linked to a Noctua user.
    /// </summary>
    [Preserve]
    public class Player
    {
        /// <summary>JWT access token for authenticating API requests on behalf of this player.</summary>
        [JsonProperty("access_token")]
        public string AccessToken;

        /// <summary>Unique server-side player identifier.</summary>
        [JsonProperty("id")]
        public long Id;

        /// <summary>In-game role identifier set by the game client.</summary>
        [JsonProperty("role_id")]
        public string RoleId;

        /// <summary>In-game server identifier set by the game client.</summary>
        [JsonProperty("server_id")]
        public string ServerId;

        /// <summary>In-game username for this player.</summary>
        [JsonProperty("username")] // in-game
        public string Username;

        /// <summary>Identifier of the game this player belongs to.</summary>
        [JsonProperty("game_id")]
        public long GameId;

        /// <summary>Display name of the game.</summary>
        [JsonProperty("game_name")]
        public string GameName;

        /// <summary>Identifier of the game platform entry.</summary>
        [JsonProperty("game_platform_id")]
        public long GamePlatformId;

        /// <summary>Distribution platform name (e.g., "google", "apple").</summary>
        [JsonProperty("game_platform")]
        public string GamePlatform;

        /// <summary>Operating system for this game platform (e.g., "android", "ios").</summary>
        [JsonProperty("game_os")]
        public string GameOS;

        /// <summary>Application bundle identifier (e.g., "com.example.game").</summary>
        [JsonProperty("bundle_id")]
        public string BundleId;

        /// <summary>The Noctua user this player belongs to.</summary>
        [JsonProperty("user")]
        public User User;

        /// <summary>Foreign key referencing the owning user's identifier.</summary>
        [JsonProperty("user_id")]
        public long UserId;

        /// <summary>Creates a shallow copy of this player instance.</summary>
        public Player ShallowCopy()
        {
            return (Player)MemberwiseClone();
        }
    }

    /// <summary>
    /// Represents a game registered in the Noctua platform.
    /// </summary>
    [Preserve]
    public class Game
    {
        /// <summary>Unique game identifier.</summary>
        [JsonProperty("id")]
        public long Id;

        /// <summary>Display name of the game.</summary>
        [JsonProperty("name")]
        public string Name;

        /// <summary>Identifier of the associated game platform entry.</summary>
        [JsonProperty("platform_id")]
        public long GamePlatformId;

    }

    /// <summary>
    /// Represents a platform-specific build of a game (e.g., Android Google Play, iOS App Store).
    /// </summary>
    [Preserve]
    public class GamePlatform
    {
        /// <summary>Unique game platform identifier.</summary>
        [JsonProperty("id")]
        public long Id;

        /// <summary>Operating system (e.g., "android", "ios").</summary>
        [JsonProperty("os")]
        public string OS;

        /// <summary>Distribution platform name (e.g., "google", "apple").</summary>
        [JsonProperty("platform")]
        public string Platform;

        /// <summary>Application bundle identifier for this platform build.</summary>
        [JsonProperty("bundle_id")]
        public string BundleId;
    }

    /// <summary>
    /// Request payload for exchanging an access token to target a different game or platform.
    /// </summary>
    [Preserve]
    public class ExchangeTokenRequest
    {
        /// <summary>Bundle identifier of the target game to exchange the token for.</summary>
        [JsonProperty("next_bundle_id")]
        public string NextBundleId;

        /// <summary>Whether to initialize a player record in the target game if one does not exist.</summary>
        [JsonProperty("init_player")]
        public bool InitPlayer;

        /// <summary>Distribution platform of the target game (e.g., "google", "apple").</summary>
        [JsonProperty("next_distribution_platform")]
        public string NextDistributionPlatform;
    }

    /// <summary>
    /// Response payload containing an access token along with the associated player, user, credential, and game data.
    /// </summary>
    [Preserve]
    public class PlayerToken
    {
        /// <summary>JWT access token for authenticating subsequent API requests.</summary>
        [JsonProperty("access_token")]
        public string AccessToken;

        /// <summary>Player record associated with this token.</summary>
        [JsonProperty("player")]
        public Player Player;

        /// <summary>User account associated with this token.</summary>
        [JsonProperty("user")]
        public User User;

        /// <summary>Credential used during authentication.</summary>
        [JsonProperty("credential")]
        public Credential Credential;

        /// <summary>Game information linked to this token.</summary>
        [JsonProperty("game")]
        public Game Game;

        /// <summary>Game platform information linked to this token.</summary>
        [JsonProperty("game_platform")]
        public GamePlatform GamePlatform;
    }


    /// <summary>
    /// Aggregates a user, their active credential, current player, and all player accounts into a single bundle.
    /// Used for account switching and display in the user center.
    /// </summary>
    [Preserve]
    public class UserBundle
    {
        /// <summary>The Noctua user account.</summary>
        [JsonProperty("user")]
        public User User;

        /// <summary>The credential that was used to authenticate this user.</summary>
        [JsonProperty("credential")]
        public Credential Credential;

        /// <summary>The currently active player record for the current game.</summary>
        [JsonProperty("player")]
        public Player Player;

        /// <summary>All player accounts across games belonging to this user.</summary>
        [JsonProperty("player_accounts")]
        public List<Player> PlayerAccounts;

        /// <summary>Timestamp of when this user bundle was last actively used.</summary>
        [JsonProperty("last_used")]
        public DateTimeOffset LastUsed;

        /// <summary>Whether this bundle was recently used (used for sorting in account switcher).</summary>
        [JsonProperty("is_recent")]
        public bool IsRecent;

        /// <summary>Returns true if this account is a guest (device-id based) account.</summary>
        [JsonIgnore]
        public bool IsGuest => User?.IsGuest ?? Credential?.Provider == "device_id";

        /// <summary>Human-readable display name derived from nickname, credential, or fallback text.</summary>
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

        /// <summary>Returns an empty UserBundle with null user/credential/player and an empty player accounts list.</summary>
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

    /// <summary>
    /// Request payload for guest login using the device identifier.
    /// </summary>
    [Preserve]
    public class LoginAsGuestRequest
    {
        /// <summary>Unique device identifier used as the guest credential.</summary>
        [JsonProperty("device_id")]
        public string DeviceId;

        /// <summary>Application bundle identifier of the requesting game.</summary>
        [JsonProperty("bundle_id")]
        public string BundleId;

        /// <summary>Distribution platform (e.g., "google", "apple").</summary>
        [JsonProperty("distribution_platform")]
        public string DistributionPlatform;
    }

    /// <summary>
    /// Response containing the OAuth redirect URL for social login flows.
    /// </summary>
    [Preserve]
    public class SocialRedirectUrlResponse
    {
        /// <summary>URL to redirect the user to for social provider authentication.</summary>
        [JsonProperty("redirect_url")]
        public string RedirectUrl;
    }

    /// <summary>
    /// Response indicating whether a player account was successfully deleted.
    /// </summary>
    [Preserve]
    public class DeletePlayerAccountResponse
    {
        /// <summary>True if the player account was deleted successfully.</summary>
        [JsonProperty("is_deleted")]
        public bool IsDeleted;
    }

    /// <summary>
    /// Metadata for a single cloud save slot, including size, content type, and timestamps.
    /// </summary>
    [Preserve]
    public class CloudSaveMetadata
    {
        /// <summary>Unique key identifying the save slot.</summary>
        [JsonProperty("slot_key")]
        public string SlotKey;

        /// <summary>MIME content type of the saved data (e.g., "application/json").</summary>
        [JsonProperty("content_type")]
        public string ContentType;

        /// <summary>Size of the saved data in bytes.</summary>
        [JsonProperty("size_bytes")]
        public int SizeBytes;

        /// <summary>Checksum hash of the saved data for integrity verification.</summary>
        [JsonProperty("checksum")]
        public string Checksum;

        /// <summary>ISO 8601 timestamp when the save slot was created.</summary>
        [JsonProperty("created_at")]
        public string CreatedAt;

        /// <summary>ISO 8601 timestamp when the save slot was last updated.</summary>
        [JsonProperty("updated_at")]
        public string UpdatedAt;
    }

    /// <summary>
    /// Response containing a paginated list of cloud save slots.
    /// </summary>
    [Preserve]
    public class CloudSaveListResponse
    {
        /// <summary>List of cloud save slot metadata entries.</summary>
        [JsonProperty("saves")]
        public List<CloudSaveMetadata> Saves;

        /// <summary>Total number of save slots available for this player.</summary>
        [JsonProperty("total")]
        public int Total;
    }

    /// <summary>
    /// Request payload for completing a social login after the OAuth redirect callback.
    /// </summary>
    [Preserve]
    public class SocialLoginRequest
    {
        /// <summary>Authorization code returned by the social provider.</summary>
        [JsonProperty("code")]
        public string Code;

        /// <summary>OAuth state parameter for CSRF verification.</summary>
        [JsonProperty("state")]
        public string State;

        /// <summary>Redirect URI that was used in the OAuth flow.</summary>
        [JsonProperty("redirect_uri")]
        public string RedirectUri;

        /// <summary>When true, prevents automatic binding of the social account to an existing guest account.</summary>
        [JsonProperty("no_bind_guest")]
        public bool NoBindGuest;
    }

    /// <summary>
    /// Request payload for linking a social provider account to an existing user.
    /// </summary>
    [Preserve]
    public class SocialLinkRequest
    {
        /// <summary>Authorization code returned by the social provider.</summary>
        [JsonProperty("code")]
        public string Code;

        /// <summary>OAuth state parameter for CSRF verification.</summary>
        [JsonProperty("state")]
        public string State;

        /// <summary>Redirect URI that was used in the OAuth flow.</summary>
        [JsonProperty("redirect_uri")]
        public string RedirectUri;
    }

    /// <summary>
    /// Request payload for binding a guest account to the current authenticated user.
    /// </summary>
    [Preserve]
    public class BindRequest
    {
        /// <summary>Access token of the guest account to be bound.</summary>
        [JsonProperty("guest_token")]
        public string GuestToken;
    }

    /// <summary>
    /// Credential key/secret pair used for email or phone-based authentication and registration.
    /// </summary>
    [Preserve]
    public class CredPair
    {
        /// <summary>Credential key (e.g., email address or phone number).</summary>
        [JsonProperty("cred_key")]
        public string CredKey;

        /// <summary>Credential secret (e.g., password or OTP).</summary>
        [JsonProperty("cred_secret")]
        public string CredSecret;

        /// <summary>Authentication provider name (e.g., "email", "phone").</summary>
        [JsonProperty("provider")]
        public string Provider;

        /// <summary>When true, prevents automatic binding to an existing guest account.</summary>
        [JsonProperty("no_bind_guest")]
        public bool NoBindGuest;

        /// <summary>Additional registration metadata (e.g., phone verification ID for VN legal compliance).</summary>
        [JsonProperty("reg_extra")]
        public Dictionary<string, string> RegExtra;
    }

    /// <summary>
    /// Request payload for verifying a credential (e.g., email verification code or password reset).
    /// </summary>
    [Preserve]
    public class CredentialVerification
    {
        /// <summary>Verification record identifier returned by the server.</summary>
        [JsonProperty("id")]
        public int Id;

        /// <summary>Verification code entered by the user.</summary>
        [JsonProperty("code")]
        public string Code;

        /// <summary>When true, prevents automatic binding to an existing guest account.</summary>
        [JsonProperty("no_bind_guest")]
        public bool NoBindGuest;

        /// <summary>New password to set during a password reset flow.</summary>
        [JsonProperty("new_password")] // Used for password reset
        public string NewPassword;
    }

    /// <summary>
    /// Data provided by the game client to update the player's in-game account information.
    /// </summary>
    [Preserve]
    public class PlayerAccountData
    {
        /// <summary>In-game display username.</summary>
        [JsonProperty("ingame_username")]
        public string IngameUsername;

        /// <summary>In-game server identifier.</summary>
        [JsonProperty("ingame_server_id")]
        public string IngameServerId;

        /// <summary>In-game role/character identifier.</summary>
        [JsonProperty("ingame_role_id")]
        public string IngameRoleId;

        /// <summary>Additional key-value metadata for the player account.</summary>
        [JsonProperty("extra")]
        public Dictionary<string, string> Extra;
    }

    /// <summary>
    /// Request payload for updating user profile fields.
    /// </summary>
    [Preserve]
    public class UpdateUserRequest
    {
        /// <summary>New display nickname for the user.</summary>
        [JsonProperty("nickname")]
        public string Nickname;

        /// <summary>User's date of birth (null to leave unchanged).</summary>
        [JsonProperty("date_of_birth")]
        public DateTime? DateOfBirth;

        /// <summary>User's gender.</summary>
        [JsonProperty("gender")]
        public string Gender;

        /// <summary>URL of the user's profile picture.</summary>
        [JsonProperty("picture_url")]
        public string PictureUrl;

        /// <summary>ISO language code preferred by the user.</summary>
        [JsonProperty("language")]
        public string Language;

        /// <summary>ISO country code of the user.</summary>
        [JsonProperty("country")]
        public string Country;

        /// <summary>ISO currency code preferred by the user.</summary>
        [JsonProperty("currency")]
        public string Currency;
    }

    /// <summary>
    /// Contains available profile option lists (countries, languages, currencies) for user profile editing UI.
    /// </summary>
    [Preserve]
    public class ProfileOptionData
    {
        /// <summary>List of available countries.</summary>
        [JsonProperty("countries")]
        public List<GeneralProfileData> Countries;

        /// <summary>List of available languages.</summary>
        [JsonProperty("languages")]
        public List<GeneralProfileData> Languages;

        /// <summary>List of available currencies.</summary>
        [JsonProperty("currencies")]
        public List<GeneralProfileData> Currencies;
    }

    /// <summary>
    /// A single profile option entry (country, language, or currency) with ISO code and display names.
    /// </summary>
    [Preserve]
    public class GeneralProfileData
    {
        /// <summary>ISO code identifying this entry (e.g., "US", "en", "USD").</summary>
        [JsonProperty("iso_code")]
        public string IsoCode;

        /// <summary>Name in the entry's own native language/script.</summary>
        [JsonProperty("native_name")]
        public string NativeName;

        /// <summary>Name in English.</summary>
        [JsonProperty("english_name")]
        public string EnglishName;
    }

    /// <summary>
    /// Request payload to send a phone number verification SMS during email registration (required for VN legal compliance).
    /// </summary>
    [Preserve]
    public class RegisterWithEmailSendPhoneNumberVerification
    {
        /// <summary>Phone number to send the verification code to.</summary>
        [JsonProperty("phone_number")]
        public string PhoneNumber;
    }

    /// <summary>
    /// Response after sending a phone number verification SMS, containing the verification record ID.
    /// </summary>
    [Preserve]
    public class RegisterWithEmailSendPhoneNumberVerificationResponse
    {
        /// <summary>Server-generated verification ID to use when submitting the verification code.</summary>
        [JsonProperty("id")]
        public string VerificationId;
    }

    /// <summary>
    /// Request payload to verify a phone number verification code during email registration (required for VN legal compliance).
    /// </summary>
    [Preserve]
    public class RegisterWithEmailVerifyPhoneNumberVerification
    {
        /// <summary>Verification record ID returned by the send verification request.</summary>
        [JsonProperty("id")]
        public string VerificationId;

        /// <summary>Verification code entered by the user from the SMS.</summary>
        [JsonProperty("code")]
        public string Code;
    }

    /// <summary>
    /// Empty response indicating successful phone number verification during email registration.
    /// </summary>
    [Preserve]
    public class RegisterWithEmailVerifyPhoneNumberVerificationResponse
    {
    }
}
