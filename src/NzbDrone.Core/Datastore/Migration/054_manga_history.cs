using System.Data;
using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(054)]
    public class manga_history : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("MangaHistory")
                .WithColumn("MangaId").AsInt32().NotNullable().ForeignKey("Manga", "Id").OnDelete(Rule.Cascade)
                .WithColumn("MangaDownloadId").AsInt32().Nullable()
                .WithColumn("MangaDownloadFileId").AsInt32().Nullable()
                .WithColumn("EventType").AsInt32().NotNullable()
                .WithColumn("Date").AsDateTime().NotNullable()
                .WithColumn("Message").AsString().NotNullable()
                .WithColumn("ReleaseTitle").AsString().Nullable()
                .WithColumn("ReleaseGuid").AsString().Nullable()
                .WithColumn("IndexerId").AsInt32().NotNullable()
                .WithColumn("CoveredItemIds").AsString().NotNullable().WithDefaultValue("[]");
            Create.Index().OnTable("MangaHistory").OnColumn("MangaId").Ascending().OnColumn("Date").Descending();

            Create.TableForModel("MangaBlocklist")
                .WithColumn("MangaId").AsInt32().NotNullable().ForeignKey("Manga", "Id").OnDelete(Rule.Cascade)
                .WithColumn("IndexerId").AsInt32().NotNullable()
                .WithColumn("ReleaseGuid").AsString().NotNullable()
                .WithColumn("ReleaseTitle").AsString().Nullable()
                .WithColumn("Reason").AsString().NotNullable()
                .WithColumn("Added").AsDateTime().NotNullable();
            Create.Index("IX_MangaBlocklist_Release")
                .OnTable("MangaBlocklist")
                .OnColumn("MangaId").Ascending()
                .OnColumn("IndexerId").Ascending()
                .OnColumn("ReleaseGuid").Ascending()
                .WithOptions().Unique();
        }
    }
}
