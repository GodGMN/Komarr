using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Parser.Model;
using MangaModel = NzbDrone.Core.Manga.Manga;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaReleaseDecisionFixture
    {
        private readonly MangaReleaseParser _parser = new ();
        private readonly MangaReleaseMatcher _matcher = new ();
        private readonly MangaReleaseDecisionEngine _engine = new ();

        [Test]
        public void Wrong_title_and_missing_requested_coverage_have_explicit_rejections()
        {
            var manga = NewManga();
            var items = NewItems();
            var release = NewRelease("BLAME! v01");
            var match = _matcher.Match(_parser.Parse(release.Title), new[] { manga }, items, 2);

            var wrongTitle = _engine.Evaluate(manga, match, null, release, null, null, requestedItemId: 2);
            var wrongVolume = _engine.Evaluate(manga, match, match.Candidates.Single(), release, null, null, requestedItemId: 2);

            wrongTitle.Rejections.Should().Contain(x => x.Contains("does not match"));
            wrongVolume.Rejections.Should().Contain(x => x.Contains("requested item"));
            wrongVolume.CanGrabManually.Should().BeFalse();
        }

        [Test]
        public void Disallowed_language_and_container_are_explained()
        {
            var manga = NewManga();
            var release = NewRelease("BLAME! v01 [Japanese].pdf");
            release.Container = "PDF";
            var match = _matcher.Match(_parser.Parse(release.Title), new[] { manga }, NewItems());

            var policy = new MangaReleasePolicy
            {
                AllowedLanguages = new List<string> { "English" },
                AllowedContainers = new List<string> { "CBZ" }
            };
            var decision = _engine.Evaluate(manga, match, match.Candidates.Single(), release, null, null, policy);

            decision.Rejections.Should().Contain(x => x.Contains("language 'Japanese'"));
            decision.Rejections.Should().Contain(x => x.Contains("container 'PDF'"));
        }

        [Test]
        public void Already_imported_volume_is_rejected_but_partial_pack_can_be_reviewed()
        {
            var manga = NewManga();
            var items = NewItems();
            var files = new[] { new MangaFile { Id = 5, MangaId = manga.Id } };
            var links = new[] { new MangaFileItem { MangaFileId = 5, MangaItemId = 1 } };
            var single = NewRelease("BLAME! v01");
            var singleMatch = _matcher.Match(_parser.Parse(single.Title), new[] { manga }, items);
            var pack = NewRelease("BLAME! v01-v02");
            var packMatch = _matcher.Match(_parser.Parse(pack.Title), new[] { manga }, items);

            var owned = _engine.Evaluate(manga, singleMatch, singleMatch.Candidates.Single(), single, files, links);
            var partial = _engine.Evaluate(manga, packMatch, packMatch.Candidates.Single(), pack, files, links);

            owned.Rejections.Should().Contain(x => x.Contains("already have imported files"));
            partial.Rejections.Should().BeEmpty();
            partial.CanGrabAutomatically.Should().BeTrue();
        }

        [Test]
        public void Ambiguous_and_fuzzy_matches_never_auto_grab()
        {
            var manga = NewManga();
            var second = NewManga();
            second.Id = 2;
            second.UserAliases = new List<string> { "BLAME!" };
            second.PreferredTitle = "Another title";
            var release = NewRelease("BLAME! v01");
            var ambiguous = _matcher.Match(_parser.Parse(release.Title), new[] { manga, second }, NewItems());
            var fuzzyRelease = NewRelease("BLAMR! v01");
            var fuzzy = _matcher.Match(_parser.Parse(fuzzyRelease.Title), new[] { manga }, NewItems());

            var ambiguousDecision = _engine.Evaluate(manga, ambiguous, ambiguous.Candidates.Single(x => x.Manga.Id == manga.Id), release, null, null);
            var fuzzyDecision = _engine.Evaluate(manga, fuzzy, fuzzy.Candidates.Single(), fuzzyRelease, null, null);

            ambiguousDecision.CanGrabAutomatically.Should().BeFalse();
            ambiguousDecision.ReviewReasons.Should().Contain(x => x.Contains("Several manga"));
            fuzzyDecision.CanGrabAutomatically.Should().BeFalse();
            fuzzyDecision.ReviewReasons.Should().Contain(x => x.Contains("manual confirmation"));
        }

        [Test]
        public void Policy_checks_blocklist_size_seeders_and_source()
        {
            var manga = NewManga();
            var release = NewRelease("BLAME! v01 (Digital)");
            release.Guid = "blocked-guid";
            release.Size = 500;
            release.Seeders = 1;
            var match = _matcher.Match(_parser.Parse(release.Title), new[] { manga }, NewItems());

            var policy = new MangaReleasePolicy
            {
                BlockedReleaseIds = new List<string> { "blocked-guid" },
                MinimumSizeBytes = 1000,
                MinimumSeeders = 3,
                AllowedSources = new List<string> { "Scanlation" }
            };
            var decision = _engine.Evaluate(manga, match, match.Candidates.Single(), release, null, null, policy);

            decision.Rejections.Should().Contain(x => x.Contains("blocklisted"));
            decision.Rejections.Should().Contain(x => x.Contains("size is below"));
            decision.Rejections.Should().Contain(x => x.Contains("seeders"));
            decision.Rejections.Should().Contain(x => x.Contains("source 'Digital'"));
        }

        [Test]
        public void Saved_policy_requires_review_when_quality_evidence_is_unknown()
        {
            var manga = NewManga();
            manga.QualityPolicy.AllowedLanguages.Add("English");
            manga.QualityPolicy.MinimumCustomFormatScore = 10;
            manga.QualityPolicy.MinimumSeeders = 2;
            var release = NewRelease("BLAME! v01");
            release.Seeders = null;
            var match = _matcher.Match(_parser.Parse(release.Title), new[] { manga }, NewItems());

            var decision = _engine.Evaluate(manga, match, match.Candidates.Single(), release, null, null);

            decision.CanGrabManually.Should().BeTrue();
            decision.CanGrabAutomatically.Should().BeFalse();
            decision.ReviewReasons.Should().Contain(x => x.Contains("language is unknown"));
            decision.ReviewReasons.Should().Contain(x => x.Contains("seeder count is unknown"));
            decision.ReviewReasons.Should().Contain(x => x.Contains("Custom format score is unknown"));
        }

        [Test]
        public void Explicit_source_preference_allows_only_better_imported_quality_until_cutoff()
        {
            var manga = NewManga();
            manga.QualityPolicy.SourcePreference = new List<string> { "Raw", "Scanlation", "Digital" };
            manga.QualityPolicy.UpgradeCutoffSource = "Digital";
            var release = NewRelease("BLAME! v01 (Digital)");
            var items = NewItems();
            var match = _matcher.Match(_parser.Parse(release.Title), new[] { manga }, items);
            var file = new MangaFile { Id = 5, MangaId = 1, Source = "Raw" };
            var links = new[] { new MangaFileItem { MangaFileId = 5, MangaItemId = 1 } };

            var upgrade = _engine.Evaluate(manga, match, match.Candidates.Single(), release, new[] { file }, links);
            file.Source = "Digital";
            var atCutoff = _engine.Evaluate(manga, match, match.Candidates.Single(), release, new[] { file }, links);

            upgrade.IsUpgrade.Should().BeTrue();
            upgrade.CanGrabAutomatically.Should().BeTrue();
            atCutoff.IsUpgrade.Should().BeFalse();
            atCutoff.CanGrabAutomatically.Should().BeFalse();
        }

        [Test]
        public void Edition_variant_requires_explicit_policy_for_automatic_grab()
        {
            var manga = NewManga();
            var release = NewRelease("BLAME! v01 Omnibus");
            var match = _matcher.Match(_parser.Parse(release.Title), new[] { manga }, NewItems());

            var decision = _engine.Evaluate(manga, match, match.Candidates.Single(), release, null, null);

            decision.CanGrabManually.Should().BeTrue();
            decision.CanGrabAutomatically.Should().BeFalse();
            decision.ReviewReasons.Should().Contain(value => value.Contains("Edition variant"));
        }

        private static MangaModel NewManga()
        {
            return new MangaModel { Id = 1, PreferredTitle = "BLAME!", Monitored = true, TrackingMode = MangaTrackingMode.Volume };
        }

        private static MangaItem[] NewItems()
        {
            var first = new MangaItem { Id = 1, MangaId = 1, Type = MangaItemType.Volume, Monitored = true };
            first.SetNumber("1");
            var second = new MangaItem { Id = 2, MangaId = 1, Type = MangaItemType.Volume, Monitored = true };
            second.SetNumber("2");
            return new[] { first, second };
        }

        private static TorrentInfo NewRelease(string title)
        {
            return new TorrentInfo { Title = title, Guid = title, Size = 1000000, Seeders = 10 };
        }
    }
}
