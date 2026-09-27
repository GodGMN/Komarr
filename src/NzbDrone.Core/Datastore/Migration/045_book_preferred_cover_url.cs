using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(045)]
    public class book_preferred_cover_url : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // User-pinned cover URL (cover picker modal). Null = use the
            // mapper's pick (work.covers[0] when available, else the
            // monitored edition's cover_i). Persists across refresh via
            // Book.UseDbFieldsFrom.
            Alter.Table("Books").AddColumn("PreferredCoverUrl").AsString().Nullable();
        }
    }
}
