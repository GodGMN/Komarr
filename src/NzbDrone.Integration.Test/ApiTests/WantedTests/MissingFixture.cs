using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using Readarr.Api.V1.RootFolders;

namespace NzbDrone.Integration.Test.ApiTests.WantedTests
{
    [TestFixture]
    public class MissingFixture : IntegrationTest
    {
        [SetUp]
        public void Setup()
        {
            // Add a root folder
            RootFolders.Post(new RootFolderResource
            {
                Name = "TestLibrary",
                Path = AuthorRootFolder,
                DefaultMetadataProfileId = 1,
                DefaultQualityProfileId = 1,
                DefaultMonitorOption = MonitorTypes.All
            });
        }

        [Test]
        [Order(0)]
        public void missing_should_be_empty()
        {
            EnsureNoAuthor(OpenLibraryFixtureData.AndrewHunterMurrayId, OpenLibraryFixtureData.AndrewHunterMurrayName);

            var result = WantedMissing.GetPaged(0, 15, "releaseDate", "desc");

            result.Records.Should().BeEmpty();
        }

        [Test]
        [Order(1)]
        public void missing_should_have_monitored_items()
        {
            EnsureAuthor(OpenLibraryFixtureData.AndrewHunterMurrayId, OpenLibraryFixtureData.AndrewHunterMurrayName, true);

            var result = WantedMissing.GetPaged(0, 15, "releaseDate", "desc");

            result.Records.Should().NotBeEmpty();
        }

        [Test]
        [Order(1)]
        public void missing_should_have_author()
        {
            EnsureAuthor(OpenLibraryFixtureData.AndrewHunterMurrayId, OpenLibraryFixtureData.AndrewHunterMurrayName, true);

            var result = WantedMissing.GetPagedIncludeAuthor(0, 15, "releaseDate", "desc", includeAuthor: true);

            result.Records.First().Author.Should().NotBeNull();
            result.Records.First().Author.AuthorName.Should().Be(OpenLibraryFixtureData.AndrewHunterMurrayName);
        }

        [Test]
        [Order(1)]
        public void missing_should_not_have_author()
        {
            EnsureAuthor(OpenLibraryFixtureData.AndrewHunterMurrayId, OpenLibraryFixtureData.AndrewHunterMurrayName, true);

            var result = WantedMissing.GetPagedIncludeAuthor(0, 15, "releaseDate", "desc", includeAuthor: false);

            result.Records.First().Author.Should().BeNull();
        }

        [Test]
        [Order(1)]
        public void missing_should_not_have_unmonitored_items()
        {
            EnsureAuthor(OpenLibraryFixtureData.AndrewHunterMurrayId, OpenLibraryFixtureData.AndrewHunterMurrayName, false);

            var result = WantedMissing.GetPaged(0, 15, "releaseDate", "desc");

            result.Records.Should().BeEmpty();
        }

        [Test]
        [Order(2)]
        public void missing_should_have_unmonitored_items()
        {
            EnsureAuthor(OpenLibraryFixtureData.AndrewHunterMurrayId, OpenLibraryFixtureData.AndrewHunterMurrayName, false);

            var result = WantedMissing.GetPaged(0, 15, "releaseDate", "desc", "monitored", false);

            result.Records.Should().NotBeEmpty();
        }
    }
}
