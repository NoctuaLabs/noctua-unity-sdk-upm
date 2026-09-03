using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// A whitelisted subset of USS layout/appearance properties, expressed as JSON.
    /// Keys are <c>snake_case</c> to match the rest of <c>noctuagg.json</c>.
    /// Length values are strings so both <c>"12"</c> (px) and <c>"50%"</c> are accepted;
    /// colors are <c>#RRGGBB</c> / <c>#RRGGBBAA</c>. The renderer maps each non-null field
    /// onto <c>VisualElement.style.*</c> and silently ignores anything it can't parse.
    /// </summary>
    [Preserve]
    public class CampaignStyleProps
    {
        [JsonProperty("width")] public string Width;
        [JsonProperty("height")] public string Height;
        [JsonProperty("min_width")] public string MinWidth;
        [JsonProperty("max_width")] public string MaxWidth;
        [JsonProperty("min_height")] public string MinHeight;
        [JsonProperty("max_height")] public string MaxHeight;

        [JsonProperty("flex_grow")] public float? FlexGrow;
        [JsonProperty("flex_shrink")] public float? FlexShrink;
        [JsonProperty("flex_direction")] public string FlexDirection;   // row | column | row-reverse | column-reverse
        [JsonProperty("justify_content")] public string JustifyContent; // flex-start | center | flex-end | space-between | space-around
        [JsonProperty("align_items")] public string AlignItems;         // flex-start | center | flex-end | stretch
        [JsonProperty("align_self")] public string AlignSelf;
        [JsonProperty("flex_wrap")] public string FlexWrap;             // nowrap | wrap

        [JsonProperty("padding")] public string Padding;               // "10" | "10 20" | "10 20 30 40"
        [JsonProperty("padding_top")] public string PaddingTop;
        [JsonProperty("padding_right")] public string PaddingRight;
        [JsonProperty("padding_bottom")] public string PaddingBottom;
        [JsonProperty("padding_left")] public string PaddingLeft;

        [JsonProperty("margin")] public string Margin;
        [JsonProperty("margin_top")] public string MarginTop;
        [JsonProperty("margin_right")] public string MarginRight;
        [JsonProperty("margin_bottom")] public string MarginBottom;
        [JsonProperty("margin_left")] public string MarginLeft;

        [JsonProperty("background_color")] public string BackgroundColor;
        [JsonProperty("color")] public string Color;
        [JsonProperty("font_size")] public string FontSize;
        [JsonProperty("font_weight")] public string FontWeight;         // normal | bold | italic | bold-italic
        [JsonProperty("text_align")] public string TextAlign;           // upper-left ... middle-center ... lower-right
        [JsonProperty("font_family")] public string FontFamily;         // a CampaignItem.fonts key, else a direct Resources path (e.g. "Fonts/Honk-Regular")
        [JsonProperty("font_outline_width")] public string FontOutlineWidth; // "2" | "2px" — glyph stroke
        [JsonProperty("font_outline_color")] public string FontOutlineColor; // #RRGGBB[AA]

        [JsonProperty("border_radius")] public string BorderRadius;
        [JsonProperty("border_width")] public string BorderWidth;
        [JsonProperty("border_color")] public string BorderColor;

        [JsonProperty("display")] public string Display;               // flex | none
        [JsonProperty("overflow")] public string Overflow;             // visible | hidden | scroll
        [JsonProperty("opacity")] public float? Opacity;
        [JsonProperty("position")] public string Position;             // relative | absolute
        [JsonProperty("top")] public string Top;
        [JsonProperty("right")] public string Right;
        [JsonProperty("bottom")] public string Bottom;
        [JsonProperty("left")] public string Left;
    }
}
