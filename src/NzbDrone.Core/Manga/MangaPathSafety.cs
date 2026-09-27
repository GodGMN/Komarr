using System;
using System.IO;

namespace NzbDrone.Core.Manga
{
    public static class MangaPathSafety
    {
        public static bool IsInside(string root, string path)
        {
            var relative = Path.GetRelativePath(root, path);
            return relative != ".." && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
        }

        public static bool IsInsidePhysicalRoot(string root, string folder)
        {
            if (!IsInside(root, folder))
            {
                return false;
            }

            var rootInfo = new DirectoryInfo(root);
            var resolvedRoot = rootInfo.Exists && rootInfo.LinkTarget != null ? rootInfo.ResolveLinkTarget(true).FullName : rootInfo.FullName;
            var current = resolvedRoot;
            var relative = Path.GetRelativePath(root, folder);
            if (relative == ".")
            {
                return true;
            }

            foreach (var part in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                var next = new DirectoryInfo(Path.Combine(current, part));
                if (next.Exists && next.LinkTarget != null)
                {
                    current = next.ResolveLinkTarget(true).FullName;
                }
                else
                {
                    var file = new FileInfo(next.FullName);
                    current = file.Exists && file.LinkTarget != null ? file.ResolveLinkTarget(true).FullName : next.FullName;
                }

                if (!IsInside(resolvedRoot, current))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
