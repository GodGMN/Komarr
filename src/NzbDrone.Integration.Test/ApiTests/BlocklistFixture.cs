using FluentAssertions;
using NUnit.Framework;
using Readarr.Api.V1.Author;
using Readarr.Api.V1.Blocklist;

namespace NzbDrone.Integration.Test.ApiTests
{
    // The fixture-level "Waiting for metadata to be back again" marker is
    // gone because it was no longer true -- EnsureAuthor works against
    // OpenLibrary now. That changes nothing about whether these run: all
    // three carry their own [Ignore], and that one IS still true. There is no
    // endpoint for adding a release to the blocklist by hand, so
    // should_be_able_to_add_to_blocklist has nothing to call, and the other
    // two assert on the row it would have created.
    //
    // Left ignored deliberately rather than deleted: they describe a feature
    // gap worth keeping visible, not dead code.
    [TestFixture]
    public class BlocklistFixture : IntegrationTest
    {
        private AuthorResource _author;

        [Test]
        [Ignore("Adding to blocklist not supported")]
        public void should_be_able_to_add_to_blocklist()
        {
            _author = EnsureAuthor(OpenLibraryFixtureData.AndrewHunterMurrayId, OpenLibraryFixtureData.AndrewHunterMurrayName);

            Blocklist.Post(new BlocklistResource
            {
                AuthorId = _author.Id,
                SourceTitle = "Blacklist - Book 1 [2015 FLAC]"
            });
        }

        [Test]
        [Ignore("Adding to blocklist not supported")]
        public void should_be_able_to_get_all_blocklisted()
        {
            var result = Blocklist.GetPaged(0, 1000, "date", "desc");

            result.Should().NotBeNull();
            result.TotalRecords.Should().Be(1);
            result.Records.Should().NotBeNullOrEmpty();
        }

        [Test]
        [Ignore("Adding to blocklist not supported")]
        public void should_be_able_to_remove_from_blocklist()
        {
            Blocklist.Delete(1);

            var result = Blocklist.GetPaged(0, 1000, "date", "desc");

            result.Should().NotBeNull();
            result.TotalRecords.Should().Be(0);
        }
    }
}
