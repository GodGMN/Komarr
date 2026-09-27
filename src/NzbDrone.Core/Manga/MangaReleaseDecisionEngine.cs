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
        public List<string> SourcePreference { get; set; } = new ();
        public string UpgradeCutoffSource { get; set; }
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
        public bool IsUpgrade { get; set; }
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

            var mangaFiles = (files ?? Enumerable.Empty<MangaFile>()).Where(file => file.MangaId == manga.Id).ToList();
            var ownedFileIds = mangaFiles.Select(file => file.Id).ToHashSet();
            var links = (fileItems ?? Enumerable.Empty<MangaFileItem>())
                .Where(link => ownedFileIds.Contains(link.MangaFileId)).ToList();
            var ownedItemIds = links.Select(link => link.MangaItemId).ToHashSet();
            if (candidate.CoveredItems.Count > 0 && candidate.CoveredItems.All(item => ownedItemIds.Contains(item.Id)))
            {
                if (CanUpgrade(policy, match.Release, candidate.CoveredItems, mangaFiles, links))
                {
                    decision.IsUpgrade = true;
                    decision.Evidence.Add($"Source upgrade from imported files toward the {policy.UpgradeCutoffSource} cutoff.");
                }
                else
                {
                    decision.Rejections.Add("All covered manga items already have imported files or meet the upgrade cutoff.");
                }
            }

            if (IsBlocked(policy, release))
            {
                decision.Rejections.Add("Release is blocklisted.");
            }

            CheckAllowed(decision, policy.AllowedLanguages, match.Release?.Language, "language");
            CheckAllowed(decision, policy.AllowedSources, match.Release?.Source, "source");
            CheckAllowed(decision, policy.AllowedEditions, match.Release?.EditionHint, "edition");
            if (!string.IsNullOrWhiteSpace(match.Release?.EditionHint) && policy.AllowedEditions.Count == 0)
            {
                decision.ReviewReasons.Add("Edition variant requires an explicit allowed-edition policy for automatic acquisition.");
            }

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

        private static bool CanUpgrade(
            MangaReleasePolicy policy,
            ParsedMangaReleaseInfo parsed,
            List<MangaItem> covered,
            List<MangaFile> files,
            List<MangaFileItem> links)
        {
            if (policy.SourcePreference.Count == 0 || string.IsNullOrWhiteSpace(policy.UpgradeCutoffSource) ||
                string.IsNullOrWhiteSpace(parsed?.Source))
            {
                return false;
            }

            var preference = policy.SourcePreference.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var releaseRank = preference.FindIndex(value => string.Equals(value, parsed.Source, StringComparison.OrdinalIgnoreCase));
            var cutoffRank = preference.FindIndex(value => string.Equals(value, policy.UpgradeCutoffSource, StringComparison.OrdinalIgnoreCase));
            if (releaseRank <= 0 || cutoffRank < releaseRank)
            {
                return false;
            }

            var byId = files.ToDictionary(file => file.Id);
            foreach (var item in covered)
            {
                var existing = links.Where(link => link.MangaItemId == item.Id).Select(link => byId[link.MangaFileId]).ToList();
                if (existing.Count == 0 || existing.Any(file =>
                    !string.Equals(file.Language, parsed.Language, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(file.EditionLabel, parsed.EditionHint, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                var ranks = existing.Select(file => preference.FindIndex(value =>
                    string.Equals(value, file.Source, StringComparison.OrdinalIgnoreCase))).ToList();
                if (ranks.Any(rank => rank < 0) || ranks.Max() >= releaseRank || ranks.Max() >= cutoffRank)
                {
                    return false;
                }
            }

            return true;
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
