using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Books
{
    public interface IEditionNarratorRepository : IBasicRepository<EditionNarrator>
    {
        List<EditionNarrator> FindByEditionId(int editionId);
        List<EditionNarrator> FindByNarratorId(int narratorId);
        void DeleteByEditionId(int editionId);
    }

    public class EditionNarratorRepository : BasicRepository<EditionNarrator>, IEditionNarratorRepository
    {
        public EditionNarratorRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<EditionNarrator> FindByEditionId(int editionId)
        {
            return Query(x => x.EditionId == editionId);
        }

        public List<EditionNarrator> FindByNarratorId(int narratorId)
        {
            return Query(x => x.NarratorId == narratorId);
        }

        public void DeleteByEditionId(int editionId)
        {
            var rows = FindByEditionId(editionId);
            if (rows.Count > 0)
            {
                DeleteMany(rows);
            }
        }
    }
}
