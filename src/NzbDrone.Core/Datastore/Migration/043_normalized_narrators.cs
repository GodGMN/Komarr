using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(043)]
    public class normalized_narrators : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Phase-2 step on the narrators schema. Migration 042 added a flat
            // comma-separated `Editions.Narrators` string column on the grounds
            // that "future moves to a dedicated table can migrate from this
            // column without an API break." This is that future move.
            //
            // Two tables go in alongside the existing string column:
            //   * `Narrators` records distinct narrator entities (Name +
            //     optional ForeignNarratorId for audnex's `key`). CleanName
            //     is the search-normalized form used by repository lookups.
            //   * `EditionNarrators` is the many-to-many join with an
            //     explicit `Order` so dual-cast narrator credits ("Read
            //     by A and B") preserve their stated billing.
            //
            // The `Editions.Narrators` string column is deliberately NOT
            // dropped in this migration. AudnexProxy dual-writes it during
            // the transition so existing readers (EditionResource DTO,
            // BookDetailsHeader UI, the AudnexProxyFixture tests, and the
            // Edition.UseDbFieldsFrom merge) keep working without a breaking
            // change. Once those consumers are migrated to read the join,
            // a follow-up migration drops the column.
            Create.Table("Narrators")
                .WithColumn("Id").AsInt32().NotNullable().PrimaryKey().Identity()
                .WithColumn("ForeignNarratorId").AsString().Nullable()
                .WithColumn("Name").AsString().NotNullable()
                .WithColumn("CleanName").AsString().NotNullable();

            Create.Index().OnTable("Narrators").OnColumn("CleanName").Ascending();
            Create.Index().OnTable("Narrators").OnColumn("ForeignNarratorId").Ascending();

            Create.Table("EditionNarrators")
                .WithColumn("Id").AsInt32().NotNullable().PrimaryKey().Identity()
                .WithColumn("EditionId").AsInt32().NotNullable()
                .WithColumn("NarratorId").AsInt32().NotNullable()
                .WithColumn("Order").AsInt32().NotNullable().WithDefaultValue(0);

            Create.Index().OnTable("EditionNarrators").OnColumn("EditionId").Ascending();
            Create.Index().OnTable("EditionNarrators").OnColumn("NarratorId").Ascending();
        }
    }
}
