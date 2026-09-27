using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Manga;
using MangaModel = NzbDrone.Core.Manga.Manga;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaWantedFixture
    {
        [Test]
        public void Completed_ten_volume_series_lists_only_unowned_monitored_volumes_as_missing()
        {
            var manga = new MangaModel
            {
                Id = 7, PreferredTitle = "BLAME!", Status = "FINISHED", AniListVolumeCount = 10,
                TrackingMode = MangaTrackingMode.Volume, Monitored = true
            };
            var items = Enumerable.Range(1, 10).Select(number =>
            {
                var item = new MangaItem { Id = number, MangaId = 7, Type = MangaItemType.Volume, Monitored = number != 4 };
                item.SetNumber(number.ToString());
                return item;
            }).ToList();
            var mangaService = new Mock<IMangaService>();
            mangaService.Setup(value => value.Find(7)).Returns(manga);
            mangaService.Setup(value => value.All()).Returns(new[] { manga });
            mangaService.Setup(value => value.GetItems(7)).Returns(items);
            mangaService.Setup(value => value.GetFiles(7)).Returns(new[]
            {
                new MangaFile { Id = 20, MangaId = 7 },
                new MangaFile { Id = 21, MangaId = 7 }
            });
            var coverage = new Mock<IMangaFileItemRepository>();
            coverage.Setup(value => value.GetByFileIds(It.IsAny<IEnumerable<int>>())).Returns(new[]
            {
                new MangaFileItem { MangaFileId = 20, MangaItemId = 1 },
                new MangaFileItem { MangaFileId = 21, MangaItemId = 2 }
            });
            var downloads = new Mock<IMangaDownloadRepository>();
            downloads.Setup(value => value.GetByMangaId(7)).Returns(new[]
            {
                new MangaDownload { MangaId = 7, Status = MangaDownloadStatus.Sent, CoveredItemIds = new List<int> { 3 } },
                new MangaDownload { MangaId = 7, Status = MangaDownloadStatus.Failed, CoveredItemIds = new List<int> { 5 } }
            });
            var service = new MangaWantedService(mangaService.Object, coverage.Object, downloads.Object);

            var all = service.GetForManga(7);
            var missing = service.GetMissing();

            all.Should().HaveCount(10);
            missing.Select(item => item.NumberText).Should().Equal("3", "5", "6", "7", "8", "9", "10");
            missing.Single(item => item.NumberText == "3").InProgress.Should().BeTrue();
            missing.Single(item => item.NumberText == "5").InProgress.Should().BeFalse();
            all.Single(item => item.NumberText == "4").Missing.Should().BeFalse();
            all.Count(item => item.Owned).Should().Be(2);
        }

        [Test]
        public void Unmonitored_manga_does_not_enter_global_wanted_list()
        {
            var manga = new MangaModel { Id = 7, Monitored = false };
            var mangaService = new Mock<IMangaService>();
            mangaService.Setup(value => value.All()).Returns(new[] { manga });
            var service = new MangaWantedService(
                mangaService.Object,
                new Mock<IMangaFileItemRepository>().Object,
                new Mock<IMangaDownloadRepository>().Object);

            service.GetMissing().Should().BeEmpty();
        }
    }
}
