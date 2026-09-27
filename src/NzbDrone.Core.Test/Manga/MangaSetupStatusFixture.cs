using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentValidation.Results;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Backup;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Manga;
using NzbDrone.Core.MetadataSource.AniList;
using NzbDrone.Core.RemotePathMappings;
using NzbDrone.Core.RootFolders;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaSetupStatusFixture
    {
        private Mock<IMangaRepository> _database;
        private Mock<IRootFolderService> _roots;
        private Mock<IIndexerFactory> _indexers;
        private Mock<IProvideDownloadClient> _clients;
        private Mock<IRemotePathMappingService> _remotePaths;
        private Mock<IAniListMetadataClient> _anilist;
        private Mock<IBackupService> _backups;
        private Mock<IDiskProvider> _disk;
        private Mock<IConfigFileProvider> _config;
        private MangaSetupStatusService _service;

        [SetUp]
        public void SetUp()
        {
            _database = new Mock<IMangaRepository>();
            _roots = new Mock<IRootFolderService>();
            _roots.Setup(value => value.All()).Returns(new List<RootFolder> { new RootFolder { Path = "/manga" } });
            _indexers = new Mock<IIndexerFactory>();
            var indexer = new Mock<IIndexer>();
            indexer.Setup(value => value.Test()).Returns(new ValidationResult());
            _indexers.Setup(value => value.RssEnabled(false)).Returns(new List<IIndexer> { indexer.Object });
            _indexers.Setup(value => value.AutomaticSearchEnabled(false)).Returns(new List<IIndexer> { indexer.Object });
            _clients = new Mock<IProvideDownloadClient>();
            var client = new Mock<IDownloadClient>();
            client.Setup(value => value.Test()).Returns(new ValidationResult());
            _clients.Setup(value => value.GetDownloadClients(false)).Returns(new[] { client.Object });
            _remotePaths = new Mock<IRemotePathMappingService>();
            _remotePaths.Setup(value => value.All()).Returns(new List<RemotePathMapping>());
            _anilist = new Mock<IAniListMetadataClient>();
            _anilist.Setup(value => value.Search("BLAME!")).Returns(new AniListResult { Availability = AniListAvailability.Available });
            _backups = new Mock<IBackupService>();
            _backups.Setup(value => value.GetBackups()).Returns(new List<NzbDrone.Core.Backup.Backup>());
            _disk = new Mock<IDiskProvider>();
            _disk.Setup(value => value.FolderExists("/manga")).Returns(true);
            _disk.Setup(value => value.GetAvailableSpace("/manga")).Returns(2L * 1024 * 1024 * 1024);
            _config = new Mock<IConfigFileProvider>();
            _service = new MangaSetupStatusService(
                _database.Object,
                _roots.Object,
                _indexers.Object,
                _clients.Object,
                _remotePaths.Object,
                _anilist.Object,
                _backups.Object,
                _disk.Object,
                _config.Object);
        }

        [Test]
        public void AniList_outage_is_a_warning_and_does_not_disable_local_operation()
        {
            _anilist.Setup(value => value.Search("BLAME!")).Returns(new AniListResult { Availability = AniListAvailability.Unavailable });

            var status = _service.GetStatus();

            status.ReadyForLocalUse.Should().BeTrue();
            status.Checks.Single(value => value.Key == "anilist").State.Should().Be("warning");
            status.Checks.Single(value => value.Key == "backups").State.Should().Be("warning");
            status.BackupWarning.Should().Contain("credentials");
            status.Checks.Single(value => value.Key == "authentication").State.Should().Be("warning");
        }

        [Test]
        public void Missing_database_root_indexer_and_client_are_actionable()
        {
            _database.Setup(value => value.Find(0)).Throws(new InvalidOperationException("database unavailable"));
            _roots.Setup(value => value.All()).Returns(new List<RootFolder> { new RootFolder { Path = "/missing" } });
            _indexers.Setup(value => value.RssEnabled(false)).Returns(new List<IIndexer>());
            _clients.Setup(value => value.GetDownloadClients(false)).Returns(new IDownloadClient[0]);

            var status = _service.GetStatus();

            status.ReadyForLocalUse.Should().BeFalse();
            status.Checks.Single(value => value.Key == "database").State.Should().Be("error");
            status.Checks.Single(value => value.Key == "roots").State.Should().Be("error");
            status.Checks.Single(value => value.Key == "indexers").Link.Should().Be("/settings/indexers");
            status.Checks.Single(value => value.Key == "clients").State.Should().Be("action");
        }

        [Test]
        public void Bad_remote_path_is_reported_without_exposing_secret_settings()
        {
            _remotePaths.Setup(value => value.All()).Returns(new List<RemotePathMapping>
            {
                new RemotePathMapping { Host = "client", RemotePath = "/downloads", LocalPath = "/missing/downloads" }
            });

            var status = _service.GetStatus();

            status.ReadyForLocalUse.Should().BeFalse();
            status.Checks.Single(value => value.Key == "remotePaths").State.Should().Be("error");
            status.Checks.Select(value => value.Message).Should().NotContain(value => value.Contains("/missing/downloads"));
        }
    }
}
