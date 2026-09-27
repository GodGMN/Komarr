using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Manga;
using NzbDrone.Core.RootFolders;
using MangaModel = NzbDrone.Core.Manga.Manga;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaLibraryScanFixture
    {
        [Test]
        public void Structured_library_is_inventoried_without_mapping_or_file_changes()
        {
            var disk = NewDisk();
            disk.Setup(value => value.GetDirectories("/library")).Returns(new[] { "/library/BLAME!", "/library/Naruto" });
            disk.Setup(value => value.GetDirectories("/library/BLAME!")).Returns(new string[0]);
            disk.Setup(value => value.GetDirectories("/library/Naruto")).Returns(new string[0]);
            disk.Setup(value => value.GetFiles("/library/BLAME!", false)).Returns(new[] { "/library/BLAME!/BLAME! v01.cbz" });
            disk.Setup(value => value.GetFiles("/library/Naruto", false)).Returns(new[]
            {
                "/library/Naruto/v02.cbz",
                "/library/Naruto/unknown.cbz",
                "/library/Naruto/readme.txt"
            });
            var service = NewService(disk.Object, new[]
            {
                new MangaModel { Id = 1, PreferredTitle = "BLAME!", Path = "/library/BLAME!", Monitored = true },
                new MangaModel { Id = 2, PreferredTitle = "Naruto", Monitored = false }
            });

            var result = service.Scan();

            result.RootsScanned.Should().Be(1);
            result.Folders.Should().HaveCount(2);
            result.Folders[0].MappedMangaId.Should().Be(1);
            result.Folders[0].Files.Single().StartNumber.Should().Be("01");
            result.Folders[1].MappedMangaId.Should().BeNull();
            result.Folders[1].SuggestedMangaId.Should().Be(2);
            result.Folders[1].NeedsReview.Should().BeTrue();
            result.Folders[1].Files.Should().HaveCount(2);
            result.Folders[1].Files[0].ParsedTitle.Should().Be("Naruto");
            result.Folders[1].Files[1].Warning.Should().NotBeNull();
            disk.Verify(value => value.MoveFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
            disk.Verify(value => value.DeleteFile(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void Scan_stops_after_bounded_number_of_folders()
        {
            var disk = NewDisk();
            disk.Setup(value => value.GetDirectories("/library"))
                .Returns(Enumerable.Range(1, 2001).Select(number => $"/library/Series {number}"));
            var service = NewService(disk.Object, new MangaModel[0]);

            var result = service.Scan();

            result.Truncated.Should().BeTrue();
            disk.Verify(value => value.GetFiles(It.IsAny<string>(), false), Times.Exactly(2000));
        }

        [Test]
        public void Unsupported_files_cannot_make_one_folder_scan_unbounded()
        {
            var disk = NewDisk();
            disk.Setup(value => value.GetDirectories("/library")).Returns(new[] { "/library/Series" });
            disk.Setup(value => value.GetFiles("/library/Series", false))
                .Returns(Enumerable.Range(1, 2000).Select(number => $"/library/Series/notes-{number}.txt"));
            var service = NewService(disk.Object, new MangaModel[0]);

            var result = service.Scan();

            result.Folders.Should().ContainSingle();
            result.Folders.Single().Truncated.Should().BeTrue();
            result.Folders.Single().Files.Should().BeEmpty();
        }

        [Test]
        public void Folder_preview_rejects_paths_outside_configured_root_children()
        {
            var disk = NewDisk();
            var service = NewService(disk.Object, new MangaModel[0]);

            System.Action action = () => service.ScanFolder("/outside/Series");

            action.Should().Throw<KeyNotFoundException>();
            disk.Verify(value => value.GetFiles(It.IsAny<string>(), false), Times.Never());
        }

        [Test]
        public void Symlinked_folder_outside_root_cannot_be_scanned_or_mapped()
        {
            var temporary = Path.Combine(Path.GetTempPath(), $"komarr-scan-{Guid.NewGuid():N}");
            var root = Path.Combine(temporary, "library");
            var outside = Path.Combine(temporary, "outside");
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(outside);
            try
            {
                var link = Path.Combine(root, "Outside");
                Directory.CreateSymbolicLink(link, outside);
                var disk = NewDisk();
                disk.Setup(value => value.FolderExists(root)).Returns(true);
                disk.Setup(value => value.FolderExists(link)).Returns(true);
                disk.Setup(value => value.GetDirectories(root)).Returns(new[] { link });
                var roots = new Mock<IRootFolderService>();
                roots.Setup(value => value.All()).Returns(new List<RootFolder> { new RootFolder { Path = root } });
                var manga = new Mock<IMangaService>();
                manga.Setup(value => value.All()).Returns(new MangaModel[0]);
                var service = new MangaLibraryScanService(
                    roots.Object,
                    manga.Object,
                    new MangaReleaseParser(),
                    disk.Object,
                    LogManager.GetCurrentClassLogger());

                service.Scan().Errors.Should().ContainSingle(value => value.Contains("outside its configured root"));
                Action preview = () => service.ScanFolder(link);
                preview.Should().Throw<ArgumentException>();
                disk.Verify(value => value.GetFiles(link, false), Times.Never());
            }
            finally
            {
                Directory.Delete(temporary, true);
            }
        }

        [Test]
        public void Linked_archive_outside_root_is_skipped_with_a_reason()
        {
            var temporary = Path.Combine(Path.GetTempPath(), $"komarr-file-{Guid.NewGuid():N}");
            var root = Path.Combine(temporary, "library");
            var folder = Path.Combine(root, "BLAME!");
            var outside = Path.Combine(temporary, "outside.cbz");
            Directory.CreateDirectory(folder);
            File.WriteAllText(outside, "archive");
            try
            {
                var link = Path.Combine(folder, "BLAME! v01.cbz");
                File.CreateSymbolicLink(link, outside);
                var disk = NewDisk();
                disk.Setup(value => value.FolderExists(folder)).Returns(true);
                disk.Setup(value => value.GetFiles(folder, false)).Returns(new[] { link });
                var roots = new Mock<IRootFolderService>();
                roots.Setup(value => value.All()).Returns(new List<RootFolder> { new RootFolder { Path = root } });
                var manga = new Mock<IMangaService>();
                manga.Setup(value => value.All()).Returns(new MangaModel[0]);
                var service = new MangaLibraryScanService(
                    roots.Object,
                    manga.Object,
                    new MangaReleaseParser(),
                    disk.Object,
                    LogManager.GetCurrentClassLogger());

                var result = service.ScanFolder(folder);

                result.Files.Should().BeEmpty();
                result.Warnings.Should().ContainSingle(value => value.Contains("outside its configured root"));
            }
            finally
            {
                Directory.Delete(temporary, true);
            }
        }

        private static Mock<IDiskProvider> NewDisk()
        {
            var disk = new Mock<IDiskProvider>();
            disk.Setup(value => value.FolderExists("/library")).Returns(true);
            disk.Setup(value => value.GetDirectories(It.IsAny<string>())).Returns(new string[0]);
            disk.Setup(value => value.GetFiles(It.IsAny<string>(), false)).Returns(new string[0]);
            return disk;
        }

        private static MangaLibraryScanService NewService(IDiskProvider disk, IEnumerable<MangaModel> manga)
        {
            var roots = new Mock<IRootFolderService>();
            roots.Setup(value => value.All()).Returns(new List<RootFolder> { new RootFolder { Path = "/library" } });
            var titles = new Mock<IMangaService>();
            titles.Setup(value => value.All()).Returns(manga);
            return new MangaLibraryScanService(
                roots.Object,
                titles.Object,
                new MangaReleaseParser(),
                disk,
                LogManager.GetCurrentClassLogger());
        }
    }
}
