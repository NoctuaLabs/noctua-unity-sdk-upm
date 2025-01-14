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

        [Preserve]
        public class PendingPurchaseItem
        {
            public int OrderId;
            public string PaymentType;
            public string Status;
            public string PurchaseItemName;
            public string Timestamp;
            public string OrderRequest;
            public string VerifyOrderRequest;
            public long? PlayerId;
        }
    /*
    We were using Model-View-Presenter, further reading:
    - https://en.wikipedia.org/wiki/Model%E2%80%93view%E2%80%93presenter
    - https://medium.com/cr8resume/make-you-hand-dirty-with-mvp-model-view-presenter-eab5b5c16e42
    - https://www.baeldung.com/mvc-vs-mvp-pattern

    But in our case, we have unique conditions:
    1. Our model (*Services.cs) is the public facing API
    2. Our public facing API need to cover both UI and non-UI stuff.

    Thus, we have to tweak the pattern to API-Model-View-Presenter
    1. UI:
    1. Presenter: where we control the state of the UI, but not the main logics
    2. Model: main logics + public facing API
    3. API: the actual model, where we talk to either HTTP API or local storage like player prefs. (TODO)

    AuthenticationModel purposes:
    1. To allow our SDK instance (including UI) to be injected into the Scene
    3. To allow an UI presenter call another UI presenter
    2. To allow model layer (logic) to call an UI presenter
    */

    internal class AuthenticationModel
    {
        private readonly ILogger _log = new NoctuaLogger();
        private readonly UIFactory _uiFactory;
        
        private readonly AccountSelectionDialogPresenter _accountSelectionDialog;
        private readonly SwitchAccountConfirmationDialogPresenter _switchAccountConfirmationDialog;
        private readonly LoginOptionsDialogPresenter _loginOptionsDialog;
        private readonly EmailLoginDialogPresenter _emailLoginDialog;
        private readonly EmailRegisterDialogPresenter _emailRegisterDialog;
        private readonly EmailVerificationDialogPresenter _emailVerificationDialog;
        private readonly EmailResetPasswordDialogPresenter _emailResetPasswordDialog;
        private readonly EmailConfirmResetPasswordDialogPresenter _emailConfirmResetPasswordDialog;
        private readonly UserCenterPresenter _userCenter;
        private readonly AccountDeletionConfirmationDialogPresenter _accountDeletionConfirmationDialog;
        private readonly BindConfirmationDialogPresenter _bindConfirmation;
        private readonly BindConflictDialogPresenter _bindConflictDialog;
        private readonly PendingPurchasesDialogPresenter _pendingPurchasesDialog;
        private readonly List<PendingPurchaseItem> _pendingPurchases = new();
        private readonly WelcomeNotificationPresenter _welcome;

        private readonly NoctuaAuthenticationService _authService;
        private readonly NoctuaIAPService _iapService;
        private readonly SocialAuthenticationService _socialAuth;
        private readonly NoctuaLocale _locale;
        private GameObject _socialAuthObject;
        
        private readonly Stack<Action> _navigationStack = new();

        public NoctuaAuthenticationService AuthService => _authService;
        public AuthIntention AuthIntention { get; set; } = AuthIntention.None;

        internal event Action<UserBundle> OnAccountChanged;


        internal AuthenticationModel(
            UIFactory uiFactory, 
            NoctuaAuthenticationService authService, 
            NoctuaIAPService iapService,
            GlobalConfig config,
            EventSender eventSender = null,
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
            
            _userCenter = _uiFactory.Create<UserCenterPresenter, AuthenticationModel>(this);
            _userCenter.EventSender = eventSender;
            _accountSelectionDialog = _uiFactory.Create<AccountSelectionDialogPresenter, AuthenticationModel>(this);
            _pendingPurchasesDialog = _uiFactory.Create<PendingPurchasesDialogPresenter, AuthenticationModel>(this);
            _switchAccountConfirmationDialog = _uiFactory.Create<SwitchAccountConfirmationDialogPresenter, AuthenticationModel>(this);
            _loginOptionsDialog = _uiFactory.Create<LoginOptionsDialogPresenter, AuthenticationModel>(this);
            _emailLoginDialog = _uiFactory.Create<EmailLoginDialogPresenter, AuthenticationModel>(this);
            _emailVerificationDialog = _uiFactory.Create<EmailVerificationDialogPresenter, AuthenticationModel>(this);
            _emailRegisterDialog = _uiFactory.Create<EmailRegisterDialogPresenter, AuthenticationModel>(this);
            _emailResetPasswordDialog = _uiFactory.Create<EmailResetPasswordDialogPresenter, AuthenticationModel>(this);
            _emailResetPasswordDialog.EventSender = eventSender;
            _emailConfirmResetPasswordDialog = _uiFactory.Create<EmailConfirmResetPasswordDialogPresenter, AuthenticationModel>(this);
            _emailConfirmResetPasswordDialog.EventSender = eventSender;
            _accountDeletionConfirmationDialog = _uiFactory.Create<AccountDeletionConfirmationDialogPresenter, AuthenticationModel>(this);
            _bindConfirmation = _uiFactory.Create<BindConfirmationDialogPresenter, AuthenticationModel>(this);
            _bindConflictDialog = _uiFactory.Create<BindConflictDialogPresenter, AuthenticationModel>(this);
            _welcome = _uiFactory.Create<WelcomeNotificationPresenter, AuthenticationModel>(this);


            _welcome.SetBehaviourWhitelabel(config);
            _emailLoginDialog.SetBehaviourWhitelabel(config);
            _emailRegisterDialog.SetBehaviourWhitelabel(config);
            _accountSelectionDialog.SetWhitelabel(config);
            _loginOptionsDialog.SetWhitelabel(config);
            _userCenter.SetWhitelabel(config);
            
            _authService = authService;
            _authService.OnAccountChanged += OnAccountChanged;
            _socialAuth = new SocialAuthenticationService(_authService, config);
        }
        
        internal void PushNavigation(Action action)
        {
            if (action == null) return;
            
            _navigationStack.Push(action);
        }
        
        internal void ClearNavigation()
        {
            _navigationStack.Clear();
        }
        
        internal void NavigateBack()
        {
            if (_navigationStack.Count > 0)
            {
                _navigationStack.Pop().Invoke();
            }
        }

        public void ShowAccountSelection()
        {
            _accountSelectionDialog.Show();
        }

        public void ShowEmailRegistration(bool clearForm, bool isRegisterOnly = false)
        {
            _emailRegisterDialog.Show(clearForm, isRegisterOnly);
        }

        public void ShowEmailVerification(string email, string password, int verificationID, Dictionary<string,string> extraData)
        {
            _emailVerificationDialog.Show(email, password, verificationID, extraData);
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

        private List<PendingPurchaseItem> GetPendingPurchases()
        {
            if (_iapService == null) {
                return _pendingPurchases;
            }

            var list =  _iapService.GetPendingPurchases();
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

                _pendingPurchases.Add(new PendingPurchaseItem{
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

        public void SetFlag(bool SSODisabled = false)
        {
            _loginOptionsDialog.SetFlag(SSODisabled);
        }
        
    }
    
    internal enum AuthIntention
    {
        None,
        Switch,
        Link,
    }
}