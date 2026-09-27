using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.BookTests
{
    // Author.QualityProfileFor is the single seam every download and import
    // decision now resolves a profile through, so its fallback behaviour is
    // worth pinning: getting it wrong doesn't throw, it silently judges an
    // audiobook by the ebook ranking.
    [TestFixture]
    public class AuthorQualityProfileForFixture : TestBase
    {
        private QualityProfile _ebookProfile;
        private QualityProfile _audiobookProfile;

        [SetUp]
        public void Setup()
        {
            _ebookProfile = new QualityProfile { Id = 1, Name = "Ebook" };
            _audiobookProfile = new QualityProfile { Id = 2, Name = "Audiobook" };
        }

        private Author GivenAuthor(int audiobookProfileId, bool loadAudiobookProfile = true)
        {
            var author = new Author
            {
                QualityProfileId = 1,
                QualityProfile = new LazyLoaded<QualityProfile>(_ebookProfile),
                AudiobookQualityProfileId = audiobookProfileId
            };

            if (loadAudiobookProfile)
            {
                author.AudiobookQualityProfile = new LazyLoaded<QualityProfile>(_audiobookProfile);
            }

            return author;
        }

        // The default, and the one that matters most: every existing install
        // has AudiobookQualityProfileId == 0 after the migration, and must
        // behave exactly as it did before.
        [Test]
        public void single_format_author_should_use_the_one_profile_for_every_format()
        {
            var author = GivenAuthor(audiobookProfileId: 0);

            author.QualityProfileFor(Quality.EPUB).Should().Be(_ebookProfile);
            author.QualityProfileFor(Quality.M4B).Should().Be(_ebookProfile);
            author.QualityProfileFor(Quality.MP3).Should().Be(_ebookProfile);
        }

        [Test]
        public void dual_format_author_should_route_by_format()
        {
            var author = GivenAuthor(audiobookProfileId: 2);

            author.QualityProfileFor(Quality.EPUB).Should().Be(_ebookProfile);
            author.QualityProfileFor(Quality.PDF).Should().Be(_ebookProfile);
            author.QualityProfileFor(Quality.M4B).Should().Be(_audiobookProfile);
            author.QualityProfileFor(Quality.FLAC).Should().Be(_audiobookProfile);
        }

        // A configured-but-unloadable audiobook profile must not reject every
        // audiobook release; falling back is the safe direction.
        [Test]
        public void should_fall_back_when_the_audiobook_profile_is_set_but_missing()
        {
            var author = GivenAuthor(audiobookProfileId: 2, loadAudiobookProfile: false);

            author.QualityProfileFor(Quality.M4B).Should().Be(_ebookProfile);
        }

        // These run inside decision specifications, where a throw kills the
        // decision for every release in the batch rather than rejecting one.
        [Test]
        public void should_tolerate_a_null_quality()
        {
            var author = GivenAuthor(audiobookProfileId: 2);

            author.QualityProfileFor((Quality)null).Should().Be(_ebookProfile);
            author.QualityProfileFor((QualityModel)null).Should().Be(_ebookProfile);
            author.QualityProfileFor(new QualityModel { Quality = null }).Should().Be(_ebookProfile);
        }

        [Test]
        public void should_resolve_through_a_quality_model()
        {
            var author = GivenAuthor(audiobookProfileId: 2);

            author.QualityProfileFor(new QualityModel(Quality.M4B)).Should().Be(_audiobookProfile);
            author.QualityProfileFor(new QualityModel(Quality.EPUB)).Should().Be(_ebookProfile);
        }
    }
}
