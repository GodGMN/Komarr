using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.BookImport;
using NzbDrone.Core.MediaFiles.BookImport.Identification;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.BookImport.Identification
{
    // When the metadata source has stopped answering, grinding through the rest
    // of a large library identifies every remaining book against nothing and
    // imports it unmatched. Stopping is the better outcome, and it has to be a
    // checked flag rather than an exception: Identify's per-book catch is
    // deliberately broad so one bad book cannot end a run, and it would swallow
    // a thrown abort just as effectively.
    [TestFixture]
    public class IdentificationAbortFixture : CoreTest<IdentificationService>
    {
        private List<LocalBook> _localBooks;

        [SetUp]
        public void Setup()
        {
            _localBooks = new List<LocalBook>
            {
                new LocalBook { Path = "/books/a.epub" },
                new LocalBook { Path = "/books/b.epub" },
                new LocalBook { Path = "/books/c.epub" }
            };

            Mocker.GetMock<ITrackGroupingService>()
                .Setup(s => s.GroupTracks(It.IsAny<List<LocalBook>>()))
                .Returns((List<LocalBook> books) =>
                {
                    var releases = new List<LocalEdition>();

                    foreach (var book in books)
                    {
                        releases.Add(new LocalEdition(new List<LocalBook> { book }));
                    }

                    return releases;
                });

            Mocker.GetMock<ICandidateService>()
                .Setup(s => s.GetDbCandidatesFromTags(It.IsAny<LocalEdition>(), It.IsAny<IdentificationOverrides>(), It.IsAny<bool>()))
                .Returns(new List<CandidateEdition>());

            Mocker.GetMock<ICandidateService>()
                .Setup(s => s.GetRemoteCandidates(It.IsAny<LocalEdition>(), It.IsAny<IdentificationOverrides>()))
                .Returns(new List<CandidateEdition>());
        }

        private void GivenSourceAvailable(bool available)
        {
            Mocker.GetMock<IMetadataSourceStatusService>()
                .SetupGet(s => s.IsAvailable)
                .Returns(available);
        }

        private void Identify()
        {
            // Never null in production — ImportDecisionMaker substitutes an empty
            // instance before calling, and IdentifyRelease dereferences it.
            Subject.Identify(_localBooks, new IdentificationOverrides(), new ImportDecisionMakerConfig());
        }

        [Test]
        public void should_identify_every_book_while_the_source_is_answering()
        {
            GivenSourceAvailable(true);

            Identify();

            Mocker.GetMock<ICandidateService>()
                .Verify(s => s.GetDbCandidatesFromTags(It.IsAny<LocalEdition>(), It.IsAny<IdentificationOverrides>(), It.IsAny<bool>()),
                    Times.Exactly(3));
        }

        [Test]
        public void should_stop_before_the_first_book_when_the_source_is_unavailable()
        {
            GivenSourceAvailable(false);

            Identify();

            Mocker.GetMock<ICandidateService>()
                .Verify(s => s.GetDbCandidatesFromTags(It.IsAny<LocalEdition>(), It.IsAny<IdentificationOverrides>(), It.IsAny<bool>()),
                    Times.Never());

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_stop_partway_when_the_source_goes_down_mid_run()
        {
            var calls = 0;

            Mocker.GetMock<IMetadataSourceStatusService>()
                .SetupGet(s => s.IsAvailable)
                .Returns(() => calls++ < 2);

            Identify();

            // Checked once per book: books 1 and 2 proceed, the third does not.
            Mocker.GetMock<ICandidateService>()
                .Verify(s => s.GetDbCandidatesFromTags(It.IsAny<LocalEdition>(), It.IsAny<IdentificationOverrides>(), It.IsAny<bool>()),
                    Times.Exactly(2));

            ExceptionVerification.ExpectedErrors(1);
        }
    }
}
