using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Books
{
    // Join row between an audiobook Edition and a Narrator. Order
    // preserves the credited billing for dual-cast titles.
    public class EditionNarrator : ModelBase
    {
        public int EditionId { get; set; }
        public int NarratorId { get; set; }
        public int Order { get; set; }
    }
}
