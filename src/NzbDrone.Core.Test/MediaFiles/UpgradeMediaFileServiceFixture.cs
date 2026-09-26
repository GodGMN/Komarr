using System.Collections.Generic;
using System.IO;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    public class UpgradeMediaFileServiceFixture : CoreTest<UpgradeMediaFileService>
    {
        private BookFile _trackFile;
        private LocalBook _localTrack;
        private string _rootPath = @"C:\Test\Music\Author".AsOsAgnostic();

        [SetUp]
        public void Setup()
        {
            _localTrack = new LocalBook();
            _localTrack.Author = new Author
            {
                Path = _rootPath
            };

            _trackFile = Builder<BookFile>
                .CreateNew()
                .Build();

            Mocker.GetMock<IDiskProvider>()
                .Setup(c => c.FileExists(It.IsAny<string>()))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(c => c.FolderExists(It.IsAny<string>()))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                 .Setup(c => c.GetParentFolder(It.IsAny<string>()))
                 .Returns<string>(c => Path.GetDirectoryName(c));

            Mocker.GetMock<IRootFolderService>()
                .Setup(c => c.GetBestRootFolder(It.IsAny<string>()))
                .Returns(new RootFolder());
        }

        private void GivenSingleTrackWithSingleTrackFile()
        {
            _localTrack.Book = Builder<Book>.CreateNew()
                .With(e => e.BookFiles = new LazyLoaded<List<BookFile>>(
                          new List<BookFile>
                          {
                              new BookFile
                              {
                                  Id = 1,
                                  Path = Path.Combine(_rootPath, @"Season 01\30.rock.s01e01.avi"),
                              }
                          }))
                .Build();
        }

        [Test]
        public void should_delete_single_track_file_once()
        {
            GivenSingleTrackWithSingleTrackFile();

            Subject.UpgradeBookFile(_trackFile, _localTrack);

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_delete_track_file_from_database()
        {
            GivenSingleTrackWithSingleTrackFile();

            Subject.UpgradeBookFile(_trackFile, _localTrack);

            Mocker.GetMock<IMediaFileService>().Verify(v => v.Delete(It.IsAny<BookFile>(), DeleteMediaFileReason.Upgrade), Times.Once());
        }

        [Test]
        public void should_delete_existing_file_fromdb_if_file_doesnt_exist()
        {
            GivenSingleTrackWithSingleTrackFile();

            Mocker.GetMock<IDiskProvider>()
                .Setup(c => c.FileExists(It.IsAny<string>()))
                .Returns(false);

            Subject.UpgradeBookFile(_trackFile, _localTrack);

            // Mocker.GetMock<IMediaFileService>().Verify(v => v.Delete(_localTrack.Book.BookFiles.Value, DeleteMediaFileReason.Upgrade), Times.Once());
        }

        [Test]
        public void should_not_try_to_recyclebin_existing_file_if_file_doesnt_exist()
        {
            GivenSingleTrackWithSingleTrackFile();

            Mocker.GetMock<IDiskProvider>()
                .Setup(c => c.FileExists(It.IsAny<string>()))
                .Returns(false);

            Subject.UpgradeBookFile(_trackFile, _localTrack);

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_return_old_track_file_in_oldFiles()
        {
            GivenSingleTrackWithSingleTrackFile();

            Subject.UpgradeBookFile(_trackFile, _localTrack).OldFiles.Count.Should().Be(1);
        }

        private void GivenExistingFiles(params Quality[] qualities)
        {
            var id = 1;
            var files = qualities.Select(q => new BookFile
            {
                Id = id++,
                Path = Path.Combine(_rootPath, $"existing.{q.Name}"),
                Quality = new QualityModel(q)
            }).ToList();

            _localTrack.Book = Builder<Book>.CreateNew()
                .With(e => e.BookFiles = new LazyLoaded<List<BookFile>>(files))
                .Build();
        }

        // The whole point of per-format quality profiles, asserted at the one
        // place that actually destroys data. Before this, importing an
        // audiobook for a book already held as an ebook recycle-binned the
        // ebook and deleted its row — not "ranked it lower", removed it.
        [Test]
        public void should_not_delete_an_ebook_when_importing_an_audiobook()
        {
            GivenExistingFiles(Quality.EPUB);
            _trackFile.Quality = new QualityModel(Quality.M4B);

            var result = Subject.UpgradeBookFile(_trackFile, _localTrack);

            result.OldFiles.Should().BeEmpty();

            Mocker.GetMock<IRecycleBinProvider>()
                  .Verify(v => v.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never());

            Mocker.GetMock<IMediaFileService>()
                  .Verify(v => v.Delete(It.IsAny<BookFile>(), It.IsAny<DeleteMediaFileReason>()), Times.Never());
        }

        [Test]
        public void should_not_delete_an_audiobook_when_importing_an_ebook()
        {
            GivenExistingFiles(Quality.M4B);
            _trackFile.Quality = new QualityModel(Quality.EPUB);

            Subject.UpgradeBookFile(_trackFile, _localTrack).OldFiles.Should().BeEmpty();

            Mocker.GetMock<IMediaFileService>()
                  .Verify(v => v.Delete(It.IsAny<BookFile>(), It.IsAny<DeleteMediaFileReason>()), Times.Never());
        }

        // The other half: replacing within a format must still work, or this
        // change would have quietly disabled upgrades altogether.
        [Test]
        public void should_still_replace_an_existing_file_of_the_same_format()
        {
            GivenExistingFiles(Quality.MP3);
            _trackFile.Quality = new QualityModel(Quality.M4B);

            Subject.UpgradeBookFile(_trackFile, _localTrack).OldFiles.Should().HaveCount(1);

            Mocker.GetMock<IMediaFileService>()
                  .Verify(v => v.Delete(It.IsAny<BookFile>(), DeleteMediaFileReason.Upgrade), Times.Once());
        }

        [Test]
        public void should_replace_only_the_matching_format_when_the_book_has_both()
        {
            GivenExistingFiles(Quality.EPUB, Quality.MP3);
            _trackFile.Quality = new QualityModel(Quality.M4B);

            var result = Subject.UpgradeBookFile(_trackFile, _localTrack);

            result.OldFiles.Should().HaveCount(1);
            result.OldFiles.Single().Quality.Quality.Should().Be(Quality.MP3);
        }

        [Test]
        [Ignore("Pending readarr fix")]
        public void should_import_if_existing_file_doesnt_exist_in_db()
        {
            _localTrack.Book = Builder<Book>.CreateNew()
                .With(e => e.BookFiles = new LazyLoaded<List<BookFile>>())
                .Build();

            Subject.UpgradeBookFile(_trackFile, _localTrack);

            // Mocker.GetMock<IMediaFileService>().Verify(v => v.Delete(_localTrack.Book.BookFiles.Value, It.IsAny<DeleteMediaFileReason>()), Times.Never());
        }
    }
}
