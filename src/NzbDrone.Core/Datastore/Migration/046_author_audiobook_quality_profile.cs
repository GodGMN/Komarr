using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(046)]
    public class author_audiobook_quality_profile : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Second quality profile per author, used for audiobook-format
            // releases. 0 means "this author is single-format" and every
            // format falls back to QualityProfileId — which is exactly what
            // every existing row gets, so this needs no backfill and changes
            // no behaviour until a user sets it.
            //
            // 0 rather than NULL deliberately: the ORM's HasOne relationship
            // (TableMapper.HasOne) is typed Func<T, int> and already treats
            // an id of 0 as "don't load", so a non-nullable column with a 0
            // default fits the existing lazy-load contract without special
            // casing.
            Alter.Table("Authors")
                 .AddColumn("AudiobookQualityProfileId").AsInt32().NotNullable().WithDefaultValue(0);
        }
    }
}
