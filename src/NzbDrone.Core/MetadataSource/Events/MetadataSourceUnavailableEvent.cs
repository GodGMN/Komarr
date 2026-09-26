using System;
using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.MetadataSource.Events
{
    // Raised when the metadata source has refused us often enough in a row that
    // we stop asking. MetadataSourceConnectivityCheck listens for it, so the
    // health page reflects the outage while it is happening rather than only at
    // startup.
    public class MetadataSourceUnavailableEvent : IEvent
    {
        public DateTime UnavailableUntil { get; private set; }

        public MetadataSourceUnavailableEvent(DateTime unavailableUntil)
        {
            UnavailableUntil = unavailableUntil;
        }
    }
}
