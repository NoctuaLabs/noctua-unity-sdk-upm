using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Application = UnityEngine.Device.Application;
using SystemInfo = UnityEngine.Device.SystemInfo;
using Newtonsoft.Json;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using com.noctuagames.sdk.Events;
using com.noctuagames.sdk.UI;
using UnityEngine.EventSystems;
using UnityEngine.Scripting;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk
{
    
    /// <summary>
    /// Provides authentication functionality for the Noctua SDK, including guest login,
    /// email-based authentication, social login, and account management.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class acts as a high-level wrapper over <see cref="NoctuaAuthenticationService"/> to provide
    /// user authentication, registration, linking, and account switching.
    /// </para>
    /// <para>
    /// It also integrates with Noctua UI to display user-related interfaces such as
    /// login, registration, and user center screens.
    /// </para>
    /// </remarks>
    public class NoctuaAuthentication
    {
        public readonly List<string> SsoCloseWebViewKeywords = new() { "https://developers.google.com/identity/protocols/oauth2" };

        // AccountList will be synced data from AccountContainer.Accounts
        public IReadOnlyList<UserBundle> AccountList => _service.AccountList;

        public bool IsAuthenticated => _service.IsAuthenticated;

        public UserBundle RecentAccount => _service.RecentAccount;

        public event Action<UserBundle> OnAccountChanged
        {
            add => _service.OnAccountChanged += value;
            remove => _service.OnAccountChanged -= value;
        }
        
        public event Action<Player> OnAccountDeleted
        {
            add => _service.OnAccountDeleted += value;
            remove => _service.OnAccountDeleted -= value;
        }

        private readonly ILogger _log = new NoctuaLogger();
        private bool _enabled;
        private readonly Config _config;
        private readonly PanelSettings _panelSettings;
        private readonly UIFactory _uiFactory;
        private readonly AuthenticationModel _uiModel;
        private readonly NoctuaAuthenticationService _service;
        private readonly NoctuaIAPService _iapService;
        private OauthRedirectListener _oauthOauthRedirectListener;

        /// <summary>
        /// Internal constructor for Noctua authentication system.
        /// </summary>
        /// <param name="service">The core authentication service.</param>
        /// <param name="iapService">The in-app purchase service used for account verification.</param>
        /// <param name="uiFactory">UI factory for rendering authentication-related views.</param>
        /// <param name="config">Global configuration data from <c>noctuagg.json</c>.</param>
        /// <param name="eventSender">Optional event sender for analytics tracking.</param>
        /// <param name="locale">Optional locale object for translations and language handling.</param>
        internal NoctuaAuthentication(
            NoctuaAuthenticationService service,
            NoctuaIAPService iapService,
            UIFactory uiFactory,
            GlobalConfig config,
            EventSender eventSender = null,
            NoctuaLocale locale = null
        )
        {
            _service = service;
            _iapService = iapService;

            _uiFactory = uiFactory;
            _uiModel = new AuthenticationModel(_uiFactory, _service, _iapService, config, eventSender, locale);
        }

        /// <summary>
        /// Enables the authentication service after SDK initialization.
        /// </summary>
        internal void Enable()
        {
            _log.Debug("calling API");

            _enabled = true;
        }

        /// <summary>
        /// Ensures that the authentication service has been enabled.
        /// Throws an exception if it has not been initialized properly.
        /// </summary>
        private void EnsureEnabled()
        {
            if (_enabled) return;

            _log.Error("Noctua Authentication is not enabled due to initialization failure.");
            throw new NoctuaException(NoctuaErrorCode.Application, "Noctua Authentication is not enabled due to initialization failure.");
        }

        /// <summary>
        /// Gets the most recently used user account.
        /// </summary>
        public UserBundle GetRecentAccount()
        {
            return RecentAccount;
        }
        
        /// <summary>
        /// Returns the access token for the most recent account, if available.
        /// </summary>
        public string GetAccessToken()
        {
            return RecentAccount?.Player?.AccessToken;
        }

        /// <summary>
        /// Authenticates the user and returns the user bundle.
        /// 
        /// If the user bundle is not detected, it will show the account selection UI.
        /// 
        /// After authentication, it will show a welcome toast for the user.
        /// 
        /// Returns the authenticated user bundle.
        /// </summary>
        /// <returns>A UserBundle object representing the selected account.</returns>
        public async UniTask<UserBundle> AuthenticateAsync()
        {
            // Disabled for production to reduce event noise
            // Noctua.Event.InternalTrackEvent("sdk_auth_start");
            if (!_enabled)
            {
                // Disabled for production to reduce event noise
                // Noctua.Event.InternalTrackEvent("sdk_auth_not_enabled");
                return UserBundle.Empty;
            }

            _log.Debug("calling API");

            try
            {
                var userBundle = await _service.AuthenticateAsync();

                // Disabled for production to reduce event noise
                // Noctua.Event.InternalTrackEvent("sdk_auth_success");
                return userBundle;
            }
            catch (NoctuaException noctuaEx) when (noctuaEx.ErrorCode == (int)NoctuaErrorCode.UserBanned)
            {
                // Disabled for production to reduce event noise
                // Noctua.Event.InternalTrackEvent("sdk_auth_banned", new Dictionary<string, IConvertible>
                // {
                //     { "ban_reason", noctuaEx.Message }
                // });

                bool confirmed = await _uiFactory.ShowBannedConfirmationDialog();

                if (confirmed)
                {
                    throw;
                }

                throw new OperationCanceledException("Action canceled.");
            }
        }

        /// <summary>
        /// Logs in anonymously as a guest user.
        /// </summary>
        /// <returns>The created guest user account bundle.</returns>
        public async UniTask<UserBundle> LoginAsGuest()
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.LoginAsGuestAsync();
        }
        
        /// <summary>
        /// Resets all locally cached user accounts.
        /// </summary>
        public void ResetAccounts()
        {
            EnsureEnabled();

            _log.Debug("calling API");

            _service.ResetAccounts();
        }

        /// <summary>
        /// Registers a new account using email and password credentials.
        /// </summary>
        /// <param name="email">User email address.</param>
        /// <param name="password">Account password.</param>
        /// <param name="regExtra">Optional extra registration metadata.</param>
        /// <returns>A <see cref="CredentialVerification"/> object containing verification details.</returns>
        public async UniTask<CredentialVerification> RegisterWithEmailAsync(string email, string password, Dictionary<string, string> regExtra = null)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.RegisterWithEmailAsync(email, password, regExtra);
        }

        /// <summary>
        /// Verifies an email registration using verification code.
        /// </summary>
        public async UniTask<UserBundle> VerifyEmailRegistrationAsync(int id, string code)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.VerifyEmailRegistrationAsync(id, code);
        }

        /// <summary>
        /// Links the current account with an email and password.
        /// </summary>
        public async UniTask<CredentialVerification> LinkWithEmailAsync(string email, string password)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.LinkWithEmailAsync(email, password);
        }
        
        /// <summary>
        /// Verifies email linking using a verification code.
        /// </summary>
        public async UniTask<Credential> VerifyEmailLinkingAsync(int id, string code)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.VerifyEmailLinkingAsync(id, code);
        }

        /// <summary>
        /// Logs in using email and password credentials.
        /// </summary>
        public async UniTask<UserBundle> LoginWithEmailAsync(string email, string password)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.LoginWithEmailAsync(email, password);
        }

        /// <summary>
        /// Requests a password reset link via email.
        /// </summary>
        public async UniTask<CredentialVerification> RequestResetPasswordAsync(string email)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.RequestResetPasswordAsync(email);
        }

        /// <summary>
        /// Confirms a password reset using a code and new password.
        /// </summary>
        public async UniTask<PlayerToken> ConfirmResetPasswordAsync(int id, string code, string newPassword)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.ConfirmResetPasswordAsync(id, code, newPassword);
        }

        /// <summary>
        /// Switches the currently active account to another user.
        /// </summary>
        /// <param name="user">Target user bundle to switch to.</param>
        public void SwitchAccount(UserBundle user)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            UniTask.Void(async () => await _service.SwitchAccountAsync(user));
        }

        /// <summary>
        /// Exchanges an access token for a refreshed session.
        /// </summary>
        /// <param name="accessToken">Existing access token.</param>
        public async UniTask<UserBundle> ExchangeToken(string accessToken)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.ExchangeTokenAsync(accessToken);
        }

        /// <summary>
        /// Gets a redirect URL for initiating social login via an external provider.
        /// </summary>
        /// <param name="provider">Social provider (e.g., "google", "facebook").</param>
        public async UniTask<string> GetSocialLoginRedirectURL(string provider)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.GetSocialAuthRedirectURLAsync(provider);
        }

        /// <summary>
        /// Performs a social login using provider-specific payload data.
        /// </summary>
        public async UniTask<UserBundle> SocialLoginAsync(string provider, SocialLoginRequest payload)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.SocialLoginAsync(provider, payload);
        }

        /// <summary>
        /// Links an existing account with a social provider.
        /// </summary>
        public async UniTask<Credential> SocialLinkAsync(string provider, SocialLinkRequest payload)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.SocialLinkAsync(provider, payload);
        }

        /// <summary>
        /// Starts a social login flow through the UI.
        /// </summary>
        public async UniTask<UserBundle> SocialLoginAsync(string provider)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _uiModel.SocialLoginAsync(provider);
        }

        /// <summary>
        /// Updates player account data (e.g. nickname or avatar).
        /// </summary>
        public async UniTask UpdatePlayerAccountAsync(PlayerAccountData playerAccountData)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            await _service.UpdatePlayerAccountAsync(playerAccountData);
        }

        /// <summary>
        /// Logs out the current user and clears their session.
        /// </summary>
        public async UniTask<UserBundle> LogoutAsync()
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.LogoutAsync();
        }

        /// <summary>
        /// Displays a retry popup when network or offline mode errors occur.
        /// </summary>
        private async Task HandleRetryPopUpMessageAsync(string offlineModeMessage)
        {
            bool isRetry = await _uiFactory.ShowRetryDialog(offlineModeMessage, "offlineMode");
            if (isRetry)
            {
                await ShowUserCenter();
            }
        }

        /// <summary>
        /// Displays the Noctua user center, allowing players to manage their accounts and linked credentials.
        /// </summary>
        /// <exception cref="NoctuaException">Thrown if offline mode prevents access to user center.</exception>
        public async UniTask ShowUserCenter()
        {
            // Offline-first handler
            _uiFactory.ShowLoadingProgress(true);

            var offlineModeMessage = Noctua.Platform.Locale.GetTranslation(LocaleTextKey.OfflineModeMessage) + " [UserCenter]";
            var isOffline = await Noctua.IsOfflineAsync();

            if (!isOffline && !Noctua.IsInitialized())
            {
                try
                {
                    await Noctua.InitAsync();

                    await Noctua.Auth.AuthenticateAsync();

                }
                catch (Exception e)
                {
                    _uiFactory.ShowLoadingProgress(false);

                    await HandleRetryPopUpMessageAsync(offlineModeMessage);

                    throw new NoctuaException(NoctuaErrorCode.Authentication, $"{e.Message}");
                }
            }

            if (isOffline)
            {
                _uiFactory.ShowLoadingProgress(false);

                await HandleRetryPopUpMessageAsync(offlineModeMessage);

                throw new NoctuaException(NoctuaErrorCode.Authentication, offlineModeMessage);
            }

            _uiFactory.ShowLoadingProgress(false);

            EnsureEnabled();

            _log.Debug("calling API");

            _uiModel.ShowUserCenter();
        }
        
        /// <summary>
        /// Applies feature flags received from remote configuration to authentication UI.
        /// </summary>
        public void SetFlag(Dictionary<string, bool> featureFlags)
        {
            _uiModel.SetFlag(featureFlags);
        }

        /// <summary>
        /// Displays the account selection UI allowing the user to switch between available accounts.
        /// </summary>
        public void SwitchAccount()
        {
            EnsureEnabled();

            _log.Debug("calling API");

            _uiModel.ShowAccountSelection();
        }
        
        /// <summary>
        /// Internal configuration model for authentication service initialization.
        /// </summary>
        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
        }
    }
}
