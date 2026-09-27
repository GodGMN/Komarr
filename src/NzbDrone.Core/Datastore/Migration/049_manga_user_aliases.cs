using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(049)]
    public class manga_user_aliases : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("Manga")
                 .AddColumn("UserAliases").AsString().NotNullable().WithDefaultValue("[]");
        }
    }
}
