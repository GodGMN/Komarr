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
            stored.UserAliases.Should().Contain("OP");
            stored.QualityPolicy.AllowedLanguages.Should().Contain("English");
            stored.QualityPolicy.MinimumSeeders.Should().Be(2);
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
            Mocker.Resolve<MangaFileItemRepository>().GetByFileIds(new[] { file.Id })
                  .Select(coverage => coverage.MangaItemId)
                  .Should().BeEquivalentTo(volumes.Select(volume => volume.Id));
            Db.Single<MangaFile>().EditionLabel.Should().Be("Omnibus");
        }

        [Test]
        public void stores_download_client_identity_and_covered_items()
        {
            var manga = Db.Insert(NewManga());
            Db.Insert(new MangaDownload
            {
                MangaId = manga.Id,
                CoveredItemIds = new List<int> { 1, 2 },
                IndexerId = 5,
                Indexer = "Nyaa",
                ReleaseGuid = "release-guid",
                ReleaseTitle = "ONE PIECE v01-v02",
                DownloadClientId = 3,
                DownloadClient = "qBittorrent",
                DownloadId = "torrent-hash",
                Status = MangaDownloadStatus.Sent,
                Added = DateTime.UtcNow
            });

            var stored = Db.Single<MangaDownload>();
            stored.CoveredItemIds.Should().Equal(1, 2);
            stored.DownloadId.Should().Be("torrent-hash");
            stored.Status.Should().Be(MangaDownloadStatus.Sent);
            Mocker.Resolve<MangaDownloadRepository>().FindByDownloadId(3, "torrent-hash").ReleaseGuid.Should().Be("release-guid");
        }

        [Test]
        public void stores_completed_download_file_review_and_coverage()
        {
            var manga = Db.Insert(NewManga());
            var download = Db.Insert(new MangaDownload
            {
                MangaId = manga.Id,
                IndexerId = 1,
                ReleaseGuid = "guid",
                ReleaseTitle = "ONE PIECE v01-v02",
                DownloadClientId = 2,
                DownloadClient = "qBittorrent",
                Status = MangaDownloadStatus.Completed,
                Added = DateTime.UtcNow
            });
            Db.Insert(new MangaDownloadFile
            {
                MangaDownloadId = download.Id,
                Path = "/downloads/ONE PIECE v01-v02.cbz",
                Size = 123,
                Status = MangaDownloadFileStatus.Ready,
                CoveredItemIds = new List<int> { 11, 12 },
                ScannedAt = DateTime.UtcNow
            });

            var stored = Mocker.Resolve<MangaDownloadFileRepository>().GetByDownloadId(download.Id).Single();
            stored.Status.Should().Be(MangaDownloadFileStatus.Ready);
            stored.CoveredItemIds.Should().Equal(11, 12);
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
                UserAliases = new List<string> { "OP" },
                QualityPolicy = new MangaReleasePolicy
                {
                    AllowedLanguages = new List<string> { "English" },
                    MinimumSeeders = 2
                },
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
