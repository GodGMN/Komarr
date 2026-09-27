using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Manga
{
    public class MangaReleasePolicy
    {
        public List<string> AllowedLanguages { get; set; } = new ();
        public List<string> AllowedContainers { get; set; } = new ();
        public List<string> AllowedSources { get; set; } = new ();
        public List<string> AllowedEditions { get; set; } = new ();
        public List<string> BlockedReleaseIds { get; set; } = new ();
        public long? MinimumSizeBytes { get; set; }
        public long? MaximumSizeBytes { get; set; }
        public int? MinimumSeeders { get; set; }
        public int? MinimumCustomFormatScore { get; set; }
    }

    public class MangaReleaseDecision
    {
        public List<string> Rejections { get; set; } = new ();
        public List<string> ReviewReasons { get; set; } = new ();
        public List<string> Evidence { get; set; } = new ();
        public bool CanGrabManually => Rejections.Count == 0;
        public bool CanGrabAutomatically => CanGrabManually && ReviewReasons.Count == 0;
    }

    public interface IMangaReleaseDecisionEngine
    {
        MangaReleaseDecision Evaluate(Manga manga,
            MangaMatchOutcome match,
            MangaMatchCandidate candidate,
            ReleaseInfo release,
            IEnumerable<MangaFile> files,
            IEnumerable<MangaFileItem> fileItems,
            MangaReleasePolicy policy = null,
            int? requestedItemId = null,
            int? customFormatScore = null);
    }

    public class MangaReleaseDecisionEngine : IMangaReleaseDecisionEngine
    {
        public MangaReleaseDecision Evaluate(Manga manga,
            MangaMatchOutcome match,
            MangaMatchCandidate candidate,
            ReleaseInfo release,
            IEnumerable<MangaFile> files,
            IEnumerable<MangaFileItem> fileItems,
            MangaReleasePolicy policy = null,
            int? requestedItemId = null,
            int? customFormatScore = null)
        {
            var decision = new MangaReleaseDecision();

            if (manga == null || release == null)
            {
                decision.Rejections.Add("Manga or release information is missing.");
                return decision;
            }

            policy ??= manga.QualityPolicy ?? new MangaReleasePolicy();

            if (candidate == null || candidate.Manga?.Id != manga.Id || match?.Candidates?.Contains(candidate) != true)
            {
                decision.Rejections.Add("Release title does not match this manga.");
                return decision;
            }

            decision.Evidence.AddRange(candidate.Evidence);
            if (!manga.Monitored)
            {
                decision.Rejections.Add("Manga is not monitored.");
            }

            if (match.Release?.UnitType != (manga.TrackingMode == MangaTrackingMode.Volume ? MangaReleaseUnitType.Volume : MangaReleaseUnitType.Chapter))
            {
                decision.Rejections.Add($"Release is a {match.Release?.UnitType.ToString().ToLowerInvariant()} but this manga tracks {manga.TrackingMode.ToString().ToLowerInvariant()}s.");
            }

            if (requestedItemId.HasValue && !candidate.ContainsRequestedItem)
            {
                decision.Rejections.Add("Release does not contain the requested item.");
            }

            if (requestedItemId.HasValue && candidate.CoveredItems.Any(item => item.Id == requestedItemId.Value && !item.Monitored))
            {
                decision.Rejections.Add("Requested manga item is not monitored.");
            }

            if (candidate.CoveredItems.Count == 0)
            {
                decision.ReviewReasons.Add("No known manga item falls inside the parsed release coverage.");
            }
            else if (candidate.CoveredItems.All(item => !item.Monitored))
            {
                decision.Rejections.Add("Covered manga items are not monitored.");
            }

            var ownedFileIds = (files ?? Enumerable.Empty<MangaFile>()).Where(file => file.MangaId == manga.Id)
                .Select(file => file.Id).ToHashSet();
            var ownedItemIds = (fileItems ?? Enumerable.Empty<MangaFileItem>())
                .Where(link => ownedFileIds.Contains(link.MangaFileId)).Select(link => link.MangaItemId).ToHashSet();
            if (candidate.CoveredItems.Count > 0 && candidate.CoveredItems.All(item => ownedItemIds.Contains(item.Id)))
            {
                decision.Rejections.Add("All covered manga items already have imported files.");
            }

            if (IsBlocked(policy, release))
            {
                decision.Rejections.Add("Release is blocklisted.");
            }

            CheckAllowed(decision, policy.AllowedLanguages, match.Release?.Language, "language");
            CheckAllowed(decision, policy.AllowedSources, match.Release?.Source, "source");
            CheckAllowed(decision, policy.AllowedEditions, match.Release?.EditionHint, "edition");
            CheckAllowed(decision, policy.AllowedContainers, GetContainer(release), "container");

            if (policy.MinimumSizeBytes.HasValue && release.Size < policy.MinimumSizeBytes.Value)
            {
                decision.Rejections.Add($"Release size is below the {policy.MinimumSizeBytes.Value} byte minimum.");
            }

            if (policy.MaximumSizeBytes.HasValue && release.Size > policy.MaximumSizeBytes.Value)
            {
                decision.Rejections.Add($"Release size exceeds the {policy.MaximumSizeBytes.Value} byte maximum.");
            }

            if (policy.MinimumSeeders.HasValue && release is TorrentInfo torrent && !torrent.Seeders.HasValue)
            {
                decision.ReviewReasons.Add("Torrent seeder count is unknown.");
            }
            else if (policy.MinimumSeeders.HasValue && release is TorrentInfo knownTorrent && knownTorrent.Seeders < policy.MinimumSeeders.Value)
            {
                decision.Rejections.Add($"Torrent has {knownTorrent.Seeders} seeders; {policy.MinimumSeeders.Value} required.");
            }

            if (policy.MinimumCustomFormatScore.HasValue && !customFormatScore.HasValue)
            {
                decision.ReviewReasons.Add("Custom format score is unknown.");
            }
            else if (policy.MinimumCustomFormatScore.HasValue && customFormatScore < policy.MinimumCustomFormatScore.Value)
            {
                decision.Rejections.Add($"Custom format score is below the {policy.MinimumCustomFormatScore.Value} minimum.");
            }

            if (match.IsAmbiguous)
            {
                decision.ReviewReasons.Add("Several manga match this title; select the correct manga manually.");
            }

            if (!candidate.CanAutoMatch)
            {
                decision.ReviewReasons.Add("Title match or parser confidence requires manual confirmation.");
            }

            return decision;
        }

        private static bool IsBlocked(MangaReleasePolicy policy, ReleaseInfo release)
        {
            return policy.BlockedReleaseIds.Any(value =>
                string.Equals(value, release.Guid, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, release.DownloadUrl, StringComparison.OrdinalIgnoreCase));
        }

        private static void CheckAllowed(MangaReleaseDecision decision, List<string> allowed, string value, string dimension)
        {
            if (allowed.Count == 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                decision.ReviewReasons.Add($"Release {dimension} is unknown; allowed {dimension}s cannot be checked automatically.");
            }
            else if (!allowed.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                decision.Rejections.Add($"Release {dimension} '{value}' is not allowed.");
            }
        }

        private static string GetContainer(ReleaseInfo release)
        {
            if (!string.IsNullOrWhiteSpace(release.Container))
            {
                return release.Container.TrimStart('.').ToUpperInvariant();
            }

            var extension = Path.GetExtension(release.Title ?? string.Empty).TrimStart('.').ToUpperInvariant();
            return new[] { "CBZ", "CBR", "EPUB", "PDF", "ZIP" }.Contains(extension) ? extension : null;
        }
    }
}
