using NzbDrone.Core.Configuration.Events;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Localization;
using NzbDrone.Core.MetadataSource.Events;

namespace NzbDrone.Core.HealthCheck.Checks
{
    // Metadata providers are replaced in a later stage. Keep the health check
    // registered so existing callers work, but never probe Open Library at boot.
    [CheckOn(typeof(ApplicationStartedEvent))]
    [CheckOn(typeof(ConfigSavedEvent))]
    [CheckOn(typeof(MetadataSourceUnavailableEvent))]
    public class MetadataSourceConnectivityCheck : HealthCheckBase
    {
        public MetadataSourceConnectivityCheck(ILocalizationService localizationService)
            : base(localizationService)
        {
        }

        public override HealthCheck Check()
        {
            return new HealthCheck(GetType());
        }
    }
}
