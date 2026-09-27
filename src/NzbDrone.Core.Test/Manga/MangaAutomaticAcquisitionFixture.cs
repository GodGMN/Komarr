using System.Collections.Generic;
using System.Linq;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Parser.Model;
using MangaModel = NzbDrone.Core.Manga.Manga;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaAutomaticAcquisitionFixture
    {
        [Test]
        public void Feed_grabs_one_safe_missing_item_and_suppresses_overlapping_reports()
        {
            var manga = NewManga();
            var item = NewItem();
            var first = Evaluate(manga, item, "BLAME! v19", "first");
            var second = Evaluate(manga, item, "BLAME! v19 [English]", "second");
            var wanted = NewWanted(item, true);
            var grab = new Mock<IMangaGrabService>();
            grab.Setup(value => value.GrabAutomatic(7, It.IsAny<ReleaseInfo>(), null)).ReturnsAsync(new MangaDownload());
            var service = NewService(manga, wanted.Object, grab.Object);

            service.Handle(new MangaRssSyncCompleteEvent(new MangaRssSyncResult
            {
                Evaluations = new List<MangaRssReleaseEvaluation> { first, second }
            }));

            grab.Verify(value => value.GrabAutomatic(7, It.IsAny<ReleaseInfo>(), null), Times.Once());
        }

        [Test]
        public void Ambiguous_or_owned_coverage_never_autograbs()
        {
            var manga = NewManga();
            var item = NewItem();
            var ambiguous = Evaluate(manga, item, "BLAME! v19", "shared", NewManga(8));
            var exact = Evaluate(manga, item, "BLAME! v19", "owned");
            var wanted = NewWanted(item, false);
            var grab = new Mock<IMangaGrabService>();
            var service = NewService(manga, wanted.Object, grab.Object);

            service.Handle(new MangaRssSyncCompleteEvent(new MangaRssSyncResult
            {
                Evaluations = new List<MangaRssReleaseEvaluation> { ambiguous, exact }
            }));

            grab.Verify(value => value.GrabAutomatic(It.IsAny<int>(), It.IsAny<ReleaseInfo>(), null), Times.Never());
        }

        [Test]
        public void Scheduled_search_uses_automatic_indexers_for_missing_manga()
        {
            var manga = NewManga();
            var item = NewItem();
            var evaluation = Evaluate(manga, item, "BLAME! v19", "search-result");
            var wanted = NewWanted(item, true);
            var grab = new Mock<IMangaGrabService>();
            grab.Setup(value => value.GrabAutomatic(7, It.IsAny<ReleaseInfo>(), null)).ReturnsAsync(new MangaDownload());
            var indexers = new Mock<IMangaIndexerSearchService>();
            indexers.Setup(value => value.Search(manga, false)).ReturnsAsync(new MangaIndexerSearchResult
            {
                Releases = new List<ReleaseInfo> { evaluation.Release }
            });
            var rss = new Mock<IMangaRssSyncService>();
            rss.Setup(value => value.Process(It.IsAny<IEnumerable<ReleaseInfo>>())).Returns(new MangaRssSyncResult
            {
                Evaluations = new List<MangaRssReleaseEvaluation> { evaluation }
            });
            var service = NewService(manga, wanted.Object, grab.Object, indexers.Object, rss.Object);

            service.Execute(new MangaSearchMissingCommand());

            indexers.Verify(value => value.Search(manga, false), Times.Once());
            grab.Verify(value => value.GrabAutomatic(7, evaluation.Release, null), Times.Once());
        }

        [Test]
        public void Explicitly_approved_upgrade_can_grab_owned_item_once()
        {
            var manga = NewManga();
            var item = NewItem();
            var evaluation = Evaluate(manga, item, "BLAME! v19 (Digital)", "upgrade");
            evaluation.Decision.IsUpgrade = true;
            var wanted = NewWanted(item, false);
            wanted.Object.GetForManga(7).Single().Monitored = true;
            var grab = new Mock<IMangaGrabService>();
            grab.Setup(value => value.GrabAutomatic(7, It.IsAny<ReleaseInfo>(), null)).ReturnsAsync(new MangaDownload());
            var service = NewService(manga, wanted.Object, grab.Object);

            service.Handle(new MangaRssSyncCompleteEvent(new MangaRssSyncResult
            {
                Evaluations = new List<MangaRssReleaseEvaluation> { evaluation, evaluation }
            }));

            grab.Verify(value => value.GrabAutomatic(7, evaluation.Release, null), Times.Once());
        }

        private static MangaAutomaticAcquisitionService NewService(
            MangaModel manga,
            IMangaWantedService wanted,
            IMangaGrabService grab,
            IMangaIndexerSearchService indexers = null,
            IMangaRssSyncService rss = null)
        {
            var titles = new Mock<IMangaService>();
            titles.Setup(value => value.Find(manga.Id)).Returns(manga);
            titles.Setup(value => value.All()).Returns(new[] { manga });
            return new MangaAutomaticAcquisitionService(
                titles.Object,
                wanted,
                indexers ?? new Mock<IMangaIndexerSearchService>().Object,
                rss ?? new Mock<IMangaRssSyncService>().Object,
                grab,
                LogManager.GetCurrentClassLogger());
        }

        private static Mock<IMangaWantedService> NewWanted(MangaItem item, bool missing)
        {
            var wanted = new Mock<IMangaWantedService>();
            var state = new MangaWantedItem
            {
                MangaId = item.MangaId,
                ItemId = item.Id,
                Monitored = missing,
                Owned = !missing,
                InProgress = false
            };
            wanted.Setup(value => value.GetForManga(item.MangaId)).Returns(new List<MangaWantedItem> { state });
            wanted.Setup(value => value.GetMissing()).Returns(new List<MangaWantedItem> { state }.Where(value => value.Missing).ToList());
            return wanted;
        }

        private static MangaRssReleaseEvaluation Evaluate(
            MangaModel manga,
            MangaItem item,
            string title,
            string guid,
            MangaModel other = null)
        {
            var release = new TorrentInfo { Title = title, Guid = guid, Size = 1000000, Seeders = 10 };
            var parsed = new MangaReleaseParser().Parse(title);
            var library = other == null ? new[] { manga } : new[] { manga, other };
            var match = new MangaReleaseMatcher().Match(parsed, library, new[] { item });
            var candidate = match.Candidates.First(value => value.Manga.Id == manga.Id);
            return new MangaRssReleaseEvaluation
            {
                Release = release,
                Parsed = parsed,
                Match = match,
                Candidate = candidate,
                Decision = new MangaReleaseDecisionEngine().Evaluate(
                    manga,
                    match,
                    candidate,
                    release,
                    new MangaFile[0],
                    new MangaFileItem[0])
            };
        }

        private static MangaModel NewManga(int id = 7)
        {
            return new MangaModel { Id = id, PreferredTitle = "BLAME!", Monitored = true, TrackingMode = MangaTrackingMode.Volume };
        }

        private static MangaItem NewItem()
        {
            var item = new MangaItem { Id = 19, MangaId = 7, Type = MangaItemType.Volume, Monitored = true };
            item.SetNumber("19");
            return item;
        }
    }
}
