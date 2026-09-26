using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(047)]
    public class root_folder_audiobook_quality_profile : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // The root-folder counterpart to migration 046's
            // Authors.AudiobookQualityProfileId, and it carries the same
            // meaning: 0 is "authors added here are single-format" and every
            // format falls back to DefaultQualityProfileId.
            //
            // 0 rather than NULL for the same reason as 046 -- the ORM's
            // HasOne relationship is typed Func<T, int> and treats 0 as
            // "don't load" -- and because it means every existing row is
            // already correct without a backfill.
            //
            // Note the asymmetry with DefaultQualityProfileId, which is
            // required and validated with ValidId(): 0 is a legitimate value
            // here, so RootFolderController validates this one only when it
            // is set. See the comment there.
            Alter.Table("RootFolders")
                 .AddColumn("DefaultAudiobookQualityProfileId").AsInt32().NotNullable().WithDefaultValue(0);
        }
    }
}
