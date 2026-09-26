using NzbDrone.Common.EnvironmentInfo;

namespace NzbDrone.Core.MetadataSource
{
    // Single source of truth for how Librarr identifies itself to the public
    // metadata APIs it depends on — OpenLibrary, Wikidata and Audnex.
    //
    // All three ask third-party clients to send an application name AND working
    // contact info, and two of them tie rate limits to it directly:
    //
    //   * OpenLibrary grants a 3x rate allowance to identified clients
    //     (https://openlibrary.org/developers/api#politeness) and explicitly
    //     asks not to be used as a third-party data backend at all;
    //   * Wikidata enforces its User-Agent policy strictly and rate-limits
    //     anonymous queries fast
    //     (https://meta.wikimedia.org/wiki/User-Agent_policy).
    //
    // Three call sites previously advertised https://github.com/Librarr/Librarr,
    // which does not exist — so the contact requirement was met in form only,
    // and neither service had a working way to reach us before blocking us. A
    // fourth (cover downloads) sent a spoofed Android/Dalvik string inherited
    // from upstream Readarr, where it existed to get images past
    // Goodreads/Amazon.
    //
    // Keep this honest and keep it in one place. Being identifiable is what
    // earns the higher limits, and it is what lets these services tell us to
    // back off before they cut us off.
    //
    // NOTE: MetadataSource/Goodreads/GoodreadsProxy.cs still sends spoofed
    // Dalvik and "Goodreads/3.33.1 (iPhone; iOS 14.3)" strings. That is
    // deliberately left alone for now — Goodreads likely blocks non-app
    // clients, which is why upstream spoofed it, so changing it is a behaviour
    // change rather than a correctness fix. Revisit when that legacy path goes.
    public static class MetadataUserAgent
    {
        public const string ContactUrl = "https://github.com/Rorqualx/Librarr";

        public static string Value => $"{BuildInfo.AppName}/{BuildInfo.Version} (+{ContactUrl})";

        // Variant carrying a short note about what the traffic is for, so an
        // operator reading their logs can tell our request streams apart.
        public static string For(string purpose)
        {
            return $"{BuildInfo.AppName}/{BuildInfo.Version} (+{ContactUrl}; {purpose})";
        }
    }
}
