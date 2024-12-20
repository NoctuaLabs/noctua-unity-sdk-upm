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

            _itemTemplate = Resources.Load<VisualTreeAsset>("PendingPurchaseItem");
        }

        public async UniTask<bool> Show(List<PendingPurchaseItem> pendingPurchases)
        {            
            _tcs = new UniTaskCompletionSource<bool>();

            _page = 1;

            _pendingPurchases = pendingPurchases;
            _pendingPurchases.Sort((p1, p2) => p1.OrderId.CompareTo(p2.OrderId));
            _pendingPurchases.Reverse();
            _pendingPurchasesListView = View.Q<ListView>("PendingPurchasesList");
            _pendingPurchasesListView.Rebuild();


            _log.Debug("total pending purchases: " + _pendingPurchases.Count.ToString());

            if (_pendingPurchases.Count == 0)
            {
                View.Q<Label>("Title").text = "No pending purchase at the moment";
            } else if (_pendingPurchases.Count == 1) {
                View.Q<Label>("Title").text = "Your Pending Purchase";
            } else {
                View.Q<Label>("Title").text = "Your Pending Purchases";
            }

            var currentPageContent = new List<PendingPurchaseItem>();

            foreach (var item in _pendingPurchases)
            {
                currentPageContent.Add(item);
            }

            BindListView(_pendingPurchasesListView, currentPageContent);

            Visible = true;

            return await _tcs.Task;
        }

        private void PendingPurchasesDialog(PointerUpEvent evt)
        {            
            Visible = false;

            _tcs?.TrySetResult(true);
        }

        private async void OpenCS(PointerUpEvent evt)
        {
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

            element.Q<Button>("CopyButton").RegisterCallback<PointerUpEvent>(evt =>
            {

                Model.ShowGeneralNotification(
                "Your purchase receipt has been copied to clipboard.",
                    true,
                    7000
                );
                GUIUtility.systemCopyBuffer = textToCopy;
            });

            element.Q<Button>("RetryButton").RegisterCallback<PointerUpEvent>(async evt =>
            {

                Visible = false;
                Model.ShowLoadingProgress(true);
                try
                {
                    var orderStatus = await Model.RetryPendingPurchaseByOrderId(items[index].OrderId);

                    Model.ShowLoadingProgress(false);

                    switch (orderStatus)
                    {
                        case OrderStatus.canceled:
                            Visible = false;
                            Model.ShowGeneralNotification(
                                "Your purchase has been canceled. Please contact customer support for more details.",
                                false,
                                7000
                            );
                            break;
                        case OrderStatus.refunded:
                            Visible = false;
                            Model.ShowGeneralNotification(
                                "Your purchase has been refunded. Please contact customer support for more details.",
                                false,
                                7000
                            );
                            break;
                        case OrderStatus.voided:
                            Visible = false;
                            Model.ShowGeneralNotification(
                                "Your purchase has been voided. Please contact customer support for more details.",
                                false,
                                7000
                            );
                            break;
                        case OrderStatus.completed:
                            Visible = false;
                            Model.ShowGeneralNotification("Your purchase has been verified!", true);
                            break;
                        default:
                            Model.ShowGeneralNotification("Purchase is not verified yet. Please try again later.", false);
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
