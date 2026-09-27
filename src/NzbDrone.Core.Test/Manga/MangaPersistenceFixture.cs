using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Test.Framework;
using MangaModel = NzbDrone.Core.Manga.Manga;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaPersistenceFixture : DbTest
    {
        [Test]
        public void stores_anilist_identity_and_collection_preferences()
        {
            Db.Insert(NewManga());

            var stored = Db.Single<MangaModel>();
            stored.AniListId.Should().Be(30013);
            stored.TitleRomaji.Should().Be("ONE PIECE");
            stored.Synonyms.Should().Contain("One Piece");
            stored.TrackingMode.Should().Be(MangaTrackingMode.Volume);
            stored.Tags.Should().BeEquivalentTo(new[] { 2, 5 });
        }

        [Test]
        public void stores_decimal_and_text_chapter_numbers()
        {
            var manga = Db.Insert(NewManga());
            var chapter = NewItem(manga.Id, MangaItemType.Chapter, "12.5");
            var extra = NewItem(manga.Id, MangaItemType.Chapter, "EXTRA-1");

            Db.Insert(chapter);
            Db.Insert(extra);

            var stored = Db.All<MangaItem>().ToDictionary(item => item.NumberText);
            stored["12.5"].NumberDecimal.Should().Be(12.5m);
            stored["EXTRA-1"].NumberDecimal.Should().BeNull();
        }

        [Test]
        public void one_file_can_cover_several_volumes()
        {
            var manga = Db.Insert(NewManga());
            var volumes = Enumerable.Range(1, 3)
                                    .Select(number => Db.Insert(NewItem(manga.Id, MangaItemType.Volume, number.ToString())))
                                    .ToList();
            var file = Db.Insert(new MangaFile
            {
                MangaId = manga.Id,
                Path = "/manga/ONE PIECE/Volumes 1-3.cbz",
                Size = 123456789,
                Modified = DateTime.UtcNow,
                DateAdded = DateTime.UtcNow,
                EditionLabel = "Omnibus"
            });

            foreach (var volume in volumes)
            {
                Db.Insert(new MangaFileItem { MangaFileId = file.Id, MangaItemId = volume.Id });
            }

            Db.All<MangaFileItem>().Where(coverage => coverage.MangaFileId == file.Id)
              .Select(coverage => coverage.MangaItemId)
              .Should().BeEquivalentTo(volumes.Select(volume => volume.Id));
            Db.Single<MangaFile>().EditionLabel.Should().Be("Omnibus");
        }

        private static MangaModel NewManga()
        {
            return new MangaModel
            {
                AniListId = 30013,
                TitleRomaji = "ONE PIECE",
                PreferredTitle = "One Piece",
                CleanTitle = "one piece",
                Synonyms = new List<string> { "One Piece" },
                Tags = new List<int> { 2, 5 },
                TrackingMode = MangaTrackingMode.Volume,
                Monitored = true,
                Added = DateTime.UtcNow
            };
        }

        private static MangaItem NewItem(int mangaId, MangaItemType type, string number)
        {
            var item = new MangaItem
            {
                MangaId = mangaId,
                Type = type,
                Monitored = true,
                DiscoveredFrom = MangaItemDiscoverySource.Manual,
                Added = DateTime.UtcNow
            };
            item.SetNumber(number);
            return item;
        }
    }
}
