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
        private Mock<IMangaFileRepository> _files;
        private Mock<IAniListMetadataClient> _metadata;
        private MangaService _service;

        [SetUp]
        public void SetUp()
        {
            _repository = new Mock<IMangaRepository>();
            _items = new Mock<IMangaItemRepository>();
            _files = new Mock<IMangaFileRepository>();
            _metadata = new Mock<IAniListMetadataClient>();
            _service = new MangaService(_repository.Object, _items.Object, _files.Object, _metadata.Object);
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
                        Status = "FINISHED",
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
            _items.Verify(x => x.Insert(It.IsAny<MangaItem>()), Times.Exactly(10));
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
        public void Refresh_adds_missing_known_volumes_without_replacing_existing_items()
        {
            var existing = new MangaModel { Id = 7, AniListId = 30149, PreferredTitle = "BLAME!", Monitored = true };
            var first = new MangaItem { Id = 10, MangaId = 7, Type = MangaItemType.Volume, Monitored = false };
            first.SetNumber("1");
            _repository.Setup(x => x.Find(7)).Returns(existing);
            _repository.Setup(x => x.Update(It.IsAny<MangaModel>())).Returns<MangaModel>(manga => manga);
            _items.Setup(x => x.GetByMangaId(7)).Returns(new[] { first });
            _metadata.Setup(x => x.GetById(30149, true)).Returns(new AniListResult
            {
                Availability = AniListAvailability.Available,
                Media = new List<AniListMedia> { new AniListMedia { Id = 30149, TitleRomaji = "BLAME!", Status = "FINISHED", Volumes = 2 } }
            });

            _service.Refresh(7);

            first.Monitored.Should().BeFalse();
            _items.Verify(x => x.Insert(It.Is<MangaItem>(item => item.NumberText == "2" && item.MangaId == 7)), Times.Once());
            _items.Verify(x => x.Insert(It.Is<MangaItem>(item => item.NumberText == "1")), Times.Never());
        }

        [Test]
        public void Ongoing_metadata_count_does_not_create_assumed_wanted_items()
        {
            _metadata.Setup(x => x.GetById(30149, false)).Returns(new AniListResult
            {
                Availability = AniListAvailability.Available,
                Media = new List<AniListMedia>
                {
                    new AniListMedia { Id = 30149, TitleRomaji = "Ongoing Manga", Status = "RELEASING", Volumes = 10 }
                }
            });
            _repository.Setup(x => x.Insert(It.IsAny<MangaModel>())).Returns<MangaModel>(manga => manga);

            _service.Add(new MangaAddOptions { AniListId = 30149, TrackingMode = MangaTrackingMode.Volume });

            _items.Verify(x => x.Insert(It.IsAny<MangaItem>()), Times.Never());
        }

        [Test]
        public void Manual_item_can_be_added_and_monitoring_changed_without_metadata_count()
        {
            var manga = new MangaModel { Id = 7, TrackingMode = MangaTrackingMode.Chapter };
            _repository.Setup(x => x.Find(7)).Returns(manga);
            _items.Setup(x => x.GetByMangaId(7)).Returns(new MangaItem[0]);
            _items.Setup(x => x.Insert(It.IsAny<MangaItem>())).Returns<MangaItem>(item =>
            {
                item.Id = 12;
                return item;
            });
            _items.Setup(x => x.Find(12)).Returns(() => new MangaItem { Id = 12, MangaId = 7, Type = MangaItemType.Chapter, Monitored = true });
            _items.Setup(x => x.Update(It.IsAny<MangaItem>())).Returns<MangaItem>(item => item);

            var added = _service.AddItem(7, new MangaItemAddOptions { NumberText = "12.5", Title = "Extra" });
            var unmonitored = _service.SetItemMonitored(7, 12, false);

            added.Type.Should().Be(MangaItemType.Chapter);
            added.NumberDecimal.Should().Be(12.5m);
            added.DiscoveredFrom.Should().Be(MangaItemDiscoverySource.Manual);
            unmonitored.Monitored.Should().BeFalse();
        }

        [Test]
        public void Manual_entry_can_confirm_a_stale_metadata_item_on_ongoing_manga()
        {
            var manga = new MangaModel { Id = 7, Status = "RELEASING", TrackingMode = MangaTrackingMode.Volume };
            var stale = new MangaItem
            {
                Id = 12, MangaId = 7, Type = MangaItemType.Volume,
                Monitored = false, DiscoveredFrom = MangaItemDiscoverySource.Metadata
            };
            stale.SetNumber("11");
            _repository.Setup(x => x.Find(7)).Returns(manga);
            _items.Setup(x => x.GetByMangaId(7)).Returns(new[] { stale });
            _items.Setup(x => x.Update(It.IsAny<MangaItem>())).Returns<MangaItem>(item => item);

            var confirmed = _service.AddItem(7, new MangaItemAddOptions { NumberText = "011", Monitored = true });

            confirmed.Id.Should().Be(12);
            confirmed.DiscoveredFrom.Should().Be(MangaItemDiscoverySource.Manual);
            confirmed.Monitored.Should().BeTrue();
            _items.Verify(x => x.Insert(It.IsAny<MangaItem>()), Times.Never());
        }

        [Test]
        public void Update_rejects_identity_change()
        {
            _repository.Setup(x => x.Find(7)).Returns(new MangaModel { Id = 7, AniListId = 30149 });

            Action action = () => _service.Update(7, new MangaAddOptions { AniListId = 999 });

            action.Should().Throw<ArgumentException>().WithMessage("*identity cannot be changed*");
            _repository.Verify(x => x.Update(It.IsAny<MangaModel>()), Times.Never());
        }

        [Test]
        public void Update_normalizes_aliases_and_preserves_them_when_omitted()
        {
            var existing = new MangaModel { Id = 7, AniListId = 30149, PreferredTitle = "BLAME!" };
            _repository.Setup(x => x.Find(7)).Returns(existing);
            _repository.Setup(x => x.Update(It.IsAny<MangaModel>())).Returns<MangaModel>(manga => manga);

            _service.Update(7, new MangaAddOptions
            {
                AniListId = 30149,
                UserAliases = new List<string> { "  Buramu!  ", "buramu", "BLAME!" },
                QualityPolicy = new MangaReleasePolicy { AllowedLanguages = new List<string> { "English" } }
            });

            existing.UserAliases.Should().Equal("Buramu!", "BLAME!");
            existing.QualityPolicy.AllowedLanguages.Should().Contain("English");

            _service.Update(7, new MangaAddOptions { AniListId = 30149 });

            existing.UserAliases.Should().Equal("Buramu!", "BLAME!");
            existing.QualityPolicy.AllowedLanguages.Should().Contain("English");
        }
    }
}
