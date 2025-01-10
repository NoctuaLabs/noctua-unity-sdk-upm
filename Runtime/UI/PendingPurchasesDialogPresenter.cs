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
    internal class PendingPurchasesDialogPresenter : Presenter<AuthenticationModel>
    {
        private readonly ILogger _log = new NoctuaLogger();
        private readonly List<PendingPurchaseItem> _pendingPurchases = new();

        private VisualTreeAsset _itemTemplate;
        private Button _btnComplete;
        private Button _btnCustomerService;
        private Button _btnClose;
        private Label _title;

        private ListView _pendingPurchasesListView;

        private UniTaskCompletionSource<bool> _tcs;

        protected override void Attach()
        {}

        protected override void Detach()
        {}

        private void Start()
        {
            _btnClose = View.Q<Button>("CustomPaymentExitButton");
            _btnClose.RegisterCallback<PointerUpEvent>(CloseDialog);
            _pendingPurchasesListView = View.Q<ListView>("PendingPurchasesList");
            _title = View.Q<Label>("Title");

            _itemTemplate = Resources.Load<VisualTreeAsset>("PendingPurchaseItem");
            
            BindListView(_pendingPurchasesListView, _pendingPurchases);
        }

        public async UniTask<bool> Show(List<PendingPurchaseItem> pendingPurchases)
        {            
            _log.Debug("Player ID from recent account: " + Model.AuthService.RecentAccount?.Player?.Id);
            _tcs = new UniTaskCompletionSource<bool>();
            _pendingPurchases.Clear();
            _pendingPurchases.AddRange(
                pendingPurchases
                .Where(p =>
                (p is not null && (p.PlayerId is null || p.PlayerId == Model.AuthService.RecentAccount?.Player?.Id))
                )
                .OrderByDescending(p => p.OrderId)
            );
            _pendingPurchasesListView.Rebuild();

            _log.Debug("total pending purchases: " + _pendingPurchases.Count);
            
            _title.text = String.Format(Locale.GetTranslation(LocaleTextKey.IAPPendingPurchaseTitle), _pendingPurchases.Count);

            Visible = true;

            return await _tcs.Task;
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
            listView.fixedItemHeight = 100;
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

            element.Q<Button>("CSButton").text = Locale.GetTranslation(LocaleTextKey.IAPPendingPurchaseItemCsButtonText);
            element.Q<Button>("CSButton").RegisterCallback<PointerUpEvent>(async evt =>
            {
                _log.Debug("clicking customer Service button");

                Visible = false;

                try
                {
                    await Noctua.Platform.Content.ShowCustomerService("pending_purchase", $"order_id_{items[index].OrderId}");
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
            });

            element.Q<Button>("CopyButton").text = Locale.GetTranslation(LocaleTextKey.IAPPendingPurchaseItemCopyButtonText);
            element.Q<Button>("CopyButton").RegisterCallback<PointerUpEvent>(evt =>
            {

                Model.ShowGeneralNotification(
                    Locale.GetTranslation(LocaleTextKey.IAPPendingPurchaseReceiptCopied),
                    true,
                    7000
                );
                GUIUtility.systemCopyBuffer = textToCopy;
            });

            element.Q<Button>("RetryButton").text = Locale.GetTranslation(LocaleTextKey.IAPPendingPurchaseItemRetryButtonText);
            element.Q<Button>("RetryButton").RegisterCallback<PointerUpEvent>(async evt =>
            {

                Visible = false;
                Model.ShowLoadingProgress(true);
                try
                {
                    var orderStatus = await Model.RetryPendingPurchaseByOrderId(items[index].OrderId);

                    // Order statuses are now shown as "pending"
                    orderStatus = OrderStatus.pending;

                    Model.ShowLoadingProgress(false);

                    switch (orderStatus)
                    {
                        case OrderStatus.canceled:
                            Visible = false;
                            Model.ShowGeneralNotification(
                                Locale.GetTranslation(LocaleTextKey.IAPPendingPurchaseCanceled),
                                false,
                                7000
                            );
                            break;
                        case OrderStatus.refunded:
                            Visible = false;
                            Model.ShowGeneralNotification(
                                Locale.GetTranslation(LocaleTextKey.IAPPendingPurchaseRefunded),
                                false,
                                7000
                            );
                            break;
                        case OrderStatus.voided:
                            Visible = false;
                            Model.ShowGeneralNotification(
                                Locale.GetTranslation(LocaleTextKey.IAPPendingPurchaseVoided),
                                false,
                                7000
                            );
                            break;
                        case OrderStatus.completed:
                            Visible = false;
                            Model.ShowGeneralNotification(
                                Locale.GetTranslation(LocaleTextKey.IAPPendingPurchaseCompleted),
                                true
                            );
                            break;
                        default:
                            Model.ShowGeneralNotification(
                                Locale.GetTranslation(LocaleTextKey.IAPPendingPurchaseNotVerified), 
                                false
                            );
                            Visible = true;
                            break;
                    }
                }
                catch (Exception e)
                {
                    _log.Error("Exception: " + e);
                    Model.ShowGeneralNotification("Purchase is not verified yet. Please try again later.", false);
                    Visible = true;
                }
                Model.ShowLoadingProgress(false);
            });

            // Assign value to elements
            var text = $"OrderID {items[index].OrderId.ToString()}";
            if (items[index].Timestamp != "")
            {
                text += $" - {items[index].Timestamp}";
            }
            element.Q<Label>("OrderId").text = text;
            element.Q<Label>("PaymentDetail").text = $"{items[index].PaymentType} - {items[index].PurchaseItemName}";

            element.Q<Label>("Status").text = items[index].Status;
            switch (items[index].Status) {
                /*
                case "refunded":
                    element.Q<Label>("Status").AddToClassList("status-label-refunded");
                    break;
                case "canceled":
                    element.Q<Label>("Status").AddToClassList("status-label-canceled");
                    break;
                case "verification_failed":
                    element.Q<Label>("Status").AddToClassList("status-label-verification-failed");
                    break;
                */
                default:
                    element.Q<Label>("Status").AddToClassList("status-label-pending");
                    break;
            }
        }
    }
}
