using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MetadataSource.Audnex;
using NzbDrone.Core.MetadataSource.Audnex.Resources;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource.Audnex
{
    // Phase 7b/8 coverage. Two axes:
    //   1. CanAugment gating  — opt-in flag, ASIN presence, audiobook format
    //   2. Augment merge      — only fills blanks, joins narrators with ", "
    [TestFixture]
    public class AudnexProxyFixture : CoreTest<AudnexProxy>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(c => c.AugmentAudiobookMetadata)
                .Returns(true);
        }

        private Book BookWith(params Edition[] editions)
        {
            return new Book
            {
                Title = "test",
                Editions = new LazyLoaded<List<Edition>>(new List<Edition>(editions))
            };
        }

        private Edition AudiobookEdition(string asin = "B00ABC1234", string format = "AudioBook")
        {
            return new Edition
            {
                Asin = asin,
                Format = format,
                Monitored = true
            };
        }

        private void GivenAudnexResponse(AudnexBookResource resource)
        {
            // HttpResponse<T> deserializes Resource from response.Content in
            // its constructor — no direct setter — so we round-trip through
            // JSON to give it something parseable.
            var json = JsonConvert.SerializeObject(resource);
            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get<AudnexBookResource>(It.IsAny<HttpRequest>()))
                .Returns<HttpRequest>(r => new HttpResponse<AudnexBookResource>(
                    new HttpResponse(r, new HttpHeader(), json)));
        }

        // ── CanAugment ──────────────────────────────────────────────
        [Test]
        public void CanAugment_should_be_false_when_config_flag_is_off()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(c => c.AugmentAudiobookMetadata)
                .Returns(false);

            Subject.CanAugment(BookWith(AudiobookEdition())).Should().BeFalse();
        }

        [Test]
        public void CanAugment_should_be_false_when_book_has_no_editions()
        {
            Subject.CanAugment(new Book { Editions = new LazyLoaded<List<Edition>>(new List<Edition>()) })
                .Should().BeFalse();
        }

        [Test]
        public void CanAugment_should_be_false_when_no_edition_has_asin()
        {
            var book = BookWith(new Edition { Format = "AudioBook", Asin = null });
            Subject.CanAugment(book).Should().BeFalse();
        }

        [Test]
        public void CanAugment_should_be_false_for_ebook_with_asin()
        {
            // Kindle editions have ASINs but no audnex data — verifies the
            // IsAudiobookFormat fix (used to wrongly match IsEbook=true).
            var book = BookWith(new Edition { Format = "EBook", IsEbook = true, Asin = "B00KINDLE" });
            Subject.CanAugment(book).Should().BeFalse();
        }

        [Test]
        public void CanAugment_should_be_true_for_audiobook_with_asin()
        {
            Subject.CanAugment(BookWith(AudiobookEdition())).Should().BeTrue();
        }

        // ── Augment merge ───────────────────────────────────────────
        [Test]
        public void Augment_should_populate_blank_narrators_joined_with_comma()
        {
            var book = BookWith(AudiobookEdition());
            GivenAudnexResponse(new AudnexBookResource
            {
                Narrators = new List<AudnexPerson>
                {
                    new AudnexPerson { Name = "George Guidall" },
                    new AudnexPerson { Name = "Frank Muller" }
                }
            });

            var result = Subject.Augment(book);

            var names = result.Editions.Value[0].NarratorList.Value.Select(n => n.Name).ToList();
            names.Should().Equal("George Guidall", "Frank Muller");
        }

        [Test]
        public void Augment_should_not_overwrite_existing_narrators()
        {
            var edition = AudiobookEdition();
            edition.NarratorList = new LazyLoaded<List<NzbDrone.Core.Books.Narrator>>(
                new List<NzbDrone.Core.Books.Narrator>
                {
                    new NzbDrone.Core.Books.Narrator { Name = "Pre-existing" }
                });
            var book = BookWith(edition);
            GivenAudnexResponse(new AudnexBookResource
            {
                Narrators = new List<AudnexPerson> { new AudnexPerson { Name = "From Audnex" } }
            });

            var result = Subject.Augment(book);

            var names = result.Editions.Value[0].NarratorList.Value.Select(n => n.Name).ToList();
            names.Should().Equal("Pre-existing");
        }

        [Test]
        public void Augment_should_fill_blank_publisher_and_release_date()
        {
            var edition = AudiobookEdition();
            edition.Publisher = null;
            edition.ReleaseDate = null;
            var book = BookWith(edition);
            GivenAudnexResponse(new AudnexBookResource
            {
                Publisher = "Brilliance Audio",
                ReleaseDate = new DateTime(2010, 6, 1)
            });

            var result = Subject.Augment(book);

            result.Editions.Value[0].Publisher.Should().Be("Brilliance Audio");
            result.Editions.Value[0].ReleaseDate.Should().Be(new DateTime(2010, 6, 1));
        }

        [Test]
        public void Augment_should_not_overwrite_existing_publisher()
        {
            var edition = AudiobookEdition();
            edition.Publisher = "Original Publisher";
            var book = BookWith(edition);
            GivenAudnexResponse(new AudnexBookResource { Publisher = "Audnex Publisher" });

            var result = Subject.Augment(book);

            result.Editions.Value[0].Publisher.Should().Be("Original Publisher");
        }

        [Test]
        public void Augment_should_skip_narrators_with_blank_names()
        {
            var book = BookWith(AudiobookEdition());
            GivenAudnexResponse(new AudnexBookResource
            {
                Narrators = new List<AudnexPerson>
                {
                    new AudnexPerson { Name = "Real Person" },
                    new AudnexPerson { Name = "  " },
                    new AudnexPerson { Name = null }
                }
            });

            var result = Subject.Augment(book);

            var names = result.Editions.Value[0].NarratorList.Value.Select(n => n.Name).ToList();
            names.Should().Equal("Real Person");
        }

        [Test]
        public void Augment_should_no_op_when_CanAugment_is_false()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(c => c.AugmentAudiobookMetadata)
                .Returns(false);

            var book = BookWith(AudiobookEdition());
            var result = Subject.Augment(book);

            result.Should().BeSameAs(book);
            Mocker.GetMock<IHttpClient>()
                .Verify(c => c.Get<AudnexBookResource>(It.IsAny<HttpRequest>()), Times.Never);
        }
    }
}
