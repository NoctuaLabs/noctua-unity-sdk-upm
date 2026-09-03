using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Maps a <see cref="CampaignStyleProps"/> (whitelisted USS subset from JSON) onto a
    /// <c>VisualElement.style</c>. Every field is independent and best-effort: an unparseable
    /// value is skipped, never thrown. Layout properties written here are set once at build
    /// time (or on an orientation flip) — never animated (see the renderer's animation rule).
    /// </summary>
    public static class CampaignStyleMapper
    {
        /// <summary>Applies every non-null field of <paramref name="s"/> to <paramref name="ve"/>.</summary>
        public static void Apply(VisualElement ve, CampaignStyleProps s)
        {
            if (ve == null || s == null) return;

            SetLength(s.Width, v => ve.style.width = v);
            SetLength(s.Height, v => ve.style.height = v);
            SetLength(s.MinWidth, v => ve.style.minWidth = v);
            SetLength(s.MaxWidth, v => ve.style.maxWidth = v);
            SetLength(s.MinHeight, v => ve.style.minHeight = v);
            SetLength(s.MaxHeight, v => ve.style.maxHeight = v);

            if (s.FlexGrow.HasValue) ve.style.flexGrow = s.FlexGrow.Value;
            if (s.FlexShrink.HasValue) ve.style.flexShrink = s.FlexShrink.Value;
            if (TryEnum<FlexDirection>(s.FlexDirection, out var fd)) ve.style.flexDirection = fd;
            if (TryEnum<Justify>(s.JustifyContent, out var jc)) ve.style.justifyContent = jc;
            if (TryEnum<Align>(s.AlignItems, out var ai)) ve.style.alignItems = ai;
            if (TryEnum<Align>(s.AlignSelf, out var asf)) ve.style.alignSelf = asf;
            if (TryEnum<Wrap>(s.FlexWrap, out var fw)) ve.style.flexWrap = fw;

            ApplyEdges(s.Padding, s.PaddingTop, s.PaddingRight, s.PaddingBottom, s.PaddingLeft,
                t => ve.style.paddingTop = t, r => ve.style.paddingRight = r,
                b => ve.style.paddingBottom = b, l => ve.style.paddingLeft = l);

            ApplyEdges(s.Margin, s.MarginTop, s.MarginRight, s.MarginBottom, s.MarginLeft,
                t => ve.style.marginTop = t, r => ve.style.marginRight = r,
                b => ve.style.marginBottom = b, l => ve.style.marginLeft = l);

            if (TryColor(s.BackgroundColor, out var bg)) ve.style.backgroundColor = bg;
            if (TryColor(s.Color, out var col)) ve.style.color = col;
            SetLength(s.FontSize, v => ve.style.fontSize = v);
            if (TryFontStyle(s.FontWeight, out var fs)) ve.style.unityFontStyleAndWeight = fs;
            if (TryTextAlign(s.TextAlign, out var ta)) ve.style.unityTextAlign = ta;

            // Glyph stroke ("font border") — distinct from the element box border below.
            // Inherited to child text like `color`.
            if (TryFloat(s.FontOutlineWidth, out var ow)) ve.style.unityTextOutlineWidth = ow;
            if (TryColor(s.FontOutlineColor, out var oc)) ve.style.unityTextOutlineColor = oc;

            SetLength(s.BorderRadius, v =>
            {
                ve.style.borderTopLeftRadius = v;
                ve.style.borderTopRightRadius = v;
                ve.style.borderBottomLeftRadius = v;
                ve.style.borderBottomRightRadius = v;
            });

            if (TryFloat(s.BorderWidth, out var bw))
            {
                ve.style.borderTopWidth = bw;
                ve.style.borderRightWidth = bw;
                ve.style.borderBottomWidth = bw;
                ve.style.borderLeftWidth = bw;
            }

            if (TryColor(s.BorderColor, out var bc))
            {
                ve.style.borderTopColor = bc;
                ve.style.borderRightColor = bc;
                ve.style.borderBottomColor = bc;
                ve.style.borderLeftColor = bc;
            }

            if (!string.IsNullOrEmpty(s.Display))
            {
                ve.style.display = s.Display.Trim().Equals("none", StringComparison.OrdinalIgnoreCase)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            if (!string.IsNullOrEmpty(s.Overflow))
            {
                ve.style.overflow = s.Overflow.Trim().Equals("visible", StringComparison.OrdinalIgnoreCase)
                    ? UnityEngine.UIElements.Overflow.Visible
                    : UnityEngine.UIElements.Overflow.Hidden;
            }

            if (s.Opacity.HasValue) ve.style.opacity = Mathf.Clamp01(s.Opacity.Value);

            if (!string.IsNullOrEmpty(s.Position))
            {
                ve.style.position = s.Position.Trim().Equals("absolute", StringComparison.OrdinalIgnoreCase)
                    ? Position.Absolute
                    : Position.Relative;
            }

            SetLength(s.Top, v => ve.style.top = v);
            SetLength(s.Right, v => ve.style.right = v);
            SetLength(s.Bottom, v => ve.style.bottom = v);
            SetLength(s.Left, v => ve.style.left = v);
        }

        /// <summary>Parses <c>"12"</c> / <c>"12px"</c> / <c>"50%"</c> into a UI Toolkit length.</summary>
        public static bool TryLength(string raw, out Length value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var t = raw.Trim();
            if (t.EndsWith("%"))
            {
                if (float.TryParse(t.Substring(0, t.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var p))
                {
                    value = new Length(p, LengthUnit.Percent);
                    return true;
                }
                return false;
            }

            if (t.EndsWith("px")) t = t.Substring(0, t.Length - 2);
            if (float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var px))
            {
                value = new Length(px);
                return true;
            }
            return false;
        }

        /// <summary>Parses <c>#RGB</c> / <c>#RRGGBB</c> / <c>#RRGGBBAA</c> / named colors.</summary>
        public static bool TryColor(string raw, out Color value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            return ColorUtility.TryParseHtmlString(raw.Trim(), out value);
        }

        private static bool TryFloat(string raw, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var t = raw.Trim();
            if (t.EndsWith("px")) t = t.Substring(0, t.Length - 2);
            return float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static void SetLength(string raw, Action<Length> set)
        {
            if (TryLength(raw, out var v)) set(v);
        }

        private static void ApplyEdges(
            string shorthand, string top, string right, string bottom, string left,
            Action<Length> setTop, Action<Length> setRight, Action<Length> setBottom, Action<Length> setLeft)
        {
            if (!string.IsNullOrWhiteSpace(shorthand))
            {
                var parts = shorthand.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1 && TryLength(parts[0], out var all))
                {
                    setTop(all); setRight(all); setBottom(all); setLeft(all);
                }
                else if (parts.Length == 2 && TryLength(parts[0], out var v) && TryLength(parts[1], out var h))
                {
                    setTop(v); setBottom(v); setRight(h); setLeft(h);
                }
                else if (parts.Length == 4
                         && TryLength(parts[0], out var t) && TryLength(parts[1], out var r)
                         && TryLength(parts[2], out var b) && TryLength(parts[3], out var l))
                {
                    setTop(t); setRight(r); setBottom(b); setLeft(l);
                }
            }

            if (TryLength(top, out var pt)) setTop(pt);
            if (TryLength(right, out var pr)) setRight(pr);
            if (TryLength(bottom, out var pb)) setBottom(pb);
            if (TryLength(left, out var pl)) setLeft(pl);
        }

        private static bool TryEnum<T>(string raw, out T value) where T : struct, Enum
        {
            value = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var norm = Normalize(raw);
            foreach (var name in Enum.GetNames(typeof(T)))
            {
                if (Normalize(name) == norm)
                {
                    value = (T)Enum.Parse(typeof(T), name);
                    return true;
                }
            }
            return false;
        }

        private static bool TryFontStyle(string raw, out FontStyle value)
        {
            value = FontStyle.Normal;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            switch (Normalize(raw))
            {
                case "normal": value = FontStyle.Normal; return true;
                case "bold": value = FontStyle.Bold; return true;
                case "italic": value = FontStyle.Italic; return true;
                case "bolditalic":
                case "boldanditalic": value = FontStyle.BoldAndItalic; return true;
                default: return false;
            }
        }

        private static bool TryTextAlign(string raw, out TextAnchor value)
        {
            value = TextAnchor.UpperLeft;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            return TryEnum(raw, out value);
        }

        private static string Normalize(string s) =>
            s.Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
    }
}
