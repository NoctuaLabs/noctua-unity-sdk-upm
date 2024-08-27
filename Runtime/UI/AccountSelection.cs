using System;
using UnityEngine;

/**/

namespace com.noctuagames.sdk.UI
{
    public class AccountSelection
    {
        public event Action<UserBundle> OnAccountSelected;
        public event Action<UserBundle> OnAccountSwitched;
        public event Action OnAccountSelectionRequested;
        public event Action OnLoginOptionsRequested;
        public event Action OnLoginWithEmailRequested;

        public UserBundle SelectedAccount { get; private set; }
        
        public readonly NoctuaAuthService AuthService ;
        
        public AccountSelection(NoctuaAuthService authService)
        {
            AuthService = authService;
        }
        
        public void SelectAccount(UserBundle user)
        {
            Debug.Log($"Selected account: {user.User.Id}");
            SelectedAccount = user;
            OnAccountSelected?.Invoke(user);
        }
        
        public void SwitchAccount(UserBundle user)
        {
            Debug.Log($"Switched account: {user.User.Id}");
            AuthService.SwitchAccount(user);
            OnAccountSwitched?.Invoke(user);
        }
        
        public void RequestLoginOptions()
        {
            Debug.Log("Requested login options");
            OnLoginOptionsRequested?.Invoke();
        }
        
        public void RequestLoginWithEmail()
        {
            Debug.Log("Requested login with email");
            OnLoginWithEmailRequested?.Invoke();
        }

        public void RequestAccountSelection()
        {
            Debug.Log("Requested account selection");
            OnAccountSelectionRequested?.Invoke();
        }
    }
}