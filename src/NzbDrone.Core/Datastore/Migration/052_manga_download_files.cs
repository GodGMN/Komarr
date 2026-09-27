using System.Data;
using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(052)]
    public class manga_download_files : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("MangaDownloadFiles")
                .WithColumn("MangaDownloadId").AsInt32().NotNullable().ForeignKey("MangaDownloads", "Id").OnDelete(Rule.Cascade)
                .WithColumn("Path").AsString().NotNullable()
                .WithColumn("Size").AsInt64().NotNullable()
                .WithColumn("Status").AsInt32().NotNullable()
                .WithColumn("ParsedTitle").AsString().Nullable()
                .WithColumn("MatchedAlias").AsString().Nullable()
                .WithColumn("CoveredItemIds").AsString().NotNullable().WithDefaultValue("[]")
                .WithColumn("Reason").AsString().Nullable()
                .WithColumn("ScannedAt").AsDateTime().NotNullable();

            Create.Index().OnTable("MangaDownloadFiles").OnColumn("MangaDownloadId").Ascending();
        }
    }
}
