using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(050)]
    public class manga_quality_policy : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("Manga")
                 .AddColumn("QualityPolicy").AsString().NotNullable().WithDefaultValue("{}");
        }
    }
}
