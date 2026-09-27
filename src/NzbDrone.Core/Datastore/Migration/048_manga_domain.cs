using System.Data;
using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(048)]
    public class manga_domain : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("Manga")
                .WithColumn("AniListId").AsInt32().NotNullable().Unique()
                .WithColumn("MalId").AsInt32().Nullable()
                .WithColumn("TitleRomaji").AsString().NotNullable()
                .WithColumn("TitleEnglish").AsString().Nullable()
                .WithColumn("TitleNative").AsString().Nullable()
                .WithColumn("PreferredTitle").AsString().NotNullable()
                .WithColumn("CleanTitle").AsString().NotNullable()
                .WithColumn("Synonyms").AsString().NotNullable().WithDefaultValue("[]")
                .WithColumn("Description").AsString().Nullable()
                .WithColumn("Status").AsString().Nullable()
                .WithColumn("Format").AsString().Nullable()
                .WithColumn("CountryOfOrigin").AsString().Nullable()
                .WithColumn("StartDate").AsDateTime().Nullable()
                .WithColumn("EndDate").AsDateTime().Nullable()
                .WithColumn("AniListChapterCount").AsInt32().Nullable()
                .WithColumn("AniListVolumeCount").AsInt32().Nullable()
                .WithColumn("CoverUrl").AsString().Nullable()
                .WithColumn("BannerUrl").AsString().Nullable()
                .WithColumn("TrackingMode").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("Monitored").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("MonitorFutureItems").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("RootFolderPath").AsString().Nullable()
                .WithColumn("Path").AsString().Nullable()
                .WithColumn("QualityProfileId").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("MetadataProfileId").AsInt32().Nullable()
                .WithColumn("Tags").AsString().NotNullable().WithDefaultValue("[]")
                .WithColumn("LastInfoSync").AsDateTime().Nullable()
                .WithColumn("LastSearchTime").AsDateTime().Nullable()
                .WithColumn("Added").AsDateTime().NotNullable()
                .WithColumn("AniListUpdatedAt").AsDateTime().Nullable();

            Create.Index().OnTable("Manga").OnColumn("CleanTitle").Ascending();
            Create.Index().OnTable("Manga").OnColumn("Path").Ascending();

            Create.TableForModel("MangaItems")
                .WithColumn("MangaId").AsInt32().NotNullable().ForeignKey("Manga", "Id").OnDelete(Rule.Cascade)
                .WithColumn("Type").AsInt32().NotNullable()
                .WithColumn("NumberDecimal").AsDecimal(20, 8).Nullable()
                .WithColumn("NumberText").AsString(100).NotNullable()
                .WithColumn("Title").AsString().Nullable()
                .WithColumn("Monitored").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("DiscoveredFrom").AsInt32().NotNullable()
                .WithColumn("ReleaseDate").AsDateTime().Nullable()
                .WithColumn("Added").AsDateTime().NotNullable();

            Create.Index("IX_MangaItems_Manga_Type_Number")
                .OnTable("MangaItems")
                .OnColumn("MangaId").Ascending()
                .OnColumn("Type").Ascending()
                .OnColumn("NumberText").Ascending()
                .WithOptions().Unique();
            Create.Index().OnTable("MangaItems").OnColumn("MangaId").Ascending().OnColumn("NumberDecimal").Ascending();

            Create.TableForModel("MangaFiles")
                .WithColumn("MangaId").AsInt32().NotNullable().ForeignKey("Manga", "Id").OnDelete(Rule.Cascade)
                .WithColumn("Path").AsString().NotNullable().Unique()
                .WithColumn("Size").AsInt64().NotNullable()
                .WithColumn("Modified").AsDateTime().NotNullable()
                .WithColumn("DateAdded").AsDateTime().NotNullable()
                .WithColumn("OriginalFilePath").AsString().Nullable()
                .WithColumn("SceneName").AsString().Nullable()
                .WithColumn("ReleaseGroup").AsString().Nullable()
                .WithColumn("Language").AsString().Nullable()
                .WithColumn("Source").AsString().Nullable()
                .WithColumn("Quality").AsString().Nullable()
                .WithColumn("IndexerFlags").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("EditionLabel").AsString().Nullable();

            Create.Index().OnTable("MangaFiles").OnColumn("MangaId").Ascending();

            Create.TableForModel("MangaFileItems")
                .WithColumn("MangaFileId").AsInt32().NotNullable().ForeignKey("MangaFiles", "Id").OnDelete(Rule.Cascade)
                .WithColumn("MangaItemId").AsInt32().NotNullable().ForeignKey("MangaItems", "Id").OnDelete(Rule.Cascade);

            Create.Index("IX_MangaFileItems_File_Item")
                .OnTable("MangaFileItems")
                .OnColumn("MangaFileId").Ascending()
                .OnColumn("MangaItemId").Ascending()
                .WithOptions().Unique();
            Create.Index().OnTable("MangaFileItems").OnColumn("MangaItemId").Ascending();
        }
    }
}
