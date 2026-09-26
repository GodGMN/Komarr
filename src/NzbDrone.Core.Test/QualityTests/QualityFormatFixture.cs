using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Qualities;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.QualityTests
{
    // Guards the ebook/audiobook classification that per-format quality
    // profiles are built on. Getting this wrong doesn't throw — it silently
    // ranks an audiobook against an ebook in the wrong profile — so the
    // classification is pinned rather than left to a range check.
    [TestFixture]
    public class QualityFormatFixture : TestBase
    {
        // The one that matters. Quality ids 0-4 are text and 10-13 are audio
        // by convention, with 5-9 an unused gap; anything added there would
        // resolve to Text by default and quietly land in the ebook profile.
        // This fails the moment a quality is added without being classified.
        [Test]
        public void every_known_quality_should_be_explicitly_classified()
        {
            var unclassified = Quality.All
                                      .Where(q => !Quality.IsClassified(q.Id))
                                      .ToList();

            unclassified.Should().BeEmpty(
                "every Quality must be listed as text or audio in Quality.cs — " +
                "an unclassified one defaults to Text and would be ranked against ebooks");
        }

        [TestCase(0)]  // Unknown (text)
        [TestCase(1)]  // PDF
        [TestCase(2)]  // MOBI
        [TestCase(3)]  // EPUB
        [TestCase(4)]  // AZW3
        public void text_qualities_should_resolve_to_text(int qualityId)
        {
            Quality.FindById(qualityId).Format.Should().Be(QualityFormat.Text);
        }

        [TestCase(10)] // MP3
        [TestCase(11)] // FLAC
        [TestCase(12)] // M4B
        [TestCase(13)] // UnknownAudio
        public void audio_qualities_should_resolve_to_audio(int qualityId)
        {
            Quality.FindById(qualityId).Format.Should().Be(QualityFormat.Audio);
        }

        [Test]
        public void both_formats_should_be_represented()
        {
            Quality.All.Select(q => q.Format).Distinct().Should().HaveCount(2);
        }

        // An id from a newer database must not take the process down. Text is
        // the safe answer: it is what every quality was treated as before
        // formats existed.
        [Test]
        public void unrecognised_id_should_fall_back_to_text_without_throwing()
        {
            Quality.FormatOf(9999).Should().Be(QualityFormat.Text);
            Quality.IsClassified(9999).Should().BeFalse();
        }
    }
}
