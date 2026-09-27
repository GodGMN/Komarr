using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Manga;
using MangaModel = NzbDrone.Core.Manga.Manga;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaLibraryMappingFixture
    {
        private const string Folder = "/library/BLAME!";
        private const string File = "/library/BLAME!/BLAME! v01.cbz";
        private Mock<IMangaLibraryScanService> _scan;
        private Mock<IMangaService> _manga;
        private Mock<IMangaRepository> _repository;
        private Mock<IMangaFileRepository> _files;
        private Mock<IMangaFileItemRepository> _coverage;
        private Mock<IDiskProvider> _disk;
        private MangaModel _title;
        private MangaLibraryMappingService _service;

        [SetUp]
        public void SetUp()
        {
            _title = new MangaModel { Id = 7, AniListId = 30149, PreferredTitle = "BLAME!", Monitored = true };
            _scan = new Mock<IMangaLibraryScanService>();
            _scan.Setup(value => value.ScanFolder(Folder)).Returns(new MangaLibraryFolder
            {
                RootPath = "/library",
                Path = Folder,
                Name = "BLAME!",
                Files = new List<MangaLibraryFile> { new MangaLibraryFile { Path = File, Name = "BLAME! v01.cbz" } }
            });
            _manga = new Mock<IMangaService>();
            _manga.Setup(value => value.Find(7)).Returns(_title);
            _manga.Setup(value => value.All()).Returns(new[] { _title });
            _manga.Setup(value => value.GetItems(7)).Returns(new MangaItem[0]);
            _manga.Setup(value => value.GetFiles(7)).Returns(new MangaFile[0]);
            _manga.Setup(value => value.AddItem(7, It.IsAny<MangaItemAddOptions>())).Returns<int, MangaItemAddOptions>((_, options) =>
            {
                var item = new MangaItem { Id = 11, MangaId = 7, Type = MangaItemType.Volume, Monitored = options.Monitored };
                item.SetNumber(options.NumberText);
                return item;
            });
            _repository = new Mock<IMangaRepository>();
            _files = new Mock<IMangaFileRepository>();
            _files.Setup(value => value.Insert(It.IsAny<MangaFile>())).Returns<MangaFile>(file =>
            {
                file.Id = 42;
                return file;
            });
            _coverage = new Mock<IMangaFileItemRepository>();
            _disk = new Mock<IDiskProvider>();
            _disk.Setup(value => value.FileExists(File)).Returns(true);
            _disk.Setup(value => value.GetFileSize(File)).Returns(100);
            _disk.Setup(value => value.FileGetLastWrite(File)).Returns(DateTime.UtcNow);
            _service = new MangaLibraryMappingService(
                _scan.Object,
                _manga.Object,
                _repository.Object,
                _files.Object,
                _coverage.Object,
                new MangaReleaseParser(),
                new MangaReleaseMatcher(),
                _disk.Object);
        }

        [Test]
        public void Selected_existing_file_is_registered_in_place_as_owned_volume()
        {
            var preview = _service.Preview(Folder, 7);
            preview.Files.Single().SuggestedNumbers.Should().Equal("01");
            preview.Files.Single().Warning.Should().BeNull();

            var result = _service.Map(new MangaLibraryMapRequest
            {
                FolderPath = Folder,
                MangaId = 7,
                Files = new List<MangaLibraryMapSelection>
                {
                    new MangaLibraryMapSelection { Path = File, Numbers = new List<string> { "1" } }
                }
            });

            result.FilesRegistered.Should().Be(1);
            result.ItemsCreated.Should().Be(1);
            _title.Path.Should().Be(Folder);
            _files.Verify(value => value.Insert(It.Is<MangaFile>(file => file.Path == File && file.OriginalFilePath == File)), Times.Once());
            _coverage.Verify(value => value.Insert(It.Is<MangaFileItem>(link => link.MangaFileId == 42 && link.MangaItemId == 11)), Times.Once());
            _disk.Verify(value => value.MoveFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
            _disk.Verify(value => value.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
            _disk.Verify(value => value.DeleteFile(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void Uncertain_filename_requires_explicit_confirmation()
        {
            _scan.Setup(value => value.ScanFolder(Folder)).Returns(new MangaLibraryFolder
            {
                RootPath = "/library",
                Path = Folder,
                Name = "BLAME!",
                Files = new List<MangaLibraryFile> { new MangaLibraryFile { Path = File, Name = "unknown.cbz" } }
            });
            var request = new MangaLibraryMapRequest
            {
                FolderPath = Folder,
                MangaId = 7,
                Files = new List<MangaLibraryMapSelection>
                {
                    new MangaLibraryMapSelection { Path = File, Numbers = new List<string> { "1" } }
                }
            };

            Action unconfirmed = () => _service.Map(request);
            unconfirmed.Should().Throw<ArgumentException>().WithMessage("*Confirm uncertain*");
            _files.Verify(value => value.Insert(It.IsAny<MangaFile>()), Times.Never());

            request.ConfirmUncertain = true;
            _service.Map(request).FilesRegistered.Should().Be(1);
        }

        [Test]
        public void Selected_path_must_come_from_current_folder_preview()
        {
            var request = new MangaLibraryMapRequest
            {
                FolderPath = Folder,
                MangaId = 7,
                Files = new List<MangaLibraryMapSelection>
                {
                    new MangaLibraryMapSelection { Path = "/outside/other.cbz", Numbers = new List<string> { "1" } }
                }
            };

            Action action = () => _service.Map(request);

            action.Should().Throw<ArgumentException>().WithMessage("*outside the scanned folder*");
            _files.Verify(value => value.Insert(It.IsAny<MangaFile>()), Times.Never());
        }
    }
}
