using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Parser.Model;
using MangaModel = NzbDrone.Core.Manga.Manga;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaRssSyncFixture
    {
        [Test]
        public async Task Failing_indexer_does_not_prevent_other_recent_feeds_and_each_is_fetched_once()
        {
            var failed = new Mock<IIndexer>();
            failed.Setup(value => value.FetchRecent()).ThrowsAsync(new InvalidOperationException("Indexer offline"));
            var working = new Mock<IIndexer>();
            working.Setup(value => value.FetchRecent()).ReturnsAsync(new List<ReleaseInfo> { NewRelease("BLAME! v01", "one") });
            var factory = new Mock<IIndexerFactory>();
            factory.Setup(value => value.RssEnabled(true)).Returns(new List<IIndexer> { failed.Object, working.Object });
            var fetcher = new FetchAndParseRssService(factory.Object, LogManager.GetCurrentClassLogger());

            var releases = await fetcher.Fetch();

            releases.Should().ContainSingle().Which.Guid.Should().Be("one");
            failed.Verify(value => value.FetchRecent(), Times.Once());
            working.Verify(value => value.FetchRecent(), Times.Once());
        }

        [Test]
        public void Large_library_uses_bounded_alias_candidates_and_deduplicates_reports()
        {
            var library = Enumerable.Range(1, 4000).Select(id => NewManga(id, $"Title {id}")).ToList();
            library[0].PreferredTitle = "BLAME!";
            var index = new MangaAliasIndex(library);
            index.Find("BLAME!").Candidates.Should().ContainSingle().Which.Id.Should().Be(1);
            index.Find("Title 2").Candidates.Should().ContainSingle().Which.Id.Should().Be(2);
            index.Find("Title 9999").TooBroad.Should().BeTrue();

            var service = NewService(library, new[] { NewItem(1, 10) });
            var release = NewRelease("BLAME! v01", "same-guid");
            var result = service.Process(new[]
            {
                release,
                NewRelease("BLAME! v01", "same-guid"),
                NewRelease("Unknown series v01", "unknown"),
                NewRelease("BLAME! 01", "bare-number")
            });

            result.Reports.Should().Be(4);
            result.Duplicates.Should().Be(1);
            result.Unresolved.Should().Be(1);
            result.Matched.Should().Be(1);
            result.SafeCandidates.Should().Be(1);
            result.Evaluations.Should().ContainSingle();
        }

        [Test]
        public void Shared_alias_stays_ambiguous()
        {
            var first = NewManga(1, "Shared Title");
            var second = NewManga(2, "Another Title");
            second.UserAliases.Add("Shared Title");
            var service = NewService(new[] { first, second }, new[] { NewItem(1, 10), NewItem(2, 20) });

            var result = service.Process(new[] { NewRelease("Shared Title v01", "shared") });

            result.Ambiguous.Should().Be(1);
            result.SafeCandidates.Should().Be(0);
            result.Evaluations.Should().HaveCount(2);
            result.Evaluations.Should().OnlyContain(value => !value.Decision.CanGrabAutomatically);
        }

        [Test]
        public void Blocklisted_release_is_not_a_safe_candidate()
        {
            var manga = NewManga(1, "BLAME!");
            var service = NewService(
                new[] { manga },
                new[] { NewItem(1, 10) },
                new[] { new MangaBlocklist { MangaId = 1, IndexerId = 3, ReleaseGuid = "blocked" } });

            var result = service.Process(new[] { NewRelease("BLAME! v01", "blocked") });

            result.SafeCandidates.Should().Be(0);
            result.Evaluations.Single().Decision.Rejections.Should().Contain(value => value.Contains("blocklisted"));
        }

        private static MangaRssSyncService NewService(
            IEnumerable<MangaModel> manga,
            IEnumerable<MangaItem> items,
            IEnumerable<MangaBlocklist> blocked = null)
        {
            var titles = new Mock<IMangaRepository>();
            titles.Setup(value => value.All()).Returns(manga);
            var knownItems = new Mock<IMangaItemRepository>();
            knownItems.Setup(value => value.All()).Returns(items);
            var files = new Mock<IMangaFileRepository>();
            files.Setup(value => value.All()).Returns(new MangaFile[0]);
            var links = new Mock<IMangaFileItemRepository>();
            links.Setup(value => value.All()).Returns(new MangaFileItem[0]);
            var blocklist = new Mock<IMangaBlocklistRepository>();
            blocklist.Setup(value => value.All()).Returns(blocked ?? new MangaBlocklist[0]);
            return new MangaRssSyncService(
                titles.Object,
                knownItems.Object,
                files.Object,
                links.Object,
                blocklist.Object,
                new MangaReleaseParser(),
                new MangaReleaseMatcher(),
                new MangaReleaseDecisionEngine(),
                LogManager.GetCurrentClassLogger());
        }

        private static MangaModel NewManga(int id, string title)
        {
            return new MangaModel { Id = id, PreferredTitle = title, Monitored = true, TrackingMode = MangaTrackingMode.Volume };
        }

        private static MangaItem NewItem(int mangaId, int id)
        {
            var item = new MangaItem { Id = id, MangaId = mangaId, Type = MangaItemType.Volume, Monitored = true };
            item.SetNumber("1");
            return item;
        }

        private static ReleaseInfo NewRelease(string title, string guid)
        {
            return new TorrentInfo { IndexerId = 3, Title = title, Guid = guid, Size = 1000000, Seeders = 10 };
        }
    }
}
