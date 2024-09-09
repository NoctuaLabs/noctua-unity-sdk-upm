using System;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class AccountDeletionConfirmationDialogPresenter : Presenter<NoctuaAuthenticationBehaviour>
    {

        private UserBundle _recentAccount;

        public void Show(UserBundle obj)
        {
            _recentAccount = obj;
            Visible = true;

            View.Q<Label>("ErrCode").AddToClassList("hide");
            View.Q<VisualElement>("Spinner").AddToClassList("hide");
            View.Q<VisualElement>("ButtonGroup").RemoveFromClassList("hide");
        }

        protected override void Attach(){}
        protected override void Detach(){}
        
        private void Start()
        {
            View.Q<Button>("ConfirmButton").RegisterCallback<PointerUpEvent>(async _  =>
            {
                var spinner = new Spinner();
                View.Q<VisualElement>("Spinner").Clear();
                View.Q<VisualElement>("Spinner").Add(spinner);
                View.Q<VisualElement>("Spinner").RemoveFromClassList("hide");

                View.Q<Label>("ErrCode").AddToClassList("hide");
                View.Q<VisualElement>("ButtonGroup").AddToClassList("hide");

                try
                {
                    await Model.AuthService.DeletePlayerAccountAsync();
                    await Model.AuthService.LogoutAsync(); // This also login as guest

                    View.Q<VisualElement>("Spinner").AddToClassList("hide");
                    View.Q<VisualElement>("ButtonGroup").RemoveFromClassList("hide");
                    Visible = false;
                }
                catch (Exception e)
                {
                    if (e is NoctuaException noctuaEx)  
                    {
                        View.Q<Label>("ErrCode").RemoveFromClassList("hide");
                        View.Q<Label>("ErrCode").text = noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message;
                    }
                    else
                    {
                        View.Q<Label>("ErrCode").RemoveFromClassList("hide");
                        View.Q<Label>("ErrCode").text = e.Message;
                    }
                    
                    View.Q<VisualElement>("Spinner").AddToClassList("hide");
                    View.Q<VisualElement>("ButtonGroup").RemoveFromClassList("hide");
                }

            });
            
            View.Q<Button>("CancelButton").RegisterCallback<PointerUpEvent>(_ =>
            {
                Visible = false;
                Model.ShowUserCenter();
            });
        }
    }
}