using System;
using UnityEngine;
using UnityEngine.Video;

namespace com.noctuagames.sdk.AdPlaceholder
{
    /// <summary>
    /// Result of a cross-promotion asset load. Exactly one of <see cref="Image"/> or
    /// <see cref="Video"/> is populated depending on <see cref="IsVideo"/>. A null result
    /// means the load failed and the caller should close the placeholder (no static fallback).
    /// </summary>
    public class CrossPromoAsset
    {
        /// <summary>True when the loaded asset is a video; false when it is an image.</summary>
        public bool IsVideo;

        /// <summary>The loaded image texture (null for videos).</summary>
        public Texture2D Image;

        /// <summary>The render texture the video draws into (null for images). Not yet playing — the caller calls <see cref="Player"/>.Play().</summary>
        public RenderTexture Video;

        /// <summary>The prepared video player (null for images). The caller owns playback (Play/Stop).</summary>
        public VideoPlayer Player;
    }

    /// <summary>
    /// Defines the types of ad placeholders that can be displayed when real ads are unavailable.
    /// </summary>
    public enum AdPlaceholderType
    {
        /// <summary>Full-screen interstitial ad placeholder.</summary>
        Interstitial,

        /// <summary>Rewarded video ad placeholder.</summary>
        Rewarded,

        /// <summary>Rewarded interstitial ad placeholder.</summary>
        RewardedInterstitial,

        /// <summary>Banner ad placeholder.</summary>
        Banner
    }
}
