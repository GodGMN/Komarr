using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NzbDrone.Core.Test.MetadataSource.OpenLibrary.Fixtures
{
    // Phase 8b cassette loader. Reads JSON files committed under
    // src/NzbDrone.Core.Test/Files/OpenLibrary/ — the .csproj already
    // copies that directory to the test output via the
    // `<None Update="Files\**\*.*">` rule.
    //
    // The corpus the master plan calls for (100+ works across the
    // categories in Files/OpenLibrary/README.md) is captured live
    // from openlibrary.org and not yet present. Fixtures using this
    // loader should annotate themselves [Explicit] until the
    // matching cassette is checked in.
    public static class OpenLibraryFixtureLoader
    {
        private const string SubDir = "OpenLibrary";

        public static T Load<T>(string fileName)
            where T : class
        {
            var json = LoadJson(fileName);
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static string LoadJson(string fileName)
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Files", SubDir, fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"OL cassette not found: {fileName}. " +
                    $"Capture it per Files/OpenLibrary/README.md and commit the JSON.",
                    path);
            }

            return File.ReadAllText(path);
        }

        public static bool Exists(string fileName)
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Files", SubDir, fileName);
            return File.Exists(path);
        }
    }
}
