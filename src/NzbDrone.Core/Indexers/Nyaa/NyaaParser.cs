using System.Xml.Linq;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Indexers.Nyaa
{
    // Nyaa.si exposes size, seeders, leechers and infohash as nyaa-namespaced
    // elements instead of the description text TorrentRssParser reads.
    public class NyaaParser : TorrentRssParser
    {
        private static readonly XNamespace NyaaNamespace = "https://nyaa.si/xmlns/nyaa";

        protected override int? GetSeeders(XElement item)
        {
            var seeders = (int?)item.Element(NyaaNamespace + "seeders");

            if (seeders.HasValue)
            {
                return seeders.Value;
            }

            return base.GetSeeders(item);
        }

        protected override int? GetPeers(XElement item)
        {
            var seeders = (int?)item.Element(NyaaNamespace + "seeders");
            var leechers = (int?)item.Element(NyaaNamespace + "leechers");

            if (seeders.HasValue || leechers.HasValue)
            {
                return (seeders ?? 0) + (leechers ?? 0);
            }

            return base.GetPeers(item);
        }

        protected override long GetSize(XElement item)
        {
            var size = base.GetSize(item);

            if (size == 0)
            {
                size = ParseSize((string)item.Element(NyaaNamespace + "size"), true);
            }

            return size;
        }

        protected override string GetInfoHash(XElement item)
        {
            var infoHash = (string)item.Element(NyaaNamespace + "infoHash");

            if (infoHash.IsNotNullOrWhiteSpace())
            {
                return infoHash;
            }

            return base.GetInfoHash(item);
        }
    }
}
