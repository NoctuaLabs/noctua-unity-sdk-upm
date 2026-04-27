using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using com.noctuagames.sdk.Events;
using com.noctuagames.sdk.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Scripting;
using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Central controller for all authentication-related UI flows.
    /// Coordinates navigation between login, registration, account selection, user center, and purchase dialogs.
    /// Allows UI presenters to call each other and provides the model layer access to UI presenters.
    /// </summary>
    internal class AuthUIController
    {
        private readonly ILogger _log = new NoctuaLogger();
        private readonly UIFactory _uiFactory;
        
        private readonly LogoutConfirmDialogPresenter _logoutConfirmDialog;
        private readonly AccountSelectionDialogPresenter _accountSelectionDialog;
        private readonly SwitchAccountConfirmationDialogPresenter _switchAccountConfirmationDialog;
        private readonly LoginOptionsDialogPresenter _loginOptionsDialog;
        private readonly EmailLoginDialogPresenter _emailLoginDialog;
        private readonly EmailRegisterDialogPresenter _emailRegisterDialog;
        private readonly EmailRegisterVNDialogPresenter _emailRegisterVNDialog;
        private readonly EmailVerificationDialogPresenter _emailVerificationDialog;
        private readonly PhoneVerificationDialogPresenter _phoneVerificationDialog;
        private readonly EmailResetPasswordDialogPresenter _emailResetPasswordDialog;
        private readonly EmailConfirmResetPasswordDialogPresenter _emailConfirmResetPasswordDialog;
        private readonly UserCenterPresenter _userCenter;
        private readonly AccountDeletionConfirmationDialogPresenter _accountDeletionConfirmationDialog;
        private readonly BindConfirmationDialogPresenter _bindConfirmation;
        private readonly BindConflictDialogPresenter _bindConflictDialog;
        private readonly PendingPurchasesDialogPresenter _pendingPurchasesDialog;
        private readonly PurchaseHistoryDialogPresenter _purchaseHistoryDialog;
        private readonly List<PurchaseItem> _pendingPurchases = new();
        private readonly List<PurchaseItem> _purchaseHistory = new();
        private readonly WelcomeNotificationPresenter _welcome;

        private readonly NoctuaAuthenticationService _authService;
        private readonly NoctuaIAPService _iapService;
        private readonly SocialAuthenticationService _socialAuth;
        private readonly NoctuaLocale _locale;
        private readonly GlobalConfig _config;
        private GameObject _socialAuthObject;
        
        private readonly Stack<Action> _navigationStack = new();

        /// <summary>Gets the authentication service used by this controller.</summary>
        public NoctuaAuthenticationService AuthService => _authService;

        /// <summary>Gets or sets the current authentication intention (none, switch, or link).</summary>
        public AuthIntention AuthIntention { get; set; } = AuthIntention.None;

        /// <summary>Event raised when the active account changes after login, switch, or logout.</summary>
        internal event Action<UserBundle> OnAccountChanged;


        internal AuthUIController(
            UIFactory uiFactory, 
            NoctuaAuthenticationService authService, 
            NoctuaIAPService iapService,
            GlobalConfig config,
            IEventSender eventSender = null,
            NoctuaLocale locale = null
        )
        {
            _uiFactory = uiFactory;

            if (locale != null)
            {
                _locale = locale;
            }

            if (iapService != null)
            {
                _iapService = iapService;
            }

            _config = config;
            
            _userCenter = _uiFactory.Create<UserCenterPresenter, AuthUIController>(this);
            _userCenter.EventSender = eventSender;
            _logoutConfirmDialog = _uiFactory.Create<LogoutConfirmDialogPresenter, AuthUIController>(this);
            _accountSelectionDialog = _uiFactory.Create<AccountSelectionDialogPresenter, AuthUIController>(this);
            _pendingPurchasesDialog = _uiFactory.Create<PendingPurchasesDialogPresenter, AuthUIController>(this);
            _purchaseHistoryDialog = _uiFactory.Create<PurchaseHistoryDialogPresenter, AuthUIController>(this);
            _switchAccountConfirmationDialog = _uiFactory.Create<SwitchAccountConfirmationDialogPresenter, AuthUIController>(this);
            _loginOptionsDialog = _uiFactory.Create<LoginOptionsDialogPresenter, AuthUIController>(this);
            _emailLoginDialog = _uiFactory.Create<EmailLoginDialogPresenter, AuthUIController>(this);
            _emailVerificationDialog = _uiFactory.Create<EmailVerificationDialogPresenter, AuthUIController>(this);
            _phoneVerificationDialog = _uiFactory.Create<PhoneVerificationDialogPresenter, AuthUIController>(this);
            _emailRegisterDialog = _uiFactory.Create<EmailRegisterDialogPresenter, AuthUIController>(this);
            _emailRegisterVNDialog = _uiFactory.Create<EmailRegisterVNDialogPresenter, AuthUIController>(this);
            _emailResetPasswordDialog = _uiFactory.Create<EmailResetPasswordDialogPresenter, AuthUIController>(this);
            _emailResetPasswordDialog.EventSender = eventSender;
            _emailConfirmResetPasswordDialog = _uiFactory.Create<EmailConfirmResetPasswordDialogPresenter, AuthUIController>(this);
            _emailConfirmResetPasswordDialog.EventSender = eventSender;
            _accountDeletionConfirmationDialog = _uiFactory.Create<AccountDeletionConfirmationDialogPresenter, AuthUIController>(this);
            _bindConfirmation = _uiFactory.Create<BindConfirmationDialogPresenter, AuthUIController>(this);
            _bindConflictDialog = _uiFactory.Create<BindConflictDialogPresenter, AuthUIController>(this);
            _welcome = _uiFactory.Create<WelcomeNotificationPresenter, AuthUIController>(this);

            _welcome.SetBehaviourWhitelabel(config);
            _emailLoginDialog.SetBehaviourWhitelabel(config);
            _emailRegisterDialog.SetBehaviourWhitelabel(config);
            _emailRegisterVNDialog.SetBehaviourWhitelabel(config);
            _accountSelectionDialog.SetWhitelabel(config);
            _loginOptionsDialog.SetWhitelabel(config);
            _userCenter.SetWhitelabel(config);
            
            _authService = authService;
            _authService.OnAccountChanged += OnAccountChanged;
            _socialAuth = new SocialAuthenticationService(_authService, config);
        }

        /// <summary>
        /// Applies white-label branding configuration to all auth UI presenters.
        /// </summary>
        /// <param name="config">The global configuration containing co-publisher branding.</param>
        public void SetBehaviourWhitelabel(GlobalConfig config)
        {
            _welcome.SetBehaviourWhitelabel(config);
            _emailLoginDialog.SetBehaviourWhitelabel(config);
            _emailRegisterDialog.SetBehaviourWhitelabel(config);
            _accountSelectionDialog.SetWhitelabel(config);
            _loginOptionsDialog.SetWhitelabel(config);
            _userCenter.SetWhitelabel(config);
        }

        
        /// <summary>
        /// Pushes a navigation action onto the back-navigation stack.
        /// </summary>
        /// <param name="action">The action to invoke when navigating back.</param>
        internal void PushNavigation(Action action)
        {
            if (action == null) return;
            
            _navigationStack.Push(action);
        }
        
        /// <summary>
        /// Clears all entries from the back-navigation stack.
        /// </summary>
        internal void ClearNavigation()
        {
            _navigationStack.Clear();
        }
        
        /// <summary>
        /// Pops and invokes the most recent back-navigation action from the stack.
        /// </summary>
        internal void NavigateBack()
        {
            if (_navigationStack.Count > 0)
            {
                _navigationStack.Pop().Invoke();
            }
        }

        /// <summary>
        /// Shows a retry dialog for offline mode; if retried, re-opens account selection.
        /// </summary>
        /// <param name="offlineModeMessage">The offline mode error message to display.</param>
        public async Task HandleRetryAccountSelectionAsync(string offlineModeMessage) {
            bool isRetry = await _uiFactory.ShowRetryDialog(offlineModeMessage, "offlineMode");
            if(isRetry)
            {
                ShowAccountSelection();
            }
        }

        /// <summary>
        /// Opens the account selection dialog showing all available game and Noctua accounts.
        /// </summary>
        public void ShowAccountSelection()
        {
            _accountSelectionDialog.Show();
        }

        public void ShowEmailRegistration(bool clearForm, bool isRegisterOnly = false)
        {
            var isVNLegalPurposeEnabled = _config.Noctua.RemoteFeatureFlags?["vnLegalPurposeEnabled"] ?? false;

            _log.Debug($"IsVNLegalPurposeEnabled: {isVNLegalPurposeEnabled}");

            if (isVNLegalPurposeEnabled)
            {
                _emailRegisterVNDialog.Show(clearForm, isRegisterOnly);
                _log.Debug("Showing EmailRegisterVNDialog");
                return;
            }

            _emailRegisterDialog.Show(clearForm, isRegisterOnly);
        }

        public void ShowEmailVerification(string email, string password, int verificationID, Dictionary<string, string> extraData)
        {
            _emailVerificationDialog.Show(email, password, verificationID, extraData);
        }

        public void ShowPhoneVerification(string verificationId, string phoneNumber, string emailAddress, string password, Dictionary<string, string> regExtra)
        {
            _phoneVerificationDialog.Show(verificationId, phoneNumber, emailAddress, password, regExtra);
        }

        public void ShowLoginOptions()
        {
            _loginOptionsDialog.Show();
        }

        public void ShowEmailLogin(Action<UserBundle> onLoginSuccess = null)
        {
            _emailLoginDialog.Show(onLoginSuccess);
        }

        public void ShowEmailResetPassword(bool clearForm)
        {
            _emailResetPasswordDialog.Show(clearForm);
        }

        public void ShowEmailConfirmResetPassword(int credVerifyId)
        {
            _emailConfirmResetPasswordDialog.Show(credVerifyId);
        }

        public void ShowSwitchAccountConfirmation(UserBundle recentAccount)
        {
            _switchAccountConfirmationDialog.Show(recentAccount);
        }

        public void ShowAccountDeletionConfirmation(UserBundle recentAccount)
        {
            _accountDeletionConfirmationDialog.Show(recentAccount);
        }

        public async Task HandleRetryUserCenterAsync(string offlineModeMessage) {
            bool isRetry = await _uiFactory.ShowRetryDialog(offlineModeMessage, "offlineMode");
            if(isRetry)
            {
                ShowUserCenter();
            }
        }
        
        public void ShowUserCenter()
        {
            _userCenter.Show();
        }
        
        public void ShowError(string message)
        {
            _uiFactory.ShowError(message);
        }
        
        public void ShowInfo(string message)
        {
            _uiFactory.ShowInfo(message);
        }

        public void ShowGeneralNotification(string message, bool isNotifSuccess = false, uint durationMs = 3000) 
        {
            _uiFactory.ShowGeneralNotification(message, isNotifSuccess, durationMs);
        }

        public void ShowLoadingProgress(bool isShow)
        {
            _uiFactory.ShowLoadingProgress(isShow);
        }

        public void ShowLogoutConfirmation()
        {
            _logoutConfirmDialog.Show();
        }

        public async UniTask<bool> ShowBannedConfirmationDialog()
        {
            return await _uiFactory.ShowBannedConfirmationDialog();
        }

        public async UniTask<UserBundle> SocialLoginAsync(string provider)
        {
            return await _socialAuth.SocialLoginAsync(provider);
        }
        
        public async UniTask<PlayerToken> GetSocialLoginTokenAsync(string provider)
        {
            return await _socialAuth.GetSocialLoginTokenAsync(provider);
        }

        public async UniTask<Credential> SocialLinkAsync(string provider)
        {
            return await _socialAuth.SocialLinkAsync(provider);
        }

        public void ShowBindConfirmation(PlayerToken playerToken)
        {
            _bindConfirmation.Show(playerToken);
        }
        
        public void ShowBindConflictDialog(PlayerToken playerToken)
        {
            _bindConflictDialog.Show(playerToken);
        }

        private List<PurchaseItem> GetPendingPurchases()
        {
            if (_iapService == null)
            {
                return _pendingPurchases;
            }

            var list = _iapService.GetPendingPurchases();
            _pendingPurchases.Clear();

            foreach (var item in list)
            {
                var status = item.Status;
                if (string.IsNullOrEmpty(status))
                {
                    status = "pending";
                }
                var purchaseItemName = item.OrderRequest.IngameItemName;
                if (string.IsNullOrEmpty(purchaseItemName))
                {
                    purchaseItemName = item.OrderRequest.ProductId;
                }

                _pendingPurchases.Add(new PurchaseItem
                {
                    OrderId = item.OrderId,
                    Timestamp = item.OrderRequest.Timestamp,
                    PaymentType = char.ToUpper(item.OrderRequest.PaymentType.ToString()[0]) + item.OrderRequest.PaymentType.ToString().Substring(1),
                    Status = status,
                    PurchaseItemName = purchaseItemName,
                    OrderRequest = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(item.OrderRequest))),
                    VerifyOrderRequest = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(item.VerifyOrderRequest))),
                    PlayerId = item.PlayerId,
                });
            }

            return _pendingPurchases;
        }

        public async void ShowPendingPurchasesDialog()
        {
            _pendingPurchasesDialog.Show(GetPendingPurchases());
        }

        public async UniTask<OrderStatus> RetryPendingPurchaseByOrderId(int orderId)
        {
            return await _iapService.RetryPendingPurchaseByOrderId(orderId);
        }

        private List<PurchaseItem> GetPurchaseHistory()
        {
            if (_iapService == null) {
                return _purchaseHistory;
            }

            var list =  _iapService.GetPurchaseHistory();
            _purchaseHistory.Clear();

            foreach (var item in list)
            {
                var status = item.Status;
                if (string.IsNullOrEmpty(status))
                {
                    status = "pending";
                }
                var purchaseItemName = item.OrderRequest.IngameItemName;
                if (string.IsNullOrEmpty(purchaseItemName))
                {
                    purchaseItemName = item.OrderRequest.ProductId;
                }

                _purchaseHistory.Add(new PurchaseItem{
                    OrderId = item.OrderId,
                    Timestamp = item.OrderRequest.Timestamp,
                    PaymentType = char.ToUpper(item.OrderRequest.PaymentType.ToString()[0]) + item.OrderRequest.PaymentType.ToString().Substring(1),
                    Status = status,
                    PurchaseItemName = purchaseItemName,
                    OrderRequest = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(item.OrderRequest))),
                    VerifyOrderRequest = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(item.VerifyOrderRequest))),
                    PlayerId = item.PlayerId,
                });
            }

            return _purchaseHistory;
        }

        public async void ShowPurchaseHistoryDialog()
        {
            _purchaseHistoryDialog.Show(GetPurchaseHistory());
        }

        public void SetFlag(Dictionary<string, bool> featureFlags)
        {
            _loginOptionsDialog.SetFlag(featureFlags);
            _userCenter.SetFlag(featureFlags);
        }
        
    }
    
    internal enum AuthIntention
    {
        None,
        Switch,
        Link,
    }
}