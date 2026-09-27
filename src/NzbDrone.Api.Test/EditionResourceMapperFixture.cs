using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Test.Common;
using Readarr.Api.V1.Books;

namespace NzbDrone.Api.Test
{
    // Post-44 narrator read-path tests. The legacy Editions.Narrators
    // string column is gone; the mapper sources the API string solely
    // from the normalized Narrators / EditionNarrators join.
    [TestFixture]
    public class EditionResourceMapperFixture : TestBase
    {
        [Test]
        public void Should_project_narrators_string_from_join_when_populated()
        {
            var edition = new Edition
            {
                Id = 7,
                NarratorList = new LazyLoaded<List<Narrator>>(new List<Narrator>
                {
                    new Narrator { Name = "George Guidall" },
                    new Narrator { Name = "Frank Muller" }
                })
            };

            var resource = edition.ToResource();

            resource.Narrators.Should().Be("George Guidall, Frank Muller");
        }

        [Test]
        public void Should_return_null_when_join_is_empty()
        {
            var edition = new Edition
            {
                Id = 8,
                NarratorList = new LazyLoaded<List<Narrator>>(new List<Narrator>())
            };

            var resource = edition.ToResource();

            resource.Narrators.Should().BeNull();
        }

        [Test]
        public void Should_return_null_when_lazy_load_is_null()
        {
            // Edge case from tests / unit setup where NarratorList isn't
            // initialized at all; mapper must not throw.
            var edition = new Edition
            {
                Id = 9,
                NarratorList = null
            };

            var resource = edition.ToResource();

            resource.Narrators.Should().BeNull();
        }

        // Phase 12.1 — structured narrator surface alongside the legacy
        // comma-joined string. Frontend chip rendering reads NarratorList.
        [Test]
        public void Should_project_structured_narrator_list_when_populated()
        {
            var edition = new Edition
            {
                Id = 10,
                NarratorList = new LazyLoaded<List<Narrator>>(new List<Narrator>
                {
                    new Narrator { Id = 1, Name = "George Guidall", ForeignNarratorId = "ol-g1" },
                    new Narrator { Id = 2, Name = "Frank Muller", ForeignNarratorId = "ol-m1" }
                })
            };

            var resource = edition.ToResource();

            resource.NarratorList.Should().HaveCount(2);
            resource.NarratorList[0].Id.Should().Be(1);
            resource.NarratorList[0].Name.Should().Be("George Guidall");
            resource.NarratorList[0].ForeignNarratorId.Should().Be("ol-g1");
            resource.NarratorList[1].Id.Should().Be(2);
            resource.NarratorList[1].Name.Should().Be("Frank Muller");
        }

        [Test]
        public void Should_omit_narrator_list_when_join_is_empty()
        {
            var edition = new Edition
            {
                Id = 11,
                NarratorList = new LazyLoaded<List<Narrator>>(new List<Narrator>())
            };

            var resource = edition.ToResource();

            resource.NarratorList.Should().BeNull();
        }
    }
}
