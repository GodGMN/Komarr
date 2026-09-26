using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Test.Common;
using Readarr.Api.V1.Author;

namespace NzbDrone.Integration.Test.ApiTests
{
    [TestFixture]
    public class AuthorEditorFixture : IntegrationTest
    {
        // Idempotent on purpose. Every test in this fixture shares one running
        // instance, so a second unconditional seed fails with
        // AuthorExistsValidator and takes down whichever test happens to run
        // second — an ordering trap the fixture only avoided by having a
        // single test.
        private void GivenExistingAuthor()
        {
            WaitForCompletion(() => Profiles.All().Count > 0);

            var existing = Author.All().Select(a => a.ForeignAuthorId).ToList();

            foreach (var name in new[] { "Alien Ant Farm", "Kiss" })
            {
                var newAuthor = Author.Lookup(name).First();

                if (existing.Contains(newAuthor.ForeignAuthorId))
                {
                    continue;
                }

                newAuthor.QualityProfileId = 1;
                newAuthor.MetadataProfileId = 1;
                newAuthor.Path = string.Format(@"C:\Test\{0}", name).AsOsAgnostic();

                Author.Post(newAuthor);
            }
        }

        [Test]
        public void should_be_able_to_update_multiple_author()
        {
            GivenExistingAuthor();

            var author = Author.All();

            var authorEditor = new AuthorEditorResource
            {
                QualityProfileId = 2,
                AuthorIds = author.Select(o => o.Id).ToList()
            };

            var result = Author.Editor(authorEditor);

            result.Should().HaveCount(2);
            result.TrueForAll(s => s.QualityProfileId == 2).Should().BeTrue();
        }

        [Test]
        public void should_be_able_to_set_and_clear_the_audiobook_quality_profile()
        {
            GivenExistingAuthor();

            var authorIds = Author.All().Select(o => o.Id).ToList();

            var assigned = Author.Editor(new AuthorEditorResource
            {
                AudiobookQualityProfileId = 2,
                AuthorIds = authorIds
            });

            assigned.TrueForAll(s => s.AudiobookQualityProfileId == 2).Should().BeTrue();

            // Omitting the field must leave it alone — this is what makes the
            // bulk editor's "No Change" option work at all. If the resource
            // ever stops being nullable, every unrelated bulk edit silently
            // resets everyone's audiobook profile to 0.
            var untouched = Author.Editor(new AuthorEditorResource
            {
                QualityProfileId = 1,
                AuthorIds = authorIds
            });

            untouched.TrueForAll(s => s.AudiobookQualityProfileId == 2).Should().BeTrue();

            // An explicit 0 is a real instruction, not an absent one: it puts
            // the authors back to a single quality profile.
            var cleared = Author.Editor(new AuthorEditorResource
            {
                AudiobookQualityProfileId = 0,
                AuthorIds = authorIds
            });

            cleared.TrueForAll(s => s.AudiobookQualityProfileId == 0).Should().BeTrue();
        }
    }
}
