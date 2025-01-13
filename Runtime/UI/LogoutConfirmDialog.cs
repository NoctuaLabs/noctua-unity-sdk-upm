using System;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class LogoutConfirmDialogPresenter : Presenter<AuthenticationModel>
    {
        private readonly ILogger _log = new NoctuaLogger();
        public void Show()
        {
            Visible = true;

            View.Q<Label>("ErrCode").AddToClassList("hide");
            View.Q<VisualElement>("Spinner").AddToClassList("hide");
            View.Q<VisualElement>("ConfirmButton").RemoveFromClassList("hide");
        }

        protected override void Attach(){}
        protected override void Detach(){}
        
        private void Start()
        {
            View.Q<Button>("ConfirmButton").RegisterCallback<PointerUpEvent>(async _  =>
            {
                _log.Debug("clicking confirm button");

                if (View.Q<VisualElement>("Spinner").childCount == 0)
                {
                    View.Q<VisualElement>("Spinner").Add(new Spinner(30, 30));
                }

                View.Q<VisualElement>("Spinner").RemoveFromClassList("hide");

                View.Q<Label>("ErrCode").AddToClassList("hide");
                View.Q<VisualElement>("ConfirmButton").AddToClassList("hide");

                try
                {
                    await Model.AuthService.LogoutAsync();

                    View.Q<VisualElement>("Spinner").AddToClassList("hide");
                    View.Q<VisualElement>("ConfirmButton").RemoveFromClassList("hide");
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
                    View.Q<VisualElement>("ConfirmButton").RemoveFromClassList("hide");
                }
            });
            
            View.Q<Button>("CancelButton").RegisterCallback<PointerUpEvent>(_ =>
            {
                _log.Debug("clicking cancel button");
                
                Visible = false;
                Model.ShowUserCenter();
            });
        }
    }
}