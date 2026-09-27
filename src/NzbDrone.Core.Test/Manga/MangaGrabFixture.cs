using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients.QBittorrent;
using NzbDrone.Core.Download.Clients.Sabnzbd;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser.Model;
using MangaModel = NzbDrone.Core.Manga.Manga;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaGrabFixture
    {
        private Mock<IMangaService> _manga;
        private Mock<IMangaIndexerSearchService> _indexers;
        private Mock<IMangaDownloadRepository> _downloads;
        private Mock<IProvideDownloadClient> _clients;
        private Mock<IDownloadClientStatusService> _clientStatus;
        private Mock<IEventAggregator> _events;
        private MangaModel _savedManga;
        private MangaItem _volume;
        private MangaGrabService _service;

        [SetUp]
        public void SetUp()
        {
            _savedManga = new MangaModel { Id = 7, PreferredTitle = "BLAME!", Monitored = true };
            _volume = new MangaItem { Id = 10, MangaId = 7, Type = MangaItemType.Volume, Monitored = true };
            _volume.SetNumber("1");
            _manga = new Mock<IMangaService>();
            _manga.Setup(x => x.Find(7)).Returns(_savedManga);
            _manga.Setup(x => x.All()).Returns(new[] { _savedManga });
            _manga.Setup(x => x.GetItems(7)).Returns(new[] { _volume });
            _manga.Setup(x => x.GetFiles(7)).Returns(new MangaFile[0]);
            _indexers = new Mock<IMangaIndexerSearchService>();
            _downloads = new Mock<IMangaDownloadRepository>();
            _downloads.Setup(x => x.GetByMangaId(7)).Returns(new MangaDownload[0]);
            _downloads.Setup(x => x.Insert(It.IsAny<MangaDownload>())).Returns<MangaDownload>(download => download);
            _downloads.Setup(x => x.Update(It.IsAny<MangaDownload>())).Returns<MangaDownload>(download => download);
            _clients = new Mock<IProvideDownloadClient>();
            _clientStatus = new Mock<IDownloadClientStatusService>();
            _events = new Mock<IEventAggregator>();
            var fileItems = new Mock<IMangaFileItemRepository>();
            fileItems.Setup(x => x.GetByFileIds(It.IsAny<IEnumerable<int>>())).Returns(new MangaFileItem[0]);
            _service = new MangaGrabService(
                _manga.Object,
                fileItems.Object,
                _downloads.Object,
                _indexers.Object,
                new MangaReleaseParser(),
                new MangaReleaseMatcher(),
                new MangaReleaseDecisionEngine(),
                _clients.Object,
                _clientStatus.Object,
                new Mock<IIndexerFactory>().Object,
                _events.Object,
                LogManager.GetCurrentClassLogger());
        }

        [TestCase(DownloadProtocol.Torrent, "qBittorrent", "torrent-hash")]
        [TestCase(DownloadProtocol.Usenet, "SABnzbd", "nzo-id")]
        public async Task Sends_selected_manga_release_and_tracks_client_id(DownloadProtocol protocol, string clientName, string trackingId)
        {
            var release = NewRelease("BLAME! v01 [English]", protocol);
            SetupRelease(release);
            var client = SetupClient(protocol, clientName);
            client.Setup(x => x.Download(It.IsAny<RemoteBook>(), null)).ReturnsAsync(trackingId);

            var result = await _service.Grab(7, NewRequest(release));

            result.DownloadId.Should().Be(trackingId);
            result.Status.Should().Be(MangaDownloadStatus.Sent);
            result.CoveredItemIds.Should().Equal(10);
            client.Verify(x => x.Download(It.Is<RemoteBook>(book =>
                book.Release == release && book.ReleaseSource == ReleaseSourceType.InteractiveSearch), null), Times.Once());
            _downloads.Verify(x => x.Update(It.Is<MangaDownload>(download => download.DownloadId == trackingId)), Times.Once());
            _clientStatus.Verify(x => x.RecordSuccess(2), Times.Once());
        }

        [Test]
        public async Task Fuzzy_result_requires_explicit_manual_confirmation()
        {
            var release = NewRelease("BLAMR! v01", DownloadProtocol.Torrent);
            SetupRelease(release);
            var client = SetupClient(DownloadProtocol.Torrent, "qBittorrent");
            client.Setup(x => x.Download(It.IsAny<RemoteBook>(), null)).ReturnsAsync("torrent-hash");
            var request = NewRequest(release);

            Func<Task> unconfirmed = async () => await _service.Grab(7, request);
            await unconfirmed.Should().ThrowAsync<MangaGrabValidationException>().WithMessage("*confirm*");
            client.Verify(x => x.Download(It.IsAny<RemoteBook>(), null), Times.Never());

            request.ConfirmManualReview = true;
            var result = await _service.Grab(7, request);

            result.Status.Should().Be(MangaDownloadStatus.Sent);
        }

        [Test]
        public async Task Does_not_trust_a_release_guid_without_a_fresh_indexer_result()
        {
            _indexers.Setup(x => x.Search(_savedManga, true)).ReturnsAsync(new MangaIndexerSearchResult());

            Func<Task> action = async () => await _service.Grab(7, new MangaGrabRequest
            {
                Guid = "invented",
                Title = "BLAME! v01",
                IndexerId = 0
            });

            await action.Should().ThrowAsync<MangaGrabValidationException>().WithMessage("*no longer available*");
            _downloads.Verify(x => x.Insert(It.IsAny<MangaDownload>()), Times.Never());
        }

        [Test]
        public async Task Client_failure_is_recorded_without_storing_credential_text()
        {
            var release = NewRelease("BLAME! v01", DownloadProtocol.Usenet);
            SetupRelease(release);
            var client = SetupClient(DownloadProtocol.Usenet, "SABnzbd");
            client.Setup(x => x.Download(It.IsAny<RemoteBook>(), null))
                .ThrowsAsync(new InvalidOperationException("apikey=private-value"));
            MangaDownload stored = null;
            _downloads.Setup(x => x.Update(It.IsAny<MangaDownload>())).Returns<MangaDownload>(download =>
            {
                stored = download;
                return download;
            });

            Func<Task> action = async () => await _service.Grab(7, NewRequest(release));

            await action.Should().ThrowAsync<InvalidOperationException>();
            stored.Status.Should().Be(MangaDownloadStatus.Failed);
            stored.Error.Should().NotContain("private-value");
            _clientStatus.Verify(x => x.RecordFailure(2), Times.Once());
        }

        [Test]
        public async Task Already_sent_release_cannot_be_sent_twice()
        {
            var release = NewRelease("BLAME! v01", DownloadProtocol.Torrent);
            SetupRelease(release);
            var client = SetupClient(DownloadProtocol.Torrent, "qBittorrent");
            _downloads.Setup(x => x.GetByMangaId(7)).Returns(new[]
            {
                new MangaDownload { MangaId = 7, IndexerId = 0, ReleaseGuid = release.Guid, Status = MangaDownloadStatus.Sent }
            });

            Func<Task> action = async () => await _service.Grab(7, NewRequest(release));

            await action.Should().ThrowAsync<MangaGrabValidationException>().WithMessage("*already sent*");
            client.Verify(x => x.Download(It.IsAny<RemoteBook>(), null), Times.Never());
        }

        [Test]
        public void New_client_settings_use_a_komarr_category()
        {
            new QBittorrentSettings().MusicCategory.Should().Be("komarr");
            new SabnzbdSettings().MusicCategory.Should().Be("komarr");
        }

        private void SetupRelease(ReleaseInfo release)
        {
            _indexers.Setup(x => x.Search(_savedManga, true)).ReturnsAsync(new MangaIndexerSearchResult
            {
                Releases = new List<ReleaseInfo> { release }
            });
        }

        private Mock<IDownloadClient> SetupClient(DownloadProtocol protocol, string name)
        {
            var client = new Mock<IDownloadClient>();
            client.SetupGet(x => x.Protocol).Returns(protocol);
            client.SetupGet(x => x.Definition).Returns(new DownloadClientDefinition { Id = 2, Name = name });
            _clients.Setup(x => x.GetDownloadClient(protocol, 0, true, It.IsAny<HashSet<int>>())).Returns(client.Object);
            return client;
        }

        private static MangaGrabRequest NewRequest(ReleaseInfo release)
        {
            return new MangaGrabRequest { Guid = release.Guid, Title = release.Title, IndexerId = release.IndexerId, ItemId = 10 };
        }

        private static ReleaseInfo NewRelease(string title, DownloadProtocol protocol)
        {
            return protocol == DownloadProtocol.Torrent
                ? new TorrentInfo { Title = title, Guid = title, DownloadProtocol = protocol, Seeders = 5, Size = 100000 }
                : new ReleaseInfo { Title = title, Guid = title, DownloadProtocol = protocol, Size = 100000 };
        }
    }
}
