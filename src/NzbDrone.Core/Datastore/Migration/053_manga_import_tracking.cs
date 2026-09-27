using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(053)]
    public class manga_import_tracking : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("MangaDownloadFiles").AddColumn("MangaFileId").AsInt32().Nullable();
            Alter.Table("MangaDownloadFiles").AddColumn("ImportedAt").AsDateTime().Nullable();
        }
    }
}
