using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Parser.Model;
using MangaModel = NzbDrone.Core.Manga.Manga;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaReleaseDiscoveryFixture
    {
        [Test]
        public void Confident_release_adds_next_wanted_volume_once()
        {
            var manga = NewManga();
            var known = Enumerable.Range(1, 18).Select(number => NewItem(number)).ToList();
            var repository = NewRepository(known);
            var service = new MangaReleaseDiscoveryService(repository.Object, LogManager.GetCurrentClassLogger());
            var message = NewMessage(manga, known, "Series v19");

            service.Handle(message);
            service.Handle(message);

            known.Should().HaveCount(19);
            known.Last().NumberText.Should().Be("19");
            known.Last().DiscoveredFrom.Should().Be(MangaItemDiscoverySource.Release);
            known.Last().Monitored.Should().BeTrue();
            repository.Verify(value => value.Insert(It.IsAny<MangaItem>()), Times.Once());
        }

        [Test]
        public void Ambiguous_edition_and_distant_coverage_require_manual_review()
        {
            var manga = NewManga();
            var known = new List<MangaItem> { NewItem(18) };
            var repository = NewRepository(known);
            var service = new MangaReleaseDiscoveryService(repository.Object, LogManager.GetCurrentClassLogger());

            service.Handle(NewMessage(manga, known, "Series v19 Omnibus"));
            service.Handle(NewMessage(manga, known, "Series v30"));
            service.Handle(NewMessage(manga, known, "Series v19", NewManga(2)));

            known.Should().ContainSingle();
            repository.Verify(value => value.Insert(It.IsAny<MangaItem>()), Times.Never());
        }

        [Test]
        public void Future_monitoring_and_releasing_status_are_required()
        {
            var manga = NewManga();
            var known = new List<MangaItem> { NewItem(18) };
            var repository = NewRepository(known);
            var service = new MangaReleaseDiscoveryService(repository.Object, LogManager.GetCurrentClassLogger());

            manga.MonitorFutureItems = false;
            service.Handle(NewMessage(manga, known, "Series v19"));
            manga.MonitorFutureItems = true;
            manga.Status = "FINISHED";
            service.Handle(NewMessage(manga, known, "Series v19"));

            known.Should().ContainSingle();
        }

        [Test]
        public void Confident_chapter_release_creates_next_chapter()
        {
            var manga = NewManga();
            manga.TrackingMode = MangaTrackingMode.Chapter;
            var chapter = NewItem(18);
            chapter.Type = MangaItemType.Chapter;
            var known = new List<MangaItem> { chapter };
            var repository = NewRepository(known);
            var service = new MangaReleaseDiscoveryService(repository.Object, LogManager.GetCurrentClassLogger());

            service.Handle(NewMessage(manga, known, "Series ch19"));

            known.Should().HaveCount(2);
            known.Last().Type.Should().Be(MangaItemType.Chapter);
            known.Last().NumberText.Should().Be("19");
        }

        private static Mock<IMangaItemRepository> NewRepository(List<MangaItem> known)
        {
            var repository = new Mock<IMangaItemRepository>();
            repository.Setup(value => value.GetByMangaId(1)).Returns(() => known.ToList());
            repository.Setup(value => value.Insert(It.IsAny<MangaItem>())).Returns((MangaItem item) =>
            {
                item.Id = known.Count + 1;
                known.Add(item);
                return item;
            });
            return repository;
        }

        private static MangaRssSyncCompleteEvent NewMessage(
            MangaModel manga,
            List<MangaItem> known,
            string title,
            MangaModel other = null)
        {
            var release = new TorrentInfo { Title = title, Guid = title, Size = 1000000, Seeders = 10 };
            var parsed = new MangaReleaseParser().Parse(title);
            var library = other == null ? new[] { manga } : new[] { manga, other };
            var match = new MangaReleaseMatcher().Match(parsed, library, known);
            var candidate = match.Candidates.First(value => value.Manga.Id == manga.Id);
            var decision = new MangaReleaseDecisionEngine().Evaluate(
                manga,
                match,
                candidate,
                release,
                new MangaFile[0],
                new MangaFileItem[0]);
            return new MangaRssSyncCompleteEvent(new MangaRssSyncResult
            {
                Evaluations = new List<MangaRssReleaseEvaluation>
                {
                    new MangaRssReleaseEvaluation
                    {
                        Release = release,
                        Parsed = parsed,
                        Match = match,
                        Candidate = candidate,
                        Decision = decision
                    }
                }
            });
        }

        private static MangaModel NewManga(int id = 1)
        {
            return new MangaModel
            {
                Id = id,
                PreferredTitle = "Series",
                Monitored = true,
                MonitorFutureItems = true,
                Status = "RELEASING",
                TrackingMode = MangaTrackingMode.Volume
            };
        }

        private static MangaItem NewItem(int number)
        {
            var item = new MangaItem { Id = number, MangaId = 1, Type = MangaItemType.Volume, Monitored = true };
            item.SetNumber(number.ToString());
            return item;
        }
    }
}
