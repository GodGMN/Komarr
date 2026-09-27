using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Books
{
    // Distinct narrator entity, surfaced from audnex during the
    // refresh path. Modeled separately from Author because the role
    // is different — Authors are the people the user collects,
    // Narrators are contributors to audiobook editions only.
    public class Narrator : ModelBase
    {
        public string ForeignNarratorId { get; set; }
        public string Name { get; set; }
        public string CleanName { get; set; }
    }
}
