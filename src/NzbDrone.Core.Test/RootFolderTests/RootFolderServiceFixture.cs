using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.RootFolderTests
{
    [TestFixture]

    public class RootFolderServiceFixture : CoreTest<RootFolderService>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(m => m.FolderExists(It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(m => m.FolderWritable(It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IRootFolderRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<RootFolder>());

            // GetDetails lists direct children when reading root-folder details.
            Mocker.GetMock<IDiskProvider>()
                  .Setup(m => m.GetDirectories(It.IsAny<string>()))
                  .Returns(Array.Empty<string>());
        }

        private void WithFolders(params string[] folders)
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(m => m.GetDirectories(It.IsAny<string>()))
                  .Returns(folders);
        }

        private void WithNonExistingFolder()
        {
            Mocker.GetMock<IDiskProvider>()
                .Setup(m => m.FolderExists(It.IsAny<string>()))
                .Returns(false);
        }

        [TestCase("D:\\Music\\")]
        [TestCase("//server//folder")]
        public void should_be_able_to_add_root_dir(string path)
        {
            var root = new RootFolder { Path = path.AsOsAgnostic() };

            Subject.Add(root);

            Mocker.GetMock<IRootFolderRepository>().Verify(c => c.Insert(root), Times.Once());
        }

        [Test]
        public void should_throw_if_folder_being_added_doesnt_exist()
        {
            WithNonExistingFolder();

            Assert.Throws<DirectoryNotFoundException>(() => Subject.Add(new RootFolder { Path = "C:\\TEST".AsOsAgnostic() }));
        }

        [Test]
        public void should_be_able_to_remove_root_dir()
        {
            Subject.Remove(1);
            Mocker.GetMock<IRootFolderRepository>().Verify(c => c.Delete(1), Times.Once());
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("BAD PATH")]
        public void invalid_folder_path_throws_on_add(string path)
        {
            Assert.Throws<ArgumentException>(() =>
                    Mocker.Resolve<RootFolderService>().Add(new RootFolder { Id = 0, Path = path }));
        }

        [Test]
        public void adding_duplicated_root_folder_should_throw()
        {
            Mocker.GetMock<IRootFolderRepository>().Setup(c => c.All()).Returns(new List<RootFolder> { new RootFolder { Path = "C:\\Music".AsOsAgnostic() } });

            Assert.Throws<InvalidOperationException>(() => Subject.Add(new RootFolder { Path = @"C:\Music".AsOsAgnostic() }));
        }

        [Test]
        public void should_throw_when_adding_not_writable_folder()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(m => m.FolderWritable(It.IsAny<string>()))
                  .Returns(false);

            Assert.Throws<UnauthorizedAccessException>(() => Subject.Add(new RootFolder { Path = @"C:\Music".AsOsAgnostic() }));
        }

        [Test]
        public void should_return_subfolders_without_querying_book_tables()
        {
            var root = @"C:\Books".AsOsAgnostic();
            WithFolders(
                Path.Combine(root, "Brandon Sanderson"),
                Path.Combine(root, "Ursula K Le Guin"));

            var unmapped = Subject.GetUnmappedFolders(root);

            unmapped.Should().HaveCount(2);
            unmapped.Select(x => x.Path).Should().Contain(Path.Combine(root, "Ursula K Le Guin"));
        }

        [TestCase("$RECYCLE.BIN")]
        [TestCase("lost+found")]
        [TestCase("@eaDir")]
        [TestCase(".caltrash")]
        public void should_exclude_special_folders(string folder)
        {
            var root = @"C:\Books".AsOsAgnostic();
            WithFolders(Path.Combine(root, folder));

            Subject.GetUnmappedFolders(root).Should().BeEmpty();
        }

        [Test]
        public void should_order_unmapped_folders_by_name()
        {
            var root = @"C:\Books".AsOsAgnostic();
            WithFolders(
                Path.Combine(root, "Zelazny"),
                Path.Combine(root, "Asimov"),
                Path.Combine(root, "Mieville"));

            Subject.GetUnmappedFolders(root)
                   .Select(x => x.Name)
                   .Should()
                   .ContainInOrder("Asimov", "Mieville", "Zelazny");
        }

        [Test]
        public void should_return_empty_when_root_folder_is_missing()
        {
            WithNonExistingFolder();

            Subject.GetUnmappedFolders(@"C:\Books".AsOsAgnostic()).Should().BeEmpty();

            Mocker.GetMock<IDiskProvider>()
                  .Verify(m => m.GetDirectories(It.IsAny<string>()), Times.Never());
        }

        // Add() inserts the row before GetDetails runs. If enumeration blew up
        // there the caller would see a failure for a root folder that had in
        // fact been created.
        [Test]
        public void should_still_add_root_folder_when_unmapped_enumeration_fails()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(m => m.GetDirectories(It.IsAny<string>()))
                  .Throws(new IOException("network share went away"));

            var root = new RootFolder { Path = @"C:\Books".AsOsAgnostic() };

            Subject.Add(root);

            Mocker.GetMock<IRootFolderRepository>().Verify(c => c.Insert(root), Times.Once());
            root.UnmappedFolders.Should().BeEmpty();

            ExceptionVerification.ExpectedErrors(1);
        }
    }
}
