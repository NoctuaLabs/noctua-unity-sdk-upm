using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace com.noctuagames.sdk.UI
{
    internal class PendingPurchasesDialogPresenter : Presenter<AuthenticationModel>
    {
        private VisualTreeAsset _itemTemplate;
        private Button _btnComplete;
        private Button _btnCustomerService;
        private Button _btnClose;
        private Button _btnNextPage;
        private Button _btnPrevPage;
        private Label _message;

        private int _page = 1;

        private List<PendingPurchaseItem> _pendingPurchases = new List<PendingPurchaseItem>();

        private ListView _pendingPurchasesListView;

        private readonly ILogger _log = new NoctuaLogger(typeof(PendingPurchasesDialogPresenter));

        private UniTaskCompletionSource<bool> _tcs;

        protected override void Attach()
        {}

        protected override void Detach()
        {}

        private void Start()
        {
            _btnClose = View.Q<Button>("CustomPaymentExitButton");
            _btnClose.RegisterCallback<PointerUpEvent>(CloseDialog);

            _btnNextPage = View.Q<Button>("NextButton");
            _btnNextPage.RegisterCallback<PointerUpEvent>(NextPage);

            _btnPrevPage = View.Q<Button>("PrevButton");
            _btnPrevPage.RegisterCallback<PointerUpEvent>(PrevPage);

            _itemTemplate = Resources.Load<VisualTreeAsset>("PendingPurchaseItem");
        }

        public async UniTask<bool> Show(List<PendingPurchaseItem> pendingPurchases)
        {            
            _tcs = new UniTaskCompletionSource<bool>();

            _pendingPurchases = pendingPurchases;
            _pendingPurchases.Reverse();
            _pendingPurchasesListView = View.Q<ListView>("PendingPurchasesList");
            _pendingPurchasesListView.Rebuild();

            if (_pendingPurchases.Count == 0)
            {
                View.Q<Label>("Title").text = "No pending purchase at the moment";
            } else if (_pendingPurchases.Count == 1) {
                View.Q<Label>("Title").text = "Your Pending Purchase";
            } else {
                View.Q<Label>("Title").text = "Your Pending Purchases";
            }


            var currentPageContent = new List<PendingPurchaseItem>();
            var count = 0;
            var limit = 5;
            if (_pendingPurchases.Count <= 5)
            {
                View.Q<VisualElement>("NavigationButtonsSpacer").RemoveFromClassList("hide");
                View.Q<VisualElement>("NavigationButtons").AddToClassList("hide");
            } else if (_pendingPurchases.Count > 5)
            {
                View.Q<Button>("PrevButton").AddToClassList("hide");
                View.Q<VisualElement>("NavigationButtonsSpacer").AddToClassList("hide");
                View.Q<VisualElement>("NavigationButtons").RemoveFromClassList("hide");
            }
            foreach (var item in _pendingPurchases)
            {
                count++;
                if (count > limit) {
                    break;
                }
                currentPageContent.Add(item);
            }

            BindListView(_pendingPurchasesListView, currentPageContent);

            Visible = true;

            return await _tcs.Task;
        }

        private void NextPage(PointerUpEvent evt)
        {
            var currentPage = _page + 1;
            NavigatePage(currentPage);

        }
        private void PrevPage(PointerUpEvent evt)
        {
            var currentPage = _page - 1;
            NavigatePage(currentPage);
        }

        private void NavigatePage(int page)
        {

            var limit = 5;
            var total = _pendingPurchases.Count;
            var offset = ((page - 1) * limit);


            var currentPageContent = new List<PendingPurchaseItem>();
            var count = 0;
            var index = 0;
            foreach (var item in _pendingPurchases)
            {
                count++;
                index++;
                if (index <= offset) {
                    continue;
                }
                count = count - offset;
                if (count > limit) {
                    break;
                }
                if (currentPageContent.Count >= limit)
                {
                    break;
                }
                currentPageContent.Add(item);
            }

            var showPrevPageButton = page > 1;
            var showNextPageButton = false;
            if (currentPageContent.Count < limit)
            {
                showNextPageButton = false;
            }
            if (((total - (page * limit) - (limit - currentPageContent.Count))) > 0)
            {
                showNextPageButton = true;
            }
            if (page == 1 && total > limit)
            {
                showNextPageButton = true;
            }


            if (currentPageContent.Count != 0)
            {
                _pendingPurchasesListView = View.Q<ListView>("PendingPurchasesList");
                _pendingPurchasesListView.Clear();
                _pendingPurchasesListView.Rebuild();
                BindListView(_pendingPurchasesListView, currentPageContent);

                _page = page;

                if (showPrevPageButton && !showNextPageButton)
                {
                    _btnNextPage.AddToClassList("hide");
                    _btnPrevPage.RemoveFromClassList("hide");
                }
                else if (!showPrevPageButton && showNextPageButton)
                {
                    _btnPrevPage.AddToClassList("hide");
                    _btnNextPage.RemoveFromClassList("hide");
                }
                else if (showNextPageButton && showPrevPageButton)
                {
                    _btnPrevPage.RemoveFromClassList("hide");
                    _btnNextPage.RemoveFromClassList("hide");
                } else {
                    _btnPrevPage.AddToClassList("hide");
                    _btnNextPage.AddToClassList("hide");
                }
            }
        }


        private void PendingPurchasesDialog(PointerUpEvent evt)
        {            
            Visible = false;

            _tcs?.TrySetResult(true);
        }

        private async void OpenCS(PointerUpEvent evt)
        {
            _log.Debug("clicking customer Service button");
            
            Visible = false;

            try
            {
                await Noctua.Platform.Content.ShowCustomerService();
            } 
            catch (Exception e) {
                _tcs?.TrySetResult(false);

                if (e is NoctuaException noctuaEx)
                {
                    _log.Error("NoctuaException: " + noctuaEx.ErrorCode + " : " + noctuaEx.Message);
                } else {
                    _log.Error("Exception: " + e);
                }
            }

            Visible = true;
        }

        private void CloseDialog(PointerUpEvent evt)
        { 
            _log.Debug("On close dialog");

            Visible = false;

            _tcs?.TrySetResult(false);
        }

        private void BindListView(ListView listView, List<PendingPurchaseItem> items)
        {
            listView.makeItem = _itemTemplate.Instantiate;
            listView.bindItem = (element, index) => BindListViewItem(element, index, items);
            listView.fixedItemHeight = 40;
            listView.itemsSource = items;
            listView.selectionType = SelectionType.Single;
        }

        private void BindListViewItem(VisualElement element, int index, List<PendingPurchaseItem> items)
        {
            if (index >= items.Count)
            {
                return;
            }
            element.userData = items[index];

            var fullReceiptData = JsonConvert.SerializeObject(items[index]);
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(fullReceiptData);
            var textToCopy = Convert.ToBase64String(plainTextBytes);
            element.RegisterCallback<PointerUpEvent>(evt =>
            {

                Model.ShowGeneralNotification(
                "Your purchase receipt has been copied to clipboard.",
                    true,
                    7000
                );
                GUIUtility.systemCopyBuffer = textToCopy;
            });

            var text = $"Oder ID {items[index].OrderId.ToString()}";
            if (items[index].Timestamp != "")
            {
                text += $" - {items[index].Timestamp}";
            }
            element.Q<Label>("OrderId").text = text;
        }
    }
}
