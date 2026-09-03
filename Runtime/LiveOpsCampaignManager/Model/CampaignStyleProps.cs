using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// A whitelisted subset of USS layout/appearance properties, expressed as JSON.
    /// Length values are strings so both <c>"12"</c> (px) and <c>"50%"</c> are accepted;
    /// colors are <c>#RRGGBB</c> / <c>#RRGGBBAA</c>. The renderer maps each non-null field
    /// onto <c>VisualElement.style.*</c> and silently ignores anything it can't parse.
    /// </summary>
    [Preserve]
    public class CampaignStyleProps
    {
        [JsonProperty("width")] public string Width;
        [JsonProperty("height")] public string Height;
        [JsonProperty("minWidth")] public string MinWidth;
        [JsonProperty("maxWidth")] public string MaxWidth;
        [JsonProperty("minHeight")] public string MinHeight;
        [JsonProperty("maxHeight")] public string MaxHeight;

        [JsonProperty("flexGrow")] public float? FlexGrow;
        [JsonProperty("flexShrink")] public float? FlexShrink;
        [JsonProperty("flexDirection")] public string FlexDirection;   // row | column | row-reverse | column-reverse
        [JsonProperty("justifyContent")] public string JustifyContent; // flex-start | center | flex-end | space-between | space-around
        [JsonProperty("alignItems")] public string AlignItems;         // flex-start | center | flex-end | stretch
        [JsonProperty("alignSelf")] public string AlignSelf;
        [JsonProperty("flexWrap")] public string FlexWrap;             // nowrap | wrap

        [JsonProperty("padding")] public string Padding;               // "10" | "10 20" | "10 20 30 40"
        [JsonProperty("paddingTop")] public string PaddingTop;
        [JsonProperty("paddingRight")] public string PaddingRight;
        [JsonProperty("paddingBottom")] public string PaddingBottom;
        [JsonProperty("paddingLeft")] public string PaddingLeft;

        [JsonProperty("margin")] public string Margin;
        [JsonProperty("marginTop")] public string MarginTop;
        [JsonProperty("marginRight")] public string MarginRight;
        [JsonProperty("marginBottom")] public string MarginBottom;
        [JsonProperty("marginLeft")] public string MarginLeft;

        [JsonProperty("backgroundColor")] public string BackgroundColor;
        [JsonProperty("color")] public string Color;
        [JsonProperty("fontSize")] public string FontSize;
        [JsonProperty("fontWeight")] public string FontWeight;         // normal | bold | italic | bold-italic
        [JsonProperty("textAlign")] public string TextAlign;           // upper-left ... middle-center ... lower-right
        [JsonProperty("fontFamily")] public string FontFamily;         // a CampaignItem.Fonts key, else a direct Resources path (e.g. "Fonts/Honk-Regular")
        [JsonProperty("fontOutlineWidth")] public string FontOutlineWidth; // "2" | "2px" — glyph stroke
        [JsonProperty("fontOutlineColor")] public string FontOutlineColor; // #RRGGBB[AA]

        [JsonProperty("borderRadius")] public string BorderRadius;
        [JsonProperty("borderWidth")] public string BorderWidth;
        [JsonProperty("borderColor")] public string BorderColor;

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
