using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(042)]
    public class edition_narrators : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Audiobook editions need a narrator field — the primary value
            // audnex augmentation surfaces that the existing BookInfo
            // schema doesn't capture. Stored as a comma-separated string
            // rather than a normalized table because:
            //   * Editions almost always have a single narrator; the rare
            //     dual-cast case (e.g. "George Guidall, Frank Muller") is
            //     fine as a flat string.
            //   * The Author table tracks people the *user is collecting*,
            //     not arbitrary contributors. Modeling narrators as authors
            //     would conflate two different roles.
            //   * Schema-stable: future moves to a dedicated table can
            //     migrate from this column without an API break.
            // Nullable for backwards compat with rows added before this
            // migration — RefreshBookService writes it on next refresh.
            Alter.Table("Editions")
                .AddColumn("Narrators").AsString().Nullable();
        }
    }
}
