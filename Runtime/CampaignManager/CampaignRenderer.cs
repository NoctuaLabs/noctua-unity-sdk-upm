using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.Campaign
{
    /// <summary>
    /// Turns a <see cref="CampaignNode"/> tree into a detached UI Toolkit
    /// <c>VisualElement</c>. Pure and side-effect-light: no <c>MonoBehaviour</c>, so it
    /// unit-tests in EditMode. The caller attaches the returned root to its panel in a
    /// single <c>Add</c> — the renderer never mutates an already-attached tree.
    ///
    /// Forward-compat: an unknown node <c>type</c> (or a campaign whose
    /// <see cref="CampaignItem.EffectiveSchemaVersion"/> exceeds
    /// <see cref="SupportedSchemaVersion"/>) is skipped and logged — never thrown.
    /// </summary>
    public partial class CampaignRenderer
    {
        /// <summary>Highest widget-schema version this build understands.</summary>
        public const int SupportedSchemaVersion = 1;

        /// <summary>Recursion cap — a pathologically nested tree would otherwise StackOverflow (uncatchable).</summary>
        private const int MaxDepth = 40;

        /// <summary>Total node cap per campaign — bounds fan-out / VisualElement memory from a bad payload.</summary>
        private const int MaxNodes = 600;

        private readonly ICampaignActions _actions;
        private readonly ICampaignImageSource _images;
        private readonly ICampaignFontSource _fonts;
        private readonly ILogger _log;

        private const string LogTag = "[campaign_render]";

        // Per-RenderCampaign state (single-threaded: one full tree renders before the next starts).
        private int _depth;
        private int _nodeBudget;
        private bool _budgetExceeded;
        private readonly List<string> _renderUrls = new List<string>();

        public CampaignRenderer(ICampaignActions actions, ICampaignImageSource images,
            ICampaignFontSource fonts = null, ILogger log = null)
        {
            _actions = actions;
            _images = images;
            _fonts = fonts;
            _log = log ?? new NoctuaLogger(typeof(CampaignRenderer));
        }

        /// <summary>
        /// Builds the view for <paramref name="item"/>. Returns <c>null</c> when the campaign's
        /// schema is too new or its root node is unrenderable.
        /// </summary>
        public VisualElement RenderCampaign(CampaignItem item, CampaignRuntimeController controller, int configSchemaVersion = 1)
        {
            if (item?.View == null)
            {
                _log.Warning($"{LogTag} campaign '{item?.Id}' has no view tree");
                return null;
            }

            var required = item.EffectiveSchemaVersion(configSchemaVersion);
            if (required > SupportedSchemaVersion)
            {
                _log.Warning($"{LogTag} skipping campaign '{item.Id}': schema v{required} > supported v{SupportedSchemaVersion}");
                return null;
            }

            if (!CampaignValidator.TryValidate(item, out var invalid))
            {
                _log.Warning($"{LogTag} campaign '{item.Id}' invalid — {invalid}");
                return null;
            }

            _depth = 0;
            _nodeBudget = MaxNodes;
            _budgetExceeded = false;
            _renderUrls.Clear();

            try
            {
                var root = Render(item.View, item, controller);

                // Pin the images this tree uses so the RAM cache won't evict them while it's on
                // screen; release on Close (controller dispose).
                if (root != null && _images != null && _renderUrls.Count > 0)
                {
                    var urls = _renderUrls.ToArray();
                    _images.Pin(urls);
                    controller?.OnDispose(() => _images.Unpin(urls));
                }

                return root;
            }
            catch (Exception e)
            {
                _log.Error($"{LogTag} campaign '{item.Id}' failed to render: {e.Message}");
                return null;
            }
        }

        /// <summary>Recursively renders one node. Unknown type / over-budget → <c>null</c> (skip), never throws.</summary>
        public VisualElement Render(CampaignNode node, CampaignItem item, CampaignRuntimeController controller)
        {
            if (node == null) return null;

            if (_depth >= MaxDepth)
            {
                _log.Warning($"{LogTag} tree exceeds max depth {MaxDepth} — subtree skipped (campaign '{item?.Id}')");
                return null;
            }
            if (_nodeBudget <= 0)
            {
                if (!_budgetExceeded)
                {
                    _budgetExceeded = true;
                    _log.Warning($"{LogTag} tree exceeds {MaxNodes} nodes — remainder skipped (campaign '{item?.Id}')");
                }
                return null;
            }
            _nodeBudget--;
            _depth++;
            try
            {
                return RenderNode(node, item, controller);
            }
            finally
            {
                _depth--;
            }
        }

        private VisualElement RenderNode(CampaignNode node, CampaignItem item, CampaignRuntimeController controller)
        {
            VisualElement ve;
            switch ((node.Type ?? string.Empty).Trim().ToLowerInvariant())
            {
                case CampaignNode.TypeContainer: ve = BuildContainer(node, item, controller); break;
                case CampaignNode.TypeText: ve = BuildText(node, item); break;
                case CampaignNode.TypeImage: ve = BuildImage(node, item); break;
                case CampaignNode.TypeButton: ve = BuildButton(node, item); break;
                case CampaignNode.TypeSpacer: ve = BuildSpacer(node); break;
                case CampaignNode.TypeDivider: ve = BuildDivider(node); break;
                case CampaignNode.TypeList: ve = BuildList(node, item, controller); break;
                case CampaignNode.TypeCarousel: ve = BuildCarousel(node, item, controller); break;
                case CampaignNode.TypeProgressBar: ve = BuildProgressBar(node, item); break;
                case CampaignNode.TypeCountdown: ve = BuildCountdown(node, item, controller); break;
                default:
                    _log.Warning($"{LogTag} unknown node type '{node.Type}' — skipped");
                    return null;
            }

            if (ve == null) return null;

            ApplyStyleWithResponsive(ve, node, controller);
            ApplyFontFamily(ve, node);
            WireAction(ve, node, item);
            return ve;
        }

        // ---- shared widgets -------------------------------------------------

        private VisualElement BuildContainer(CampaignNode node, CampaignItem item, CampaignRuntimeController controller)
        {
            var ve = new VisualElement { name = "campaign-container" };
            AddChildren(ve, node, item, controller);
            return ve;
        }

        private VisualElement BuildText(CampaignNode node, CampaignItem item)
        {
            var label = new Label(ResolveText(node, item)) { name = "campaign-text" };
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private VisualElement BuildImage(CampaignNode node, CampaignItem item)
        {
            var ve = new VisualElement { name = "campaign-image" };

            var scaleMode = node.PropString("scaleMode", "contain")
                .Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
            ve.style.backgroundSize = scaleMode switch
            {
                "cover" or "scaleandcrop" => new BackgroundSize(BackgroundSizeType.Cover),
                "stretch" or "stretchtofill" => new BackgroundSize(Length.Percent(100), Length.Percent(100)),
                _ => new BackgroundSize(BackgroundSizeType.Contain),
            };

            var url = ResolveTokens(node.PropString("url"), item);
            if (!string.IsNullOrEmpty(url) && _images != null)
            {
                _renderUrls.Add(url);
                _images.GetImage(url, tex =>
                {
                    if (tex != null) ve.style.backgroundImage = new StyleBackground(tex);
                });
            }
            return ve;
        }

        private VisualElement BuildButton(CampaignNode node, CampaignItem item)
        {
            var button = new Button { name = "campaign-button", text = ResolveText(node, item) };
            // Action wiring happens in WireAction via ClickEvent so buttons and
            // tappable images share one path.
            return button;
        }

        private VisualElement BuildSpacer(CampaignNode node)
        {
            var ve = new VisualElement { name = "campaign-spacer" };
            if (node.Style?.FlexGrow == null && node.Style?.Height == null && node.Style?.Width == null)
            {
                ve.style.flexGrow = 1f;
            }
            return ve;
        }

        private VisualElement BuildDivider(CampaignNode node)
        {
            var ve = new VisualElement { name = "campaign-divider" };
            ve.style.height = 1f;
            ve.style.flexShrink = 0f;
            if (CampaignStyleMapper.TryColor(node.PropString("color", "#3A3F47"), out var c))
            {
                ve.style.backgroundColor = c;
            }
            return ve;
        }

        private VisualElement BuildList(CampaignNode node, CampaignItem item, CampaignRuntimeController controller)
        {
            var ve = new VisualElement { name = "campaign-list" };
            ve.style.flexDirection = FlexDirection.Column;
            AddChildren(ve, node, item, controller);
            return ve;
        }

        // ---- helpers ------------------------------------------------------

        private void AddChildren(VisualElement parent, CampaignNode node, CampaignItem item, CampaignRuntimeController controller)
        {
            if (node.Children == null) return;
            foreach (var child in node.Children)
            {
                var childVe = Render(child, item, controller);
                if (childVe != null) parent.Add(childVe);
            }
        }

        private void ApplyStyleWithResponsive(VisualElement ve, CampaignNode node, CampaignRuntimeController controller)
        {
            CampaignStyleMapper.Apply(ve, node.Style);

            if (node.Responsive == null || node.Responsive.Count == 0) return;

            void ReapplyForOrientation()
            {
                CampaignStyleMapper.Apply(ve, node.Style);
                var key = (ve.panel?.visualTree?.layout.width ?? Screen.width)
                          > (ve.panel?.visualTree?.layout.height ?? Screen.height)
                    ? "landscape" : "portrait";
                if (node.Responsive.TryGetValue(key, out var overrideStyle))
                {
                    CampaignStyleMapper.Apply(ve, overrideStyle);
                }
            }

            ReapplyForOrientation();

            EventCallback<GeometryChangedEvent> cb = _ => ReapplyForOrientation();
            ve.RegisterCallback<GeometryChangedEvent>(cb);
            controller?.OnDispose(() => ve.UnregisterCallback<GeometryChangedEvent>(cb));
        }

        /// <summary>
        /// Resolves <c>style.fontFamily</c> against the config font registry and applies the
        /// downloaded font to <paramref name="ve"/>. UI Toolkit inherits <c>unityFontDefinition</c>
        /// to children, so a <c>fontFamily</c> on a container styles all its text. A missing
        /// source, an empty family, or a failed/offline load leaves the panel's default font.
        /// </summary>
        private void ApplyFontFamily(VisualElement ve, CampaignNode node)
        {
            var family = node.Style?.FontFamily;
            if (string.IsNullOrWhiteSpace(family) || _fonts == null) return;

            _fonts.GetFont(family.Trim(), fa =>
            {
                if (fa != null) ve.style.unityFontDefinition = new StyleFontDefinition(fa);
            });
        }

        private void WireAction(VisualElement ve, CampaignNode node, CampaignItem item)
        {
            if (node.Action == null || node.Action.Type == CampaignActionType.None || _actions == null) return;

            var resolved = ResolveActionTokens(node.Action, item);
            ve.RegisterCallback<ClickEvent>(_ => _actions.Dispatch(resolved, item));
        }

        /// <summary>Returns a copy of <paramref name="a"/> with <c>{{token}}</c> resolved in its string fields.</summary>
        private CampaignAction ResolveActionTokens(CampaignAction a, CampaignItem item)
        {
            return new CampaignAction
            {
                TypeRaw = a.TypeRaw,
                Deeplink = ResolveTokens(a.Deeplink, item),
                ProductId = ResolveTokens(a.ProductId, item),
            };
        }

        private string ResolveText(CampaignNode node, CampaignItem item)
        {
            var locKey = node.PropString("locKey");
            string raw;
            if (!string.IsNullOrEmpty(locKey) && item?.Data != null && item.Data.TryGetValue(locKey, out var loc))
            {
                raw = loc;
            }
            else
            {
                raw = node.PropString("text", string.Empty);
            }
            return ResolveTokens(raw, item);
        }

        /// <summary>Replaces <c>{{key}}</c> with <c>item.Data[key]</c> (missing → empty, logged).</summary>
        public string ResolveTokens(string input, CampaignItem item)
        {
            return CampaignTokens.Resolve(
                input,
                item?.Data,
                key => _log.Warning($"{LogTag} unresolved token '{{{{{key}}}}}' in campaign '{item?.Id}'"));
        }
    }
}
