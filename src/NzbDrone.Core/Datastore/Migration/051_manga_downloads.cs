using System.Data;
using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(051)]
    public class manga_downloads : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("MangaDownloads")
                .WithColumn("MangaId").AsInt32().NotNullable().ForeignKey("Manga", "Id").OnDelete(Rule.Cascade)
                .WithColumn("RequestedItemId").AsInt32().Nullable()
                .WithColumn("CoveredItemIds").AsString().NotNullable().WithDefaultValue("[]")
                .WithColumn("IndexerId").AsInt32().NotNullable()
                .WithColumn("Indexer").AsString().Nullable()
                .WithColumn("ReleaseGuid").AsString().NotNullable()
                .WithColumn("ReleaseTitle").AsString().NotNullable()
                .WithColumn("Protocol").AsInt32().NotNullable()
                .WithColumn("DownloadClientId").AsInt32().NotNullable()
                .WithColumn("DownloadClient").AsString().NotNullable()
                .WithColumn("DownloadId").AsString().Nullable()
                .WithColumn("Status").AsInt32().NotNullable()
                .WithColumn("Error").AsString().Nullable()
                .WithColumn("Added").AsDateTime().NotNullable()
                .WithColumn("LastUpdated").AsDateTime().Nullable();

            Create.Index().OnTable("MangaDownloads").OnColumn("MangaId").Ascending();
            Create.Index().OnTable("MangaDownloads").OnColumn("DownloadClientId").Ascending().OnColumn("DownloadId").Ascending();
        }
    }
}
