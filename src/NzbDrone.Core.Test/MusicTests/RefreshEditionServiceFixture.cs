using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    // Coverage for the NarratorList → Narrators/EditionNarrators
    // materialization that landed alongside migration 043 (sync) and
    // migration 044 (legacy column drop). The rest of
    // RefreshEditionService (edition merging, delete, tag sync) was
    // upstream-untested and is out of scope here.
    [TestFixture]
    public class RefreshEditionServiceFixture : CoreTest<RefreshEditionService>
    {
        private static List<Tuple<Edition, Edition>> EmptyMerge => new List<Tuple<Edition, Edition>>();
        private static List<Edition> EmptyEditions => new List<Edition>();

        private static LazyLoaded<List<Narrator>> NarratorsFromNames(params string[] names)
        {
            return new LazyLoaded<List<Narrator>>(
                names.Select(n => new Narrator { Name = n }).ToList());
        }

        [Test]
        public void should_sync_narrators_for_added_edition_with_single_name()
        {
            var added = new Edition
            {
                Id = 11,
                ForeignEditionId = "OL-E-1",
                NarratorList = NarratorsFromNames("George Guidall")
            };

            Subject.RefreshEditionInfo(
                new List<Edition> { added },
                EmptyEditions,
                EmptyMerge,
                EmptyEditions,
                EmptyEditions,
                new List<Edition> { added },
                false);

            Mocker.GetMock<INarratorService>()
                .Verify(s => s.SetNarratorsForEdition(11, It.Is<IEnumerable<string>>(
                    names => names.SequenceEqual(new[] { "George Guidall" }))), Times.Once);
        }

        [Test]
        public void should_skip_narrator_sync_when_lazy_load_is_not_loaded()
        {
            // No augmenter fired — NarratorList is unset entirely. The sync
            // must not run; otherwise we'd wipe existing join rows just
            // because the lazy load would resolve to whatever the DB
            // currently has.
            var added = new Edition
            {
                Id = 12,
                ForeignEditionId = "OL-E-2",
                NarratorList = null
            };

            Subject.RefreshEditionInfo(
                new List<Edition> { added },
                EmptyEditions,
                EmptyMerge,
                EmptyEditions,
                EmptyEditions,
                new List<Edition> { added },
                false);

            Mocker.GetMock<INarratorService>()
                .Verify(s => s.SetNarratorsForEdition(It.IsAny<int>(), It.IsAny<IEnumerable<string>>()),
                    Times.Never);
        }

        [Test]
        public void should_preserve_order_for_multi_cast_narrator_list()
        {
            // An updated edition's metadata comes from the matching remote
            // edition (UseMetadataFrom), so the remote needs the NarratorList
            // value the test wants to assert on.
            var local = new Edition
            {
                Id = 13,
                ForeignEditionId = "OL-E-3",
                Title = "Audio Book"
            };
            var remote = new Edition
            {
                ForeignEditionId = "OL-E-3",
                NarratorList = NarratorsFromNames("Frank Muller", "George Guidall", "Stefan Rudnicki")
            };

            Subject.RefreshEditionInfo(
                EmptyEditions,
                new List<Edition> { local },
                EmptyMerge,
                EmptyEditions,
                EmptyEditions,
                new List<Edition> { remote },
                false);

            Mocker.GetMock<INarratorService>()
                .Verify(s => s.SetNarratorsForEdition(13, It.Is<IEnumerable<string>>(
                    names => names.SequenceEqual(new[] { "Frank Muller", "George Guidall", "Stefan Rudnicki" }))),
                    Times.Once);
        }
    }
}
