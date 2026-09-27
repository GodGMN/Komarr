using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Books
{
    public interface INarratorRepository : IBasicRepository<Narrator>
    {
        Narrator FindByForeignId(string foreignNarratorId);
        Narrator FindByCleanName(string cleanName);
        List<Narrator> FindForEdition(int editionId);
    }

    public class NarratorRepository : BasicRepository<Narrator>, INarratorRepository
    {
        public NarratorRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public Narrator FindByForeignId(string foreignNarratorId)
        {
            return Query(x => x.ForeignNarratorId == foreignNarratorId).FirstOrDefault();
        }

        public Narrator FindByCleanName(string cleanName)
        {
            return Query(x => x.CleanName == cleanName).FirstOrDefault();
        }

        public List<Narrator> FindForEdition(int editionId)
        {
            // Result is unordered — callers that need credited billing
            // should query EditionNarratorRepository directly to get the
            // Order column and arrange Narrators client-side. Keeping
            // the join SQL plain avoids the raw-SQL OrderBy form on
            // the local SqlBuilder.
            return Query(Builder()
                .Join<Narrator, EditionNarrator>((n, en) => n.Id == en.NarratorId)
                .Where<EditionNarrator>(en => en.EditionId == editionId));
        }
    }
}
