using System;
using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Manga
{
    public class MangaAliasLookup
    {
        public List<Manga> Candidates { get; set; } = new ();
        public bool TooBroad { get; set; }
    }

    // Built once per feed sync. Exact aliases are preferred; fallback buckets stay bounded.
    public class MangaAliasIndex
    {
        private const int MaximumCandidates = 32;
        private readonly Dictionary<string, HashSet<int>> _exact = new (StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<int>> _prefix = new (StringComparer.Ordinal);
        private readonly Dictionary<int, Manga> _manga = new ();

        public MangaAliasIndex(IEnumerable<Manga> manga)
        {
            foreach (var title in manga.Where(value => value.Monitored))
            {
                _manga[title.Id] = title;
                foreach (var alias in MangaReleaseMatcher.GetAliasTitles(title))
                {
                    var normalized = MangaReleaseMatcher.Normalize(alias);
                    Add(_exact, normalized, title);
                    if (normalized.Length >= 4)
                    {
                        Add(_prefix, normalized.Substring(0, 2), title);
                    }
                }
            }
        }

        public MangaAliasLookup Find(string parsedTitle)
        {
            var normalized = MangaReleaseMatcher.Normalize(parsedTitle);
            if (normalized.Length == 0)
            {
                return new MangaAliasLookup();
            }

            if (_exact.TryGetValue(normalized, out var exact))
            {
                return Bound(exact);
            }

            return normalized.Length >= 4 && _prefix.TryGetValue(normalized.Substring(0, 2), out var narrowed)
                ? Bound(narrowed)
                : new MangaAliasLookup();
        }

        private MangaAliasLookup Bound(HashSet<int> values)
        {
            return values.Count > MaximumCandidates
                ? new MangaAliasLookup { TooBroad = true }
                : new MangaAliasLookup { Candidates = values.Select(id => _manga[id]).ToList() };
        }

        private static void Add(Dictionary<string, HashSet<int>> index, string key, Manga manga)
        {
            if (!index.TryGetValue(key, out var titles))
            {
                titles = new HashSet<int>();
                index.Add(key, titles);
            }

            titles.Add(manga.Id);
        }
    }
}
