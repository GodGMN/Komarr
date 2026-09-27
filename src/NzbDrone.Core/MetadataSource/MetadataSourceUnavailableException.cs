using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.MetadataSource
{
    // Thrown instead of issuing a request while the metadata source is known to
    // be refusing us. Deliberately an NzbDroneException so the import's existing
    // "skip this search" handlers in CandidateService already catch it — a
    // tripped breaker should look like a failed lookup to callers, just an
    // instant one that costs no HTTP.
    public class MetadataSourceUnavailableException : NzbDroneException
    {
        public MetadataSourceUnavailableException(string message, params object[] args)
            : base(message, args)
        {
        }

        public MetadataSourceUnavailableException(string message)
            : base(message)
        {
        }
    }
}
