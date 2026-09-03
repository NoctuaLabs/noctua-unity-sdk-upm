using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// The three widgets that carry runtime behaviour — <c>carousel</c> (paging + optional
    /// auto-advance), <c>progressbar</c> (built-in <see cref="ProgressBar"/>), and
    /// <c>countdown</c> (per-second label). Their timers observe the
    /// <see cref="CampaignRuntimeController"/> and stop on dispose; the loops only run in
    /// play mode so the renderer stays synchronous under EditMode tests.
    /// </summary>
    public partial class CampaignRenderer
    {
        private VisualElement BuildProgressBar(CampaignNode node, CampaignItem item)
        {
            var min = node.PropFloat("min") ?? 0f;
            var max = node.PropFloat("max") ?? 100f;
            if (max <= min) max = min + 1f;
            var value = Mathf.Clamp(node.PropFloat("value") ?? min, min, max);

            var pb = new ProgressBar
            {
                name = "campaign-progressbar",
                lowValue = min,
                highValue = max,
                value = value,
            };

            var title = ResolveTokens(node.PropString("title"), item);
            if (!string.IsNullOrEmpty(title)) pb.title = title;

            if (CampaignStyleMapper.TryColor(node.PropString("bar_color"), out var barColor))
            {
                var fill = pb.Q(className: "unity-progress-bar__progress");
                if (fill != null) fill.style.backgroundColor = barColor;
            }

            return pb;
        }

        private VisualElement BuildCountdown(CampaignNode node, CampaignItem item, CampaignRuntimeController controller)
        {
            var label = new Label { name = "campaign-countdown" };
            var prefix = ResolveTokens(node.PropString("prefix", string.Empty), item);
            var suffix = ResolveTokens(node.PropString("suffix", string.Empty), item);
            var endUtc = ParseEndTimestamp(node.PropString("end_ts"), item);

            void Refresh()
            {
                var remaining = endUtc - DateTime.UtcNow;
                if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
                label.text = prefix + FormatRemaining(remaining) + suffix;
            }

            Refresh(); // synchronous initial value — asserted by EditMode tests

            if (Application.isPlaying && controller != null)
            {
                RunCountdownLoop(Refresh, endUtc, controller.Token).Forget();
            }

            return label;
        }

        private static async UniTaskVoid RunCountdownLoop(Action refresh, DateTime endUtc, System.Threading.CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    refresh();
                    if (DateTime.UtcNow >= endUtc) break;
                    await UniTask.Delay(1000, cancellationToken: ct);
                }
            }
            catch (OperationCanceledException) { /* view closed */ }
        }

        private VisualElement BuildCarousel(CampaignNode node, CampaignItem item, CampaignRuntimeController controller)
        {
            var root = new VisualElement { name = "campaign-carousel" };
            root.style.overflow = Overflow.Hidden;
            root.style.flexDirection = FlexDirection.Column;

            var viewport = new VisualElement { name = "campaign-carousel-viewport" };
            viewport.style.overflow = Overflow.Hidden;
            viewport.style.flexGrow = 1f;

            var track = new VisualElement { name = "campaign-carousel-track" };
            track.style.flexDirection = FlexDirection.Row;
            track.style.height = Length.Percent(100);
            track.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("translate") };
            track.style.transitionDuration = new List<TimeValue> { new TimeValue(250, TimeUnit.Millisecond) };

            var pages = new List<VisualElement>();
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    var childVe = Render(child, item, controller);
                    if (childVe == null) continue;

                    var page = new VisualElement { name = "campaign-carousel-page" };
                    page.style.width = Length.Percent(100);
                    page.style.height = Length.Percent(100);
                    page.style.flexShrink = 0f;
                    page.Add(childVe);
                    track.Add(page);
                    pages.Add(page);
                }
            }

            viewport.Add(track);
            root.Add(viewport);

            var count = pages.Count;
            if (count == 0) return root;

            var loop = node.PropBool("loop", true);
            var index = 0;

            void GoTo(int target)
            {
                if (count <= 1) return;
                index = loop ? ((target % count) + count) % count : Mathf.Clamp(target, 0, count - 1);
                track.style.translate = new Translate(Length.Percent(-100f * index), 0f);
                UpdateDots();
            }

            // Page dots
            var dots = new VisualElement { name = "campaign-carousel-dots" };
            dots.style.flexDirection = FlexDirection.Row;
            dots.style.justifyContent = Justify.Center;
            dots.style.flexShrink = 0f;
            var dotElements = new List<VisualElement>();
            for (var i = 0; i < count; i++)
            {
                var dot = new VisualElement { name = "campaign-carousel-dot" };
                dot.style.width = 6f;
                dot.style.height = 6f;
                dot.style.marginLeft = 3f;
                dot.style.marginRight = 3f;
                dot.style.marginTop = 6f;
                dot.style.borderTopLeftRadius = 3f;
                dot.style.borderTopRightRadius = 3f;
                dot.style.borderBottomLeftRadius = 3f;
                dot.style.borderBottomRightRadius = 3f;
                dot.style.backgroundColor = new Color(1f, 1f, 1f, 0.3f);
                dots.Add(dot);
                dotElements.Add(dot);
            }
            if (count > 1) root.Add(dots);

            void UpdateDots()
            {
                for (var i = 0; i < dotElements.Count; i++)
                {
                    dotElements[i].style.backgroundColor = i == index
                        ? new Color(1f, 1f, 1f, 0.95f)
                        : new Color(1f, 1f, 1f, 0.3f);
                }
            }
            UpdateDots();

            // Swipe detection (pointer down x → up x). Simpler and less finicky than
            // live finger-follow; good enough for a promo carousel in v1.
            var downX = 0f;
            var tracking = false;
            const float swipeThresholdPx = 40f;

            viewport.RegisterCallback<PointerDownEvent>(e => { downX = e.position.x; tracking = true; });
            viewport.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!tracking) return;
                tracking = false;
                var dx = e.position.x - downX;
                if (dx <= -swipeThresholdPx) GoTo(index + 1);
                else if (dx >= swipeThresholdPx) GoTo(index - 1);
            });
            viewport.RegisterCallback<PointerLeaveEvent>(_ => tracking = false);

            if (node.PropBool("autoplay", false) && count > 1 && Application.isPlaying && controller != null)
            {
                var intervalMs = Mathf.Max(1000, node.PropInt("interval_ms") ?? 4000);
                RunAutoplayLoop(() => GoTo(index + 1), intervalMs, controller.Token).Forget();
            }

            return root;
        }

        private static async UniTaskVoid RunAutoplayLoop(Action advance, int intervalMs, System.Threading.CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await UniTask.Delay(intervalMs, cancellationToken: ct);
                    advance();
                }
            }
            catch (OperationCanceledException) { /* view closed */ }
        }

        // ---- countdown parsing / formatting ------------------------------

        private DateTime ParseEndTimestamp(string raw, CampaignItem item)
        {
            raw = ResolveTokens(raw, item);
            if (string.IsNullOrWhiteSpace(raw)) return DateTime.UtcNow;

            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
            {
                // Heuristic: > ~year 2001 in seconds, otherwise treat as ms.
                var seconds = unix > 100_000_000_000L ? unix / 1000L : unix;
                return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
            }

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
            {
                return dt;
            }

            _log.Warning($"{LogTag} countdown end_ts '{raw}' unparseable in campaign '{item?.Id}'");
            return DateTime.UtcNow;
        }

        private static string FormatRemaining(TimeSpan t)
        {
            return t.TotalDays >= 1
                ? $"{(int)t.TotalDays}d {t.Hours:00}:{t.Minutes:00}:{t.Seconds:00}"
                : $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
        }
    }
}
