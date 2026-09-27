using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Manga;
using NzbDrone.Core.MetadataSource.AniList;
using MangaModel = NzbDrone.Core.Manga.Manga;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaServiceFixture
    {
        private Mock<IMangaRepository> _repository;
        private Mock<IMangaItemRepository> _items;
        private Mock<IAniListMetadataClient> _metadata;
        private MangaService _service;

        [SetUp]
        public void SetUp()
        {
            _repository = new Mock<IMangaRepository>();
            _items = new Mock<IMangaItemRepository>();
            _metadata = new Mock<IAniListMetadataClient>();
            _service = new MangaService(_repository.Object, _items.Object, _metadata.Object);
        }

        [Test]
        public void Add_requires_an_explicit_anilist_id()
        {
            Action action = () => _service.Add(new MangaAddOptions());

            action.Should().Throw<ArgumentException>().WithMessage("*Select an AniList manga ID*");
            _metadata.Verify(x => x.Search(It.IsAny<string>()), Times.Never());
            _repository.Verify(x => x.Insert(It.IsAny<MangaModel>()), Times.Never());
        }

        [Test]
        public void Add_persists_selected_identity_and_metadata()
        {
            _metadata.Setup(x => x.GetById(30149, false)).Returns(new AniListResult
            {
                Availability = AniListAvailability.Available,
                Media = new List<AniListMedia>
                {
                    new AniListMedia
                    {
                        Id = 30149,
                        TitleRomaji = "BLAME!",
                        Synonyms = new List<string> { "Buramu!" },
                        Volumes = 10
                    }
                }
            });
            _repository.Setup(x => x.Insert(It.IsAny<MangaModel>())).Returns<MangaModel>(manga => manga);

            var manga = _service.Add(new MangaAddOptions { AniListId = 30149, TrackingMode = MangaTrackingMode.Volume });

            manga.AniListId.Should().Be(30149);
            manga.PreferredTitle.Should().Be("BLAME!");
            manga.Synonyms.Should().Contain("Buramu!");
            manga.AniListVolumeCount.Should().Be(10);
            _repository.Verify(x => x.Insert(It.Is<MangaModel>(m => m.AniListId == 30149)), Times.Once());
        }

        [Test]
        public void Add_rejects_mismatched_response_and_does_not_write()
        {
            _metadata.Setup(x => x.GetById(30149, false)).Returns(new AniListResult
            {
                Availability = AniListAvailability.Available,
                Media = new List<AniListMedia> { new AniListMedia { Id = 2 } }
            });

            Action action = () => _service.Add(new MangaAddOptions { AniListId = 30149 });

            action.Should().Throw<MangaMetadataException>();
            _repository.Verify(x => x.Insert(It.IsAny<MangaModel>()), Times.Never());
        }

        [Test]
        public void Refresh_keeps_local_metadata_during_outage()
        {
            var existing = new MangaModel { Id = 7, AniListId = 30149, PreferredTitle = "BLAME!", Description = "Local description" };
            _repository.Setup(x => x.Find(7)).Returns(existing);
            _metadata.Setup(x => x.GetById(30149, true)).Returns(new AniListResult
            {
                Availability = AniListAvailability.Stale,
                Media = new List<AniListMedia> { new AniListMedia { Id = 30149, Description = "Stale description" } }
            });

            var manga = _service.Refresh(7);

            manga.Description.Should().Be("Local description");
            _repository.Verify(x => x.Update(It.IsAny<MangaModel>()), Times.Never());
        }

        [Test]
        public void Update_rejects_identity_change()
        {
            _repository.Setup(x => x.Find(7)).Returns(new MangaModel { Id = 7, AniListId = 30149 });

            Action action = () => _service.Update(7, new MangaAddOptions { AniListId = 999 });

            action.Should().Throw<ArgumentException>().WithMessage("*identity cannot be changed*");
            _repository.Verify(x => x.Update(It.IsAny<MangaModel>()), Times.Never());
        }
    }
}
