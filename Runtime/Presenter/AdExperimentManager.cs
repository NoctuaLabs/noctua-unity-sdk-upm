using System;
using System.Collections.Generic;
using System.Text;
using com.noctuagames.sdk.Events;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Assigns users to A/B experiment variants and applies the winning variant's IAA config
    /// override on top of the base config.
    ///
    /// Assignment is deterministic per user+experiment pair (stable across sessions and app restarts).
    /// Each experiment's variant assignment and the event fire flag are persisted to PlayerPrefs.
    ///
    /// Usage in Noctua.Initialization.cs:
    ///   var manager = new AdExperimentManager(mergedIaa.AdExperiments, segmentManager, eventSender);
    ///   IAA effectiveIaa = manager.ApplyExperiments(mergedIaa, countryCode);
    /// </summary>
    public class AdExperimentManager
    {
        private readonly NoctuaLogger _log = new(typeof(AdExperimentManager));
        private const string PrefsPrefix = "NoctuaExp_";

        private readonly List<AdExperimentConfig> _experiments;
        private readonly UserSegmentManager _segmentManager;
        private readonly IEventSender _eventSender;

        /// <summary>
        /// Creates a new <see cref="AdExperimentManager"/>.
        /// </summary>
        /// <param name="experiments">List of experiment configurations (from IAA.AdExperiments).</param>
        /// <param name="segmentManager">Segment manager for resolving country tier and composite segment key.</param>
        /// <param name="eventSender">Event sender used to track the ad_experiment_assigned event.</param>
        public AdExperimentManager(
            List<AdExperimentConfig> experiments,
            UserSegmentManager segmentManager,
            IEventSender eventSender)
        {
            _experiments   = experiments ?? new List<AdExperimentConfig>();
            _segmentManager = segmentManager;
            _eventSender    = eventSender;
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a new IAA config after applying all active experiment overrides to
        /// <paramref name="baseConfig"/> in sequence.
        /// </summary>
        /// <param name="baseConfig">The merged local+remote IAA config before experiment overrides.</param>
        /// <param name="isoCountryCode">The user's country code (from InitGameResponse.Country).</param>
        public IAA ApplyExperiments(IAA baseConfig, string isoCountryCode)
        {
            if (_experiments == null || _experiments.Count == 0)
                return baseConfig;

            string segmentKey = _segmentManager?.GetCompositeSegment(isoCountryCode) ?? "";
            string tierKey    = UserSegmentManager.GetCountryTier(isoCountryCode);

            IAA effective = baseConfig;

            foreach (var experiment in _experiments)
            {
                if (!experiment.Enabled) continue;

                // Segment filter: skip if experiment is tier-restricted and user is not in it
                if (!IsInSegmentFilter(experiment.SegmentFilters, tierKey)) continue;

                string userId    = _eventSender?.PseudoUserId ?? "";
                string variantId = GetAssignedVariant(experiment, segmentKey, userId);

                if (string.IsNullOrEmpty(variantId)) continue;

                var variant = FindVariant(experiment.Variants, variantId);
                if (variant == null) continue;

                TrackAssignment(experiment.ExperimentId, variantId, segmentKey);

                if (variant.IaaOverride != null)
                {
                    effective = effective.MergeWith(variant.IaaOverride);
                    _log.Info($"Experiment '{experiment.ExperimentId}' variant '{variantId}' applied for segment '{segmentKey}'.");
                }
                else
                {
                    _log.Debug($"Experiment '{experiment.ExperimentId}' variant '{variantId}' is control — no override.");
                }
            }

            return effective;
        }

        /// <summary>
        /// Deterministically assigns a variant to the user for the given experiment.
        /// Returns null if the experiment is disabled or the user is not in the segment filter.
        /// </summary>
        public string GetAssignedVariant(AdExperimentConfig experiment, string segmentKey, string userId)
        {
            if (!experiment.Enabled || experiment.Variants == null || experiment.Variants.Count == 0)
                return null;

            // Return persisted assignment if it exists (ensures stability across sessions)
            string persistedVariant = LoadPersistedVariant(experiment.ExperimentId);
            if (!string.IsNullOrEmpty(persistedVariant))
                return persistedVariant;

            // Compute deterministic bucket 0–99
            int bucket = ComputeHashBucket(userId, experiment.ExperimentId);
            string variantId = MapBucketToVariant(experiment, bucket);

            if (!string.IsNullOrEmpty(variantId))
                PersistVariant(experiment.ExperimentId, variantId);

            return variantId;
        }

        // ── Private helpers ────────────────────────────────────────────────────────

        private bool IsInSegmentFilter(List<string> filters, string tierKey)
        {
            if (filters == null || filters.Count == 0) return true;
            foreach (var filter in filters)
            {
                if (filter == tierKey) return true;
            }
            return false;
        }

        /// <summary>
        /// Computes a stable bucket (0–99) using FNV-1a 32-bit hash of "userId:experimentId".
        /// FNV-1a is deterministic across runtimes and platforms, unlike GetHashCode().
        /// </summary>
        private static int ComputeHashBucket(string userId, string experimentId)
        {
            string input = $"{userId}:{experimentId}";
            byte[] bytes = Encoding.UTF8.GetBytes(input);

            uint hash = 2166136261u; // FNV offset basis
            foreach (byte b in bytes)
            {
                hash ^= b;
                hash *= 16777619u; // FNV prime
            }

            return (int)(hash % 100);
        }

        /// <summary>
        /// Maps a bucket (0–99) to a variant using cumulative weight ranges.
        /// </summary>
        private static string MapBucketToVariant(AdExperimentConfig experiment, int bucket)
        {
            int cumulative = 0;
            foreach (var variant in experiment.Variants)
            {
                cumulative += variant.Weight;
                if (bucket < cumulative)
                    return variant.VariantId;
            }
            // Fallback: return the last variant (handles weights that don't sum to exactly 100)
            return experiment.Variants[experiment.Variants.Count - 1].VariantId;
        }

        private static AdVariantConfig FindVariant(List<AdVariantConfig> variants, string variantId)
        {
            if (variants == null) return null;
            foreach (var v in variants)
            {
                if (v.VariantId == variantId) return v;
            }
            return null;
        }

        private string LoadPersistedVariant(string experimentId)
        {
            return PlayerPrefs.GetString($"{PrefsPrefix}{experimentId}_variant", "");
        }

        private void PersistVariant(string experimentId, string variantId)
        {
            PlayerPrefs.SetString($"{PrefsPrefix}{experimentId}_variant", variantId);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Sends the ad_experiment_assigned event once per experiment per install.
        /// </summary>
        private void TrackAssignment(string experimentId, string variantId, string segmentKey)
        {
            string firedKey = $"{PrefsPrefix}{experimentId}_fired";
            if (PlayerPrefs.GetInt(firedKey, 0) == 1)
                return;

            _eventSender?.Send("ad_experiment_assigned", new Dictionary<string, IConvertible>
            {
                { "experiment_id", experimentId },
                { "variant_id",    variantId    },
                { "segment_key",   segmentKey   }
            });

            PlayerPrefs.SetInt(firedKey, 1);
            PlayerPrefs.Save();

            _log.Debug($"Tracked ad_experiment_assigned: {experimentId}/{variantId} for {segmentKey}");
        }
    }
}
