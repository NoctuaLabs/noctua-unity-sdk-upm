using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace com.noctuagames.sdk.UI
{
    internal class PurchaseHistoryDialogPresenter : Presenter<AuthenticationModel>
    {
        private readonly ILogger _log = new NoctuaLogger();
        private readonly List<PurchaseItem> _purchaseHistory = new();

        private VisualTreeAsset _itemTemplate;
        private Button _btnComplete;
        private Button _btnCustomerService;
        private Button _btnClose;
        private Label _title;

        private ListView _purchaseHistoryListView;

        private UniTaskCompletionSource<bool> _tcs;

        protected override void Attach()
        {}

        protected override void Detach()
        {}

        private void Start()
        {
            _btnClose = View.Q<Button>("CustomPaymentExitButton");
            _btnClose.RegisterCallback<PointerUpEvent>(CloseDialog);
            _purchaseHistoryListView = View.Q<ListView>("PurchaseHistoryList");
            _title = View.Q<Label>("Title");

            _itemTemplate = Resources.Load<VisualTreeAsset>("PurchaseHistoryItem");
            
            BindListView(_purchaseHistoryListView, _purchaseHistory);
        }

        public async UniTask<bool> Show(List<PurchaseItem> purchaseHistory)
        {            
            _log.Debug("Player ID from recent account: " + Model.AuthService.RecentAccount?.Player?.Id);
            _tcs = new UniTaskCompletionSource<bool>();
            _purchaseHistory.Clear();
            _purchaseHistory.AddRange(
                purchaseHistory
                .Where(p =>
                (p is not null && (p.PlayerId is null || p.PlayerId == Model.AuthService.RecentAccount?.Player?.Id))
                )
                .OrderByDescending(p => p.OrderId)
            );
            _purchaseHistoryListView.Rebuild();

            _log.Debug("total pending purchases: " + _purchaseHistory.Count);
            
            _title.text = String.Format(Locale.GetTranslation(LocaleTextKey.IAPPurchaseHistoryTitle), _purchaseHistory.Count);

            Visible = true;

            return await _tcs.Task;
        }

        private void CloseDialog(PointerUpEvent evt)
        { 
            _log.Debug("On close dialog");

            Visible = false;

            _tcs?.TrySetResult(false);
        }

        private void BindListView(ListView listView, List<PurchaseItem> items)
        {
            listView.makeItem = _itemTemplate.Instantiate;
            listView.bindItem = (element, index) => BindListViewItem(element, index, items);
            listView.fixedItemHeight = 100;
            listView.itemsSource = items;
            listView.selectionType = SelectionType.Single;
        }

        private void BindListViewItem(VisualElement element, int index, List<PurchaseItem> items)
        {
            if (index >= items.Count)
            {
                return;
            }
            element.userData = items[index];

            // Assign value to elements
            var text = $"OrderID {items[index].OrderId.ToString()}";
            if (items[index].Timestamp != "")
            {
                text += $" - {items[index].Timestamp}";
            }
            element.Q<Label>("OrderId").text = text;
            element.Q<Label>("PaymentDetail").text = $"{items[index].PaymentType} - {items[index].PurchaseItemName}";

            element.Q<Label>("Status").text = "completed";
            switch (items[index].Status) {
                default:
                    element.Q<Label>("Status").AddToClassList("status-label-completed");
                    break;
            }
        }
    }
}
