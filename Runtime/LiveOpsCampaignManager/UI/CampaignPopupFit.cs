using UnityEngine.UIElements;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Fits a campaign's fixed design box into the space the popup card actually has.
    ///
    /// The admin authors a popup on a fixed canvas — 300×500 portrait or 500×300 landscape —
    /// and the payload carries that as a hard <c>width</c>/<c>height</c> on the view's root
    /// container, with every child absolutely positioned in those design coordinates. Nothing
    /// in that tree is elastic, so when the box is wider than the card the card simply clips
    /// it: the mount is a vertical-only <c>ScrollView</c>, so horizontal overflow has nowhere
    /// to go and the creative silently loses its right edge — CTA included.
    ///
    /// That happens whenever a campaign's orientation does not match the game's. Rotation is
    /// locked per game, so a landscape popup on a portrait-locked title is not a transient
    /// state the UI can wait out; it is how that campaign renders for its whole run. Scaling
    /// the box down uniformly makes the mismatch survivable — smaller, but whole and still
    /// tappable — instead of silently truncated. It also covers the honest cases: a narrow
    /// device, or a design authored larger than the card was ever going to be.
    ///
    /// Pure maths, no <c>VisualElement</c> state, so it unit-tests without a panel.
    /// </summary>
    public static class CampaignPopupFit
    {
        /// <summary>
        /// Uniform scale that fits <paramref name="designWidth"/> × <paramref name="designHeight"/>
        /// inside <paramref name="availableWidth"/> × <paramref name="availableHeight"/>.
        ///
        /// Never exceeds 1: a design smaller than the card keeps its authored size rather than
        /// being blown up, which would soften its art and betray the pixel positions the admin
        /// dragged. Returns 1 for any non-positive or non-finite input, so a not-yet-laid-out
        /// panel leaves the creative alone until the geometry callback runs again.
        /// </summary>
        public static float ScaleFor(float designWidth, float designHeight,
            float availableWidth, float availableHeight)
        {
            if (!IsUsable(designWidth) || !IsUsable(designHeight)) return 1f;
            if (!IsUsable(availableWidth) || !IsUsable(availableHeight)) return 1f;

            var scale = availableWidth / designWidth;
            var vertical = availableHeight / designHeight;
            if (vertical < scale) scale = vertical;

            return scale < 1f ? scale : 1f;
        }

        /// <summary>
        /// Reads the design box off the view root's style. Both axes must be absolute pixel
        /// lengths — a percentage or <c>auto</c> box is already elastic and sizes itself to the
        /// card, so there is nothing to fit and scaling it would fight the layout.
        /// </summary>
        public static bool TryDesignSize(CampaignStyleProps style, out float width, out float height)
        {
            width = 0f;
            height = 0f;
            if (style == null) return false;

            if (!CampaignStyleMapper.TryLength(style.Width, out var w) || w.unit != LengthUnit.Pixel) return false;
            if (!CampaignStyleMapper.TryLength(style.Height, out var h) || h.unit != LengthUnit.Pixel) return false;
            if (!IsUsable(w.value) || !IsUsable(h.value)) return false;

            width = w.value;
            height = h.value;
            return true;
        }

        private static bool IsUsable(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}
