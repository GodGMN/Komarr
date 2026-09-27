using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Manga;
using MangaModel = NzbDrone.Core.Manga.Manga;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaImportFixture
    {
        private const string Source = "/downloads/BLAME! v01.cbz";
        private const string Target = "/library/BLAME!/BLAME! - v01.cbz";

        private Mock<IMangaService> _manga;
        private Mock<IMangaFileRepository> _libraryFiles;
        private Mock<IMangaFileItemRepository> _coverage;
        private Mock<IMangaDownloadFileRepository> _downloadFiles;
        private Mock<IDiskProvider> _disk;
        private Mock<IConfigService> _config;
        private MangaImportService _service;
        private MangaDownload _download;
        private MangaDownloadFile _file;
        private MangaModel _savedManga;

        [SetUp]
        public void SetUp()
        {
            _savedManga = new MangaModel { Id = 7, PreferredTitle = "BLAME!", RootFolderPath = "/library" };
            var item = new MangaItem { Id = 11, MangaId = 7, Type = MangaItemType.Volume };
            item.SetNumber("1");
            _manga = new Mock<IMangaService>();
            _manga.Setup(value => value.Find(7)).Returns(_savedManga);
            _manga.Setup(value => value.GetItems(7)).Returns(new[] { item });
            _manga.Setup(value => value.GetFiles(7)).Returns(new MangaFile[0]);
            _libraryFiles = new Mock<IMangaFileRepository>();
            _libraryFiles.Setup(value => value.Insert(It.IsAny<MangaFile>())).Returns<MangaFile>(file =>
            {
                file.Id = 43;
                return file;
            });
            _coverage = new Mock<IMangaFileItemRepository>();
            _coverage.Setup(value => value.GetByFileIds(It.IsAny<IEnumerable<int>>())).Returns(new MangaFileItem[0]);
            _downloadFiles = new Mock<IMangaDownloadFileRepository>();
            _file = new MangaDownloadFile
            {
                Id = 3,
                MangaDownloadId = 2,
                Path = Source,
                Status = MangaDownloadFileStatus.Ready,
                CoveredItemIds = new List<int> { 11 }
            };
            _downloadFiles.Setup(value => value.GetByDownloadId(2)).Returns(new[] { _file });
            _disk = new Mock<IDiskProvider>();
            _disk.Setup(value => value.FolderExists("/library")).Returns(true);
            _disk.Setup(value => value.FileExists(Source)).Returns(true);
            _disk.Setup(value => value.GetFileSize(Target)).Returns(100);
            _disk.Setup(value => value.FileGetLastWrite(Target)).Returns(DateTime.UtcNow);
            _config = new Mock<IConfigService>();
            _config.SetupGet(value => value.CopyUsingHardlinks).Returns(true);
            _download = new MangaDownload { Id = 2, MangaId = 7, Protocol = DownloadProtocol.Torrent };
            _service = new MangaImportService(
                _manga.Object,
                _libraryFiles.Object,
                _coverage.Object,
                _downloadFiles.Object,
                _disk.Object,
                _config.Object,
                LogManager.GetCurrentClassLogger());
        }

        [Test]
        public void Torrent_uses_hardlink_and_records_item_coverage_without_touching_source()
        {
            _disk.Setup(value => value.TryCreateHardLink(Source, Target)).Returns(true);

            _service.ImportReady(_download).Should().Be(1);

            _disk.Verify(value => value.TryCreateHardLink(Source, Target), Times.Once());
            _disk.Verify(value => value.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
            _disk.Verify(value => value.MoveFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
            _disk.Verify(value => value.DeleteFile(Source), Times.Never());
            _libraryFiles.Verify(value => value.Insert(It.Is<MangaFile>(file => file.Path == Target && file.OriginalFilePath == Source)), Times.Once());
            _coverage.Verify(value => value.Insert(It.Is<MangaFileItem>(link => link.MangaFileId == 43 && link.MangaItemId == 11)), Times.Once());
            _file.Status.Should().Be(MangaDownloadFileStatus.Imported);
            _file.MangaFileId.Should().Be(43);
        }

        [Test]
        public void Failed_hardlink_falls_back_to_copy_and_preserves_torrent_payload()
        {
            _disk.Setup(value => value.TryCreateHardLink(Source, Target)).Returns(false);

            _service.ImportReady(_download).Should().Be(1);

            _disk.Verify(value => value.CopyFile(Source, Target, false), Times.Once());
            _disk.Verify(value => value.MoveFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
            _disk.Verify(value => value.DeleteFile(Source), Times.Never());
        }

        [Test]
        public void Folder_outside_configured_root_is_rejected_before_transfer()
        {
            _savedManga.Path = "/elsewhere/BLAME!";

            _service.ImportReady(_download).Should().Be(0);

            _file.Status.Should().Be(MangaDownloadFileStatus.ManualReview);
            _file.Reason.Should().Contain("outside");
            _disk.Verify(value => value.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void Existing_destination_is_reported_instead_of_overwritten()
        {
            _disk.Setup(value => value.FileExists(Target)).Returns(true);

            _service.ImportReady(_download).Should().Be(0);

            _file.Status.Should().Be(MangaDownloadFileStatus.ManualReview);
            _file.Reason.Should().Contain("already exists");
            _disk.Verify(value => value.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
        }
    }
}
