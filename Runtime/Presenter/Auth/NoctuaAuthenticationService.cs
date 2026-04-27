using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using com.noctuagames.sdk.Events;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Core authentication service handling guest login, email/password auth, social login, token exchange, and account management.
    /// </summary>
    public class NoctuaAuthenticationService : IAuthenticationService, IAccountEvents
    {
        /// <summary>
        /// Gets the list of all known user accounts across all games.
        /// </summary>
        public IReadOnlyList<UserBundle> AccountList => _accountContainer.Accounts;

        /// <summary>
        /// Gets the list of user accounts that have player data for the current game.
        /// </summary>
        public IReadOnlyList<UserBundle> CurrentGameAccountList => _accountContainer.CurrentGameAccounts;

        /// <summary>
        /// Gets the list of user accounts that only have player data for other games.
        /// </summary>
        public IReadOnlyList<UserBundle> OtherGamesAccountList => _accountContainer.OtherGamesAccounts;

        /// <summary>
        /// Gets whether the current user has a valid access token.
        /// </summary>
        public bool IsAuthenticated => !string.IsNullOrEmpty(_accountContainer.RecentAccount?.Player?.AccessToken);

        /// <summary>
        /// Gets the most recently authenticated user bundle for the current game.
        /// </summary>
        public UserBundle RecentAccount => _accountContainer.RecentAccount;

        /// <summary>
        /// Fires when the active account changes (e.g., after login, switch, or logout).
        /// </summary>
        public event Action<UserBundle> OnAccountChanged
        {
            add => _accountContainer.OnAccountChanged += value;
            remove => _accountContainer.OnAccountChanged -= value;
        }

        /// <summary>
        /// Fires when a player account is deleted from the server.
        /// </summary>
        public event Action<Player> OnAccountDeleted;

        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaAuthenticationService));
        private readonly string _clientId;
        private readonly string _baseUrl;
        private readonly string _bundleId;
        private readonly NoctuaLocale _locale;
        private readonly AccountContainer _accountContainer;
        private readonly IEventSender _eventSender;
        private OauthRedirectListener _oauthOauthRedirectListener;

        /// <summary>
        /// Initializes a new instance of the authentication service.
        /// </summary>
        /// <param name="baseUrl">Base URL for the Noctua authentication API.</param>
        /// <param name="clientId">Client identifier for API requests.</param>
        /// <param name="nativeAccountStore">Native platform account storage implementation.</param>
        /// <param name="locale">Optional locale provider for language preferences.</param>
        /// <param name="bundleId">Application bundle identifier.</param>
        /// <param name="eventSender">Optional event sender for analytics.</param>
        public NoctuaAuthenticationService(
            string baseUrl,
            string clientId,
            INativeAccountStore nativeAccountStore,
            NoctuaLocale locale = null,
            string bundleId = null,
            IEventSender eventSender = null
        )
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new ArgumentNullException(nameof(baseUrl));
            }

            if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentNullException(nameof(clientId));
            }

            if (string.IsNullOrEmpty(bundleId))
            {
                throw new ArgumentNullException(nameof(bundleId));
            }

            _clientId = clientId;
            _baseUrl = baseUrl;
            _locale = locale;
            _bundleId = bundleId;
            _eventSender = eventSender;
            _accountContainer = new AccountContainer(nativeAccountStore, bundleId, _locale);
        }

        /// <summary>
        /// Creates or retrieves a guest account using the device identifier.
        /// </summary>
        /// <returns>The authenticated guest user bundle.</returns>
        /// <exception cref="ApplicationException">Thrown when the application identifier is not set.</exception>
        public async UniTask<UserBundle> LoginAsGuestAsync()
        {
            if (string.IsNullOrEmpty(Application.identifier))
            {
                throw new ApplicationException($"App id for platform {Application.platform} is not set");
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/guest/login")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new LoginAsGuestRequest
                    {
                        DeviceId = SystemInfo.deviceUniqueIdentifier,
                        BundleId = Application.identifier,
                        DistributionPlatform = Utility.GetPlatformType()
                    }
                );


            var response = await request.Send<PlayerToken>();

            _accountContainer.UpdateRecentAccount(response);

            PlayerPrefs.SetString("NoctuaAccessToken", response.AccessToken);

            SetEventProperties(response);
            SendEvent("account_authenticated");

            return _accountContainer.RecentAccount;
        }

        /// <summary>
        /// Exchanges an existing access token for a new one scoped to the current game.
        /// </summary>
        /// <param name="accessToken">The access token to exchange.</param>
        /// <returns>The updated user bundle with the new token.</returns>
        public async UniTask<UserBundle> ExchangeTokenAsync(string accessToken)
        {
            var exchangeToken = new ExchangeTokenRequest
            {
                NextBundleId = _bundleId,
                InitPlayer = true,
                NextDistributionPlatform = Utility.GetPlatformType()
            };

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/token-exchange")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + accessToken)
                .WithJsonBody(exchangeToken);


            var response = await request.Send<PlayerToken>();

            _accountContainer.UpdateRecentAccount(response);

            PlayerPrefs.SetString("NoctuaAccessToken", response.AccessToken);

            return _accountContainer.RecentAccount;
        }

        /// <summary>
        /// Gets the OAuth redirect URL for social login with the specified provider.
        /// </summary>
        /// <param name="provider">Social auth provider name (e.g., "google", "facebook").</param>
        /// <param name="redirectUri">Optional custom redirect URI for desktop OAuth flows.</param>
        /// <returns>The redirect URL to open in a browser or webview.</returns>
        public async UniTask<string> GetSocialAuthRedirectURLAsync(string provider, string redirectUri = "")
        {
            if (!string.IsNullOrEmpty(redirectUri))
            {
                redirectUri = $"?redirect_uri={WebUtility.UrlEncode(redirectUri)}";
            }

            var request = new HttpRequest(HttpMethod.Get, $"{_baseUrl}/auth/{provider}/login/redirect{redirectUri}")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId);

            var redirectUrlResponse = await request.Send<SocialRedirectUrlResponse>();

            return redirectUrlResponse?.RedirectUrl;
        }
        /// <summary>
        /// Authenticates the user via a social provider callback (OAuth code exchange).
        /// </summary>
        /// <param name="provider">Social auth provider name (e.g., "google", "facebook").</param>
        /// <param name="payload">OAuth callback data containing code, state, and redirect URI.</param>
        /// <returns>The authenticated user bundle.</returns>
        public async UniTask<UserBundle> SocialLoginAsync(string provider, SocialLoginRequest payload)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/{provider}/login/callback")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(payload);

            if (!string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken) && RecentAccount.IsGuest)
            {
                request.WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);
            }

            var response = await request.Send<PlayerToken>();

            _accountContainer.UpdateRecentAccount(response);

            SetEventProperties(response);
            SendEvent("account_authenticated");
            SendEvent("account_authenticated_by_sso");

            return _accountContainer.RecentAccount;
        }

        /// <summary>
        /// Authenticates the user with email and password credentials.
        /// </summary>
        /// <param name="email">User email address.</param>
        /// <param name="password">User password.</param>
        /// <returns>The authenticated user bundle.</returns>
        // TODO: Add support for phone
        public async UniTask<UserBundle> LoginWithEmailAsync(string email, string password)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/login")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new CredPair
                    {
                        CredKey = email,
                        CredSecret = password
                    }
                )
                .NoVerboseLog();

            var response = await request.Send<PlayerToken>();

            _accountContainer.UpdateRecentAccount(response);


            SetEventProperties(response);
            SendEvent("account_authenticated");
            SendEvent("account_authenticated_by_email");

            return _accountContainer.RecentAccount;
        }

        /// <summary>
        /// Registers a new account with email and password, returning a verification object for code confirmation.
        /// </summary>
        /// <param name="email">Email address for the new account.</param>
        /// <param name="password">Password for the new account.</param>
        /// <param name="regExtra">Additional registration data (e.g., marketing consent).</param>
        /// <returns>A credential verification object containing the verification ID.</returns>
        // TODO: Add support for phone
        public async UniTask<CredentialVerification> RegisterWithEmailAsync(string email, string password, Dictionary<string, string> regExtra)
        {
            _log.Debug("RegisterWithEmailAsync");

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/register")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new CredPair
                    {
                        CredKey = email,
                        CredSecret = password,
                        Provider = "email",
                        RegExtra = regExtra
                    }
                )
                .NoVerboseLog();

            return await request.Send<CredentialVerification>();
        }

        /// <summary>
        /// Sends a phone number verification SMS as part of the email registration flow (VN legal compliance).
        /// </summary>
        /// <param name="phoneNumber">Phone number to verify.</param>
        /// <returns>Response containing the verification ID.</returns>
        // This API is a subset of email register to support VN legal purpose, not a full registration
        // That is why it has RegisterWithEmail prefix. RegisterWithPhoneNumber will have its own API in the future.
        public async UniTask<RegisterWithEmailSendPhoneNumberVerificationResponse> RegisterWithEmailSendPhoneNumberVerificationAsync(string phoneNumber)
        {
            _log.Debug("RegisterWithEmailSendPhoneNumberVerificationAsync");

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/register-phone-number")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new RegisterWithEmailSendPhoneNumberVerification
                    {
                        PhoneNumber = phoneNumber
                    }
                )
                .NoVerboseLog();

            return await request.Send<RegisterWithEmailSendPhoneNumberVerificationResponse>();
        }

        /// <summary>
        /// Verifies a phone number using the verification code sent via SMS during email registration.
        /// </summary>
        /// <param name="id">The verification ID from the send verification step.</param>
        /// <param name="code">The SMS verification code entered by the user.</param>
        /// <returns>Response indicating whether verification succeeded.</returns>
        public async UniTask<RegisterWithEmailVerifyPhoneNumberVerificationResponse> RegisterWithEmailVerifyPhoneNumberAsync(string id, string code)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/verify-phone-number-registration")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new RegisterWithEmailVerifyPhoneNumberVerification
                    {
                        VerificationId = id,
                        Code = code
                    }
                )
                .NoVerboseLog();

            return await request.Send<RegisterWithEmailVerifyPhoneNumberVerificationResponse>();
        }

        /// <summary>
        /// Completes email registration by verifying the email confirmation code and authenticating the user.
        /// </summary>
        /// <param name="id">The verification ID from the registration step.</param>
        /// <param name="code">The email verification code.</param>
        /// <returns>The newly created and authenticated user bundle.</returns>
        public async UniTask<UserBundle> VerifyEmailRegistrationAsync(int id, string code)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/verify-registration")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new CredentialVerification
                    {
                        Id = id,
                        Code = code,
                        NoBindGuest = true
                    }
                );

            var response = await request.Send<PlayerToken>();

            _accountContainer.UpdateRecentAccount(response);

            SetEventProperties(response);
            SendEvent("account_created");
            SendEvent("account_created_by_email");

            return _accountContainer.RecentAccount;
        }

        // TODO: Add support for phone

        /// <summary>
        /// Initiates a password reset flow by sending a verification code to the given email.
        /// </summary>
        /// <param name="email">The email address to send the reset code to.</param>
        /// <returns>A credential verification object containing the verification ID.</returns>
        public async UniTask<CredentialVerification> RequestResetPasswordAsync(string email)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/reset-password")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new CredPair
                    {
                        CredKey = email
                    }
                );

            var response = await request.Send<CredentialVerification>();

            SendEvent("reset_password_requested");

            return response;
        }

        // TODO: Add support for phone

        /// <summary>
        /// Completes the password reset by verifying the code and setting a new password.
        /// </summary>
        /// <param name="id">The verification ID from the reset request.</param>
        /// <param name="code">The verification code sent to the user's email.</param>
        /// <param name="newPassword">The new password to set.</param>
        /// <returns>A player token for the account with the reset password.</returns>
        public async UniTask<PlayerToken> ConfirmResetPasswordAsync(int id, string code, string newPassword)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/verify-reset-password")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new CredentialVerification
                    {
                        Id = id,
                        Code = code,
                        NewPassword = newPassword,
                    }
                )
                .NoVerboseLog();

            var response = await request.Send<PlayerToken>();

            SendEvent("reset_password_completed");

            return response;
        }

        /// <summary>
        /// Links a social provider credential to the current authenticated (non-guest) account.
        /// </summary>
        /// <param name="provider">Social auth provider name (e.g., "google", "facebook").</param>
        /// <param name="payload">OAuth callback data for the social link.</param>
        /// <returns>The newly linked credential.</returns>
        /// <exception cref="NoctuaException">Thrown when not authenticated or account is a guest.</exception>
        public async UniTask<Credential> SocialLinkAsync(string provider, SocialLinkRequest payload)
        {
            _log.Debug("SocialLinkAsync");
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw NoctuaException.MissingAccessToken;
            }

            if (RecentAccount.IsGuest)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Guest account cannot link email");
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/{provider}/link/callback")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken)
                .WithJsonBody(payload);

            var response = await request.Send<Credential>();

            SendEvent(
                "credential_added",
                new()
                {
                    { "new_credential_provider", response.Provider },
                    { "new_credential_id", response.Id }
                }
            );

            return response;
        }

        // TODO: Add support for phone

        /// <summary>
        /// Links an email/password credential to the current authenticated (non-guest) account.
        /// </summary>
        /// <param name="email">Email address to link.</param>
        /// <param name="password">Password for the email credential.</param>
        /// <returns>A credential verification object requiring code confirmation.</returns>
        /// <exception cref="NoctuaException">Thrown when not authenticated or account is a guest.</exception>
        public async UniTask<CredentialVerification> LinkWithEmailAsync(string email, string password)
        {
            _log.Debug("LinkWithEmailAsync");
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw NoctuaException.MissingAccessToken;
            }

            if (RecentAccount.IsGuest)
            {
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
                )
                .NoVerboseLog();

            return await request.Send<CredentialVerification>();
        }

        /// <summary>
        /// Completes email linking by verifying the confirmation code.
        /// </summary>
        /// <param name="id">The verification ID from the link request.</param>
        /// <param name="code">The email verification code.</param>
        /// <returns>The newly linked email credential.</returns>
        /// <exception cref="NoctuaException">Thrown when the access token is missing.</exception>
        public async UniTask<Credential> VerifyEmailLinkingAsync(int id, string code)
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw NoctuaException.MissingAccessToken;
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/verify-link")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken)
                .WithJsonBody(
                    new CredentialVerification
                    {
                        Id = id,
                        Code = code
                    }
                );

            var response = await request.Send<Credential>();

            SendEvent(
                "credential_added",
                new()
                {
                    { "new_credential_provider", response.Provider },
                    { "new_credential_id", response.Id }
                }
            );

            return response;
        }

        /// <summary>
        /// Verifies email registration for a guest account without binding, returning a token for the new account.
        /// </summary>
        /// <param name="id">The verification ID from the registration step.</param>
        /// <param name="code">The email verification code.</param>
        /// <returns>A player token for the verified account (not yet bound to the guest).</returns>
        /// <exception cref="NoctuaException">Thrown when the current account is not a guest.</exception>
        public async UniTask<PlayerToken> BeginVerifyEmailRegistrationAsync(int id, string code)
        {
            if (!RecentAccount.IsGuest)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Account is not a guest account");
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/verify-registration")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new CredentialVerification
                    {
                        Id = id,
                        Code = code,
                        NoBindGuest = true
                    }
                );

            request.WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            return await request.Send<PlayerToken>();
        }

        /// <summary>
        /// Verifies email linking for a guest account without binding, returning a token for the linked account.
        /// </summary>
        /// <param name="id">The verification ID from the link step.</param>
        /// <param name="code">The email verification code.</param>
        /// <returns>A player token for the linked account (not yet bound to the guest).</returns>
        /// <exception cref="NoctuaException">Thrown when the current account is not a guest.</exception>
        public async UniTask<PlayerToken> BeginVerifyEmailLinkingAsync(int id, string code)
        {
            if (!RecentAccount.IsGuest)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Account is not a guest account");
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/verify-link")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new CredentialVerification
                    {
                        Id = id,
                        Code = code,
                        NoBindGuest = true
                    }
                );

            request.WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            return await request.Send<PlayerToken>();
        }

        /// <summary>
        /// Gets a social login token for a guest account without binding the guest to the social account.
        /// </summary>
        /// <param name="provider">Social auth provider name (e.g., "google", "facebook").</param>
        /// <param name="payload">OAuth callback data.</param>
        /// <returns>A player token for the social account (not yet bound to the guest).</returns>
        /// <exception cref="NoctuaException">Thrown when the current account is not a guest.</exception>
        public async UniTask<PlayerToken> GetSocialLoginTokenAsync(string provider, SocialLoginRequest payload)
        {
            if (!RecentAccount.IsGuest)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Account is not a guest account");
            }

            payload.NoBindGuest = true;

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/{provider}/login/callback")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(payload);

            request.WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            return await request.Send<PlayerToken>();
        }

        /// <summary>
        /// Gets an email login token for a guest account without binding the guest to the email account.
        /// </summary>
        /// <param name="email">Email address.</param>
        /// <param name="password">Password.</param>
        /// <returns>A player token for the email account (not yet bound to the guest).</returns>
        /// <exception cref="NoctuaException">Thrown when the current account is not a guest.</exception>
        // TODO: Add support for phone
        public async UniTask<PlayerToken> GetEmailLoginTokenAsync(string email, string password)
        {
            if (!RecentAccount.IsGuest)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Account is not a guest account");
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/email/login")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithJsonBody(
                    new CredPair
                    {
                        CredKey = email,
                        CredSecret = password,
                        NoBindGuest = true
                    }
                )
                .NoVerboseLog();

            request.WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            return await request.Send<PlayerToken>();
        }

        /// <summary>
        /// Logs in using a pre-obtained player token, updating the recent account and firing auth events.
        /// </summary>
        /// <param name="playerToken">The player token obtained from a prior authentication step.</param>
        public void LoginWithToken(PlayerToken playerToken)
        {
            _accountContainer.UpdateRecentAccount(playerToken);

            SetEventProperties(playerToken);

            SendEvent("account_authenticated");

            var eventName = RecentAccount.Credential?.Provider switch
            {
                "email" => "account_authenticated_by_email",
                "device_id" => "account_authenticated_by_guest",
                _ => "account_authenticated_by_sso"
            };

            SendEvent(eventName);
        }

        /// <summary>
        /// Binds the current guest account to a target player account and logs in as the target.
        /// </summary>
        /// <param name="targetPlayer">The target player token to bind the guest to.</param>
        /// <returns>The authenticated user bundle after binding.</returns>
        /// <exception cref="NoctuaException">Thrown when the current account is not a guest or tokens are missing.</exception>
        public async UniTask<UserBundle> BindGuestAndLoginAsync(PlayerToken targetPlayer)
        {
            if (!RecentAccount.IsGuest)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Account is not a guest account");
            }

            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "origin access token is missing");
            }

            if (string.IsNullOrEmpty(targetPlayer?.AccessToken))
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "target access token is missing");
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/auth/bind")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + targetPlayer?.AccessToken)
                .WithJsonBody(new BindRequest { GuestToken = RecentAccount.Player.AccessToken });

            var response = await request.Send<PlayerToken>();

            _accountContainer.UpdateRecentAccount(response);

            SetEventProperties(response);

            SendEvent("account_bound");

            if (RecentAccount.Credential?.Provider == "email")
            {
                SendEvent("account_bound_by_email");
            }
            else if (RecentAccount.Credential?.Provider != "device_id")
            {
                SendEvent("account_bound_by_sso");
            }

            return _accountContainer.RecentAccount;
        }

        /// <summary>
        /// Logs out the current user by switching back to a guest account.
        /// </summary>
        /// <returns>The new guest user bundle.</returns>
        public async UniTask<UserBundle> LogoutAsync()
        {
            return await LoginAsGuestAsync(); // will always back to guest
        }

        /// <summary>
        /// Retrieves the current user's profile from the server.
        /// </summary>
        /// <returns>The user profile data.</returns>
        /// <exception cref="NoctuaException">Thrown when the access token is missing.</exception>
        public async UniTask<User> GetUserAsync()
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw NoctuaException.MissingAccessToken;
            }

            var request = new HttpRequest(HttpMethod.Get, $"{_baseUrl}/user/profile")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            return await request.Send<User>();
        }

        /// <summary>
        /// Updates the user's profile (language, country, currency) on the server and refreshes the token.
        /// </summary>
        /// <param name="updateUserRequest">The profile fields to update.</param>
        /// <exception cref="NoctuaException">Thrown when the access token is missing.</exception>
        public async UniTask UpdateUserAsync(UpdateUserRequest updateUserRequest)
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw NoctuaException.MissingAccessToken;
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/user/profile")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken)
                .WithJsonBody(updateUserRequest);

            _ = await request.Send<object>();

            await ExchangeTokenAsync(RecentAccount.Player.AccessToken);
            // Update the user language in player prefs
            // so user does not have to restart twice to load the translation
            _locale?.SetUserPrefsLanguage(updateUserRequest.Language);

            SendEvent(
                "profile_updated",
                new()
                {
                    { "new_country", updateUserRequest.Country },
                    { "new_currency", updateUserRequest.Currency },
                    { "new_language", updateUserRequest.Language },
                }
            );

        }

        /// <summary>
        /// Authenticates the user and returns the user bundle.
        /// 
        /// After authentication, it will show a welcome toast for the user.
        /// 
        /// Returns the authenticated user bundle.
        /// </summary>
        /// <returns>A UserBundle object representing the selected account.</returns>
        public async UniTask<UserBundle> AuthenticateAsync()
        {
            if (IsAuthenticated)
            {
                SetEventProperties(RecentAccount);
                SendEvent("account_detected");

                return RecentAccount;
            }

            // 3.b and 4.a.i.2: Invalid data will not be loaded

            _accountContainer.Load();

            // 2.a: If there is no account, login as guest

            if (_accountContainer.Accounts.Count == 0)
            {
                await LoginAsGuestAsync();

                SetEventProperties(RecentAccount);
                SendEvent("account_detected");

                return RecentAccount;
            }

            var firstUser = _accountContainer.Accounts.First();

            // Recent accounts are accounts that have played the game, they always match this game
            // 3.a: If there is already a recent account, reuse token

            if (firstUser.Player != null)
            {
                await ExchangeTokenAsync(firstUser.Player.AccessToken);

                SetEventProperties(RecentAccount);
                SendEvent("account_detected");

                return RecentAccount;
            }

            // Non recent accounts are accounts that have not played the game, they always don't match this game

            var firstPlayer = firstUser.PlayerAccounts.FirstOrDefault();

            // This isn't supposed to happen, because a user will at least have a player account

            if (firstPlayer == null)
            {
                // Disabled for production to reduce noise
                // SendEvent("no_player_account_found");
                throw new NoctuaException(NoctuaErrorCode.Authentication, "No player account found");
            }

            // 4.a.i.2, 4.a.ii.1.b: If there is no recent account, exchange token 

            await ExchangeTokenAsync(firstPlayer.AccessToken);

            SetEventProperties(RecentAccount);
            SendEvent("account_detected");

            return RecentAccount;
        }

        /// <summary>
        /// Switches the active account to a different user from the account list.
        /// </summary>
        /// <param name="user">The user bundle to switch to (must exist in the account list).</param>
        /// <exception cref="NoctuaException">Thrown when the user is not found in the account list.</exception>
        public async UniTask SwitchAccountAsync(UserBundle user)
        {
            var targetUser = AccountList.FirstOrDefault(x => x.User.Id == user.User.Id);

            if (targetUser == null)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, $"User {user.User.Id} not found in account list");
            }

            await ExchangeTokenAsync(user.PlayerAccounts.First().AccessToken);

            SetEventProperties(_accountContainer.RecentAccount);
            SendEvent("account_switched");
        }

        /// <summary>
        /// Clears all stored accounts for the current game from native storage.
        /// </summary>
        public void ResetAccounts()
        {
            _accountContainer.ResetAccounts();
        }

        /// <summary>
        /// Syncs in-game player account data (username, server, role) to the server.
        /// </summary>
        /// <param name="playerAccountData">The player account data to sync.</param>
        public async UniTask UpdatePlayerAccountAsync(PlayerAccountData playerAccountData)
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/players/sync")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken)
                .WithJsonBody(playerAccountData);

            _ = await request.Send<object>();

            SendEvent(
                "role_updated",
                new()
                {
                    { "ingame_username", playerAccountData.IngameUsername },
                    { "ingame_server_id", playerAccountData.IngameServerId },
                    { "ingame_role_id", playerAccountData.IngameRoleId }
                }
            );
        }

        /// <summary>
        /// Permanently deletes the current player account from the server and local storage.
        /// </summary>
        public async UniTask DeletePlayerAccountAsync()
        {
            var currentPlayer = RecentAccount.Player;

            var request = new HttpRequest(HttpMethod.Delete, $"{_baseUrl}/players/destroy")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            _ = await request.Send<DeletePlayerAccountResponse>();

            _accountContainer.DeleteRecentAccount();

            SendEvent("account_deleted");

            OnAccountDeleted?.Invoke(currentPlayer);
        }

        /// <summary>
        /// Uploads a file (e.g., profile image) to the server and returns the resulting URL.
        /// </summary>
        /// <param name="filePath">Local file path to upload.</param>
        /// <returns>The URL of the uploaded file.</returns>
        /// <exception cref="NoctuaException">Thrown when the access token is missing.</exception>
        /// <exception cref="Exception">Thrown when the file does not exist.</exception>
        public async UniTask<string> FileUploader(string filePath)
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw NoctuaException.MissingAccessToken;
            }

            if (!System.IO.File.Exists(filePath))
            {
                throw new Exception($"File not found at {filePath}");
            }

            byte[] fileData = System.IO.File.ReadAllBytes(filePath);

            var request = new HttpRequest(HttpMethod.Post, $"{_baseUrl}/user/profile-image")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken)
                .WithRawBody(fileData);

            string response = await request.SendRaw();
            Newtonsoft.Json.Linq.JObject jObject = Newtonsoft.Json.Linq.JObject.Parse(response);
            string fileUrl = jObject["data"]?["url"]?.ToString();

            return fileUrl;
        }

        /// <summary>
        /// Retrieves available profile options (e.g., selectable countries, currencies) from the server.
        /// </summary>
        /// <returns>Profile option data for use in profile editing UI.</returns>
        /// <exception cref="NoctuaException">Thrown when the access token is missing.</exception>
        public async UniTask<ProfileOptionData> GetProfileOptions()
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw NoctuaException.MissingAccessToken;
            }

            var request = new HttpRequest(HttpMethod.Get, $"{_baseUrl}/user/profile-options")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            return await request.Send<ProfileOptionData>();
        }

        /// <summary>
        /// Saves a key-value pair to the cloud save storage for the current player.
        /// </summary>
        /// <param name="key">The slot key to store data under.</param>
        /// <param name="value">The data value to store.</param>
        /// <exception cref="NoctuaException">Thrown when the access token is missing.</exception>
        public async UniTask SaveGameStateAsync(string key, string value)
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw NoctuaException.MissingAccessToken;
            }

            var request = new HttpRequest(HttpMethod.Put, $"{_baseUrl}/cloud-saves/{Uri.EscapeDataString(key)}")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken)
                .WithRawBody(System.Text.Encoding.UTF8.GetBytes(value));

            await request.Send<CloudSaveMetadata>();
        }

        /// <summary>
        /// Loads a previously saved value from cloud save storage by key.
        /// </summary>
        /// <param name="key">The slot key to retrieve data for.</param>
        /// <returns>The raw string value stored under the given key.</returns>
        /// <exception cref="NoctuaException">Thrown when the access token is missing.</exception>
        public async UniTask<string> LoadGameStateAsync(string key)
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw NoctuaException.MissingAccessToken;
            }

            var request = new HttpRequest(HttpMethod.Get, $"{_baseUrl}/cloud-saves/{Uri.EscapeDataString(key)}")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            return await request.SendRaw();
        }

        /// <summary>
        /// Gets all cloud save slot keys available for the current player.
        /// </summary>
        /// <returns>A list of slot key strings.</returns>
        /// <exception cref="NoctuaException">Thrown when the access token is missing.</exception>
        public async UniTask<List<string>> GetGameStateKeysAsync()
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw NoctuaException.MissingAccessToken;
            }

            var request = new HttpRequest(HttpMethod.Get, $"{_baseUrl}/cloud-saves")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            var response = await request.Send<CloudSaveListResponse>();

            return response.Saves?.Select(s => s.SlotKey).ToList() ?? new List<string>();
        }

        /// <summary>
        /// Deletes a cloud save slot by key for the current player.
        /// </summary>
        /// <param name="key">The slot key to delete.</param>
        /// <exception cref="NoctuaException">Thrown when the access token is missing.</exception>
        public async UniTask DeleteGameStateAsync(string key)
        {
            if (string.IsNullOrEmpty(RecentAccount?.Player?.AccessToken))
            {
                throw NoctuaException.MissingAccessToken;
            }

            var request = new HttpRequest(HttpMethod.Delete, $"{_baseUrl}/cloud-saves/{Uri.EscapeDataString(key)}")
                .WithHeader("X-CLIENT-ID", _clientId)
                .WithHeader("X-BUNDLE-ID", _bundleId)
                .WithHeader("Authorization", "Bearer " + RecentAccount.Player.AccessToken);

            await request.Send<object>();
        }

        private void SetEventProperties(UserBundle newUser)
        {
            _eventSender?.SetProperties(
                userId: newUser.User?.Id,
                playerId: newUser.Player?.Id,
                credentialId: newUser.Credential?.Id,
                credentialProvider: newUser.Credential?.Provider,
                gameId: newUser.Player?.GameId,
                gamePlatformId: newUser.Player?.GamePlatformId
            );
        }

        private void SetEventProperties(PlayerToken newUser)
        {
            _eventSender?.SetProperties(
                userId: newUser.User?.Id,
                playerId: newUser.Player?.Id,
                credentialId: newUser.Credential?.Id,
                credentialProvider: newUser.Credential?.Provider,
                gameId: newUser.Player?.GameId,
                gamePlatformId: newUser.Player?.GamePlatformId
            );
        }

        private void SendEvent(string eventName, Dictionary<string, IConvertible> data = null)
        {
            _eventSender?.Send(eventName, data ?? new Dictionary<string, IConvertible>());
        }

        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
        }
    }
}
