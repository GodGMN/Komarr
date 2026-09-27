using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using MangaModel = NzbDrone.Core.Manga.Manga;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaDownloadFileFixture
    {
        private Mock<IMangaService> _manga;
        private Mock<IDiskProvider> _disk;
        private MangaDownloadFileIdentifier _identifier;
        private MangaDownload _download;
        private List<MangaItem> _volumes;

        [SetUp]
        public void SetUp()
        {
            var manga = new MangaModel { Id = 7, PreferredTitle = "BLAME!", TrackingMode = MangaTrackingMode.Volume };
            _volumes = Enumerable.Range(1, 11).Select(number =>
            {
                var item = new MangaItem { Id = number, MangaId = 7, Type = MangaItemType.Volume };
                item.SetNumber(number.ToString());
                return item;
            }).ToList();
            _manga = new Mock<IMangaService>();
            _manga.Setup(service => service.Find(7)).Returns(manga);
            _manga.Setup(service => service.All()).Returns(new[] { manga });
            _manga.Setup(service => service.GetItems(7)).Returns(_volumes);
            _disk = new Mock<IDiskProvider>();
            _disk.Setup(disk => disk.GetFileSize(It.IsAny<string>())).Returns(100);
            _identifier = new MangaDownloadFileIdentifier(_manga.Object, new MangaReleaseParser(), new MangaReleaseMatcher(), _disk.Object);
            _download = new MangaDownload { Id = 3, MangaId = 7, DownloadClientId = 2, DownloadId = "torrent-hash", Status = MangaDownloadStatus.Sent };
        }

        [Test]
        public void Eleven_file_pack_maps_each_volume_independently()
        {
            var paths = Enumerable.Range(1, 11).Select(number => $"/downloads/BLAME!/BLAME! v{number:00}.cbz");
            var files = _identifier.Identify(_download, paths);
            files.Should().HaveCount(11);
            files.Should().OnlyContain(file => file.Status == MangaDownloadFileStatus.Ready && file.CoveredItemIds.Count == 1);
            files.SelectMany(file => file.CoveredItemIds).Should().BeEquivalentTo(_volumes.Select(item => item.Id));
        }

        [Test]
        public void Range_file_covers_all_known_volumes()
        {
            var file = _identifier.Identify(_download, new[] { "/downloads/BLAME! v01-v11.epub" }).Single();
            file.Status.Should().Be(MangaDownloadFileStatus.Ready);
            file.CoveredItemIds.Should().Equal(Enumerable.Range(1, 11));
        }

        [Test]
        public void Ambiguous_and_unsupported_files_are_reported_without_ready_coverage()
        {
            var files = _identifier.Identify(_download, new[]
            {
                "/downloads/BLAME! 02.cbz",
                "/downloads/notes.txt",
                "/downloads/Other Series v03.pdf"
            });
            files[0].Status.Should().Be(MangaDownloadFileStatus.ManualReview);
            files[0].Reason.Should().NotBeNullOrWhiteSpace();
            files[1].Status.Should().Be(MangaDownloadFileStatus.Unsupported);
            files[1].Reason.Should().Contain("Unsupported");
            files[2].Status.Should().Be(MangaDownloadFileStatus.ManualReview);
            files.Should().OnlyContain(file => file.CoveredItemIds.Count == 0);
        }

        [Test]
        public void Overlapping_files_require_manual_review()
        {
            var files = _identifier.Identify(_download, new[]
            {
                "/downloads/BLAME! v01.cbz",
                "/downloads/BLAME! v01-v02.zip"
            });
            files.Should().OnlyContain(file => file.Status == MangaDownloadFileStatus.ManualReview);
            files.Should().OnlyContain(file => file.Reason.Contains("same manga item"));
        }

        [Test]
        public void Completed_client_item_scans_multifile_download_and_keeps_client_item()
        {
            var downloads = new Mock<IMangaDownloadRepository>();
            downloads.Setup(repository => repository.GetSent()).Returns(new[] { _download });
            downloads.Setup(repository => repository.Update(It.IsAny<MangaDownload>())).Returns<MangaDownload>(value => value);
            var files = new Mock<IMangaDownloadFileRepository>();
            files.Setup(repository => repository.GetByDownloadId(3)).Returns(new MangaDownloadFile[0]);
            var client = new Mock<IDownloadClient>();
            client.Setup(value => value.GetItems()).Returns(new[]
            {
                new DownloadClientItem
                {
                    DownloadId = "torrent-hash",
                    Status = DownloadItemStatus.Completed,
                    OutputPath = new OsPath("/downloads/BLAME!")
                }
            });
            var clients = new Mock<IProvideDownloadClient>();
            clients.Setup(value => value.Get(2)).Returns(client.Object);
            _disk.Setup(value => value.FolderExists("/downloads/BLAME!")).Returns(true);
            _disk.Setup(value => value.GetFiles("/downloads/BLAME!", true)).Returns(new[]
            {
                "/downloads/BLAME!/BLAME! v01.cbz",
                "/downloads/BLAME!/BLAME! v02.cbr"
            });
            var monitor = new MangaDownloadMonitor(
                downloads.Object,
                files.Object,
                _identifier,
                new Mock<IMangaImportService>().Object,
                new Mock<IMangaHistoryService>().Object,
                new Mock<IMangaBlocklistService>().Object,
                clients.Object,
                _disk.Object,
                LogManager.GetCurrentClassLogger());
            monitor.Handle(new TrackedDownloadRefreshedEvent(new List<TrackedDownload>()));
            files.Verify(repository => repository.Insert(It.Is<MangaDownloadFile>(file => file.Status == MangaDownloadFileStatus.Ready)), Times.Exactly(2));
            _download.Status.Should().Be(MangaDownloadStatus.Completed);
            client.Verify(value => value.RemoveItem(It.IsAny<DownloadClientItem>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void Inherited_book_monitor_skips_registered_manga_downloads()
        {
            var item = new DownloadClientItem { DownloadId = "torrent-hash", Status = DownloadItemStatus.Completed };
            var client = new Mock<IDownloadClient>();
            client.SetupGet(value => value.Definition).Returns(new DownloadClientDefinition { Id = 2, Name = "qBittorrent" });
            client.Setup(value => value.GetItems()).Returns(new[] { item });
            var factory = new Mock<IDownloadClientFactory>();
            factory.Setup(value => value.DownloadHandlingEnabled(true)).Returns(new List<IDownloadClient> { client.Object });
            var downloads = new Mock<IMangaDownloadRepository>();
            downloads.Setup(value => value.FindByDownloadId(2, "torrent-hash")).Returns(_download);
            var tracked = new Mock<ITrackedDownloadService>();
            var monitor = new DownloadMonitoringService(
                new Mock<IDownloadClientStatusService>().Object,
                factory.Object,
                new Mock<IEventAggregator>().Object,
                new Mock<IManageCommandQueue>().Object,
                new Mock<IConfigService>().Object,
                new Mock<IFailedDownloadService>().Object,
                new Mock<ICompletedDownloadService>().Object,
                tracked.Object,
                downloads.Object,
                LogManager.GetCurrentClassLogger());

            monitor.Execute(new RefreshMonitoredDownloadsCommand());

            tracked.Verify(value => value.TrackDownload(It.IsAny<DownloadClientDefinition>(), It.IsAny<DownloadClientItem>()), Times.Never());
            client.Verify(value => value.RemoveItem(It.IsAny<DownloadClientItem>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void Failed_client_download_is_visible_and_blocklisted_without_importing_items()
        {
            var downloads = new Mock<IMangaDownloadRepository>();
            downloads.Setup(value => value.GetSent()).Returns(new[] { _download });
            var files = new Mock<IMangaDownloadFileRepository>();
            var client = new Mock<IDownloadClient>();
            client.Setup(value => value.GetItems()).Returns(new[]
            {
                new DownloadClientItem { DownloadId = "torrent-hash", Status = DownloadItemStatus.Failed }
            });
            var clients = new Mock<IProvideDownloadClient>();
            clients.Setup(value => value.Get(2)).Returns(client.Object);
            var history = new Mock<IMangaHistoryService>();
            var blocklist = new Mock<IMangaBlocklistService>();
            var monitor = new MangaDownloadMonitor(
                downloads.Object,
                files.Object,
                _identifier,
                new Mock<IMangaImportService>().Object,
                history.Object,
                blocklist.Object,
                clients.Object,
                _disk.Object,
                LogManager.GetCurrentClassLogger());

            monitor.Handle(new TrackedDownloadRefreshedEvent(new List<TrackedDownload>()));

            _download.Status.Should().Be(MangaDownloadStatus.Failed);
            downloads.Verify(value => value.Update(_download), Times.Once());
            blocklist.Verify(value => value.Block(_download, It.IsAny<string>()), Times.Once());
            history.Verify(value => value.Record(_download, MangaHistoryEventType.DownloadFailed, It.IsAny<string>(), null), Times.Once());
            files.Verify(value => value.Insert(It.IsAny<MangaDownloadFile>()), Times.Never());
        }
    }
}
