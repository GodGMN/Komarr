namespace NzbDrone.Integration.Test
{
    // The authors the integration suite adds and asserts on, in one place
    // because six fixtures share them.
    //
    // These were Goodreads numerics inherited from upstream (14586394,
    // 383606) alongside Goodreads edition ids used to look the authors up
    // (43765115, 16160797). None of those identify anything under
    // OpenLibrary, which is why every fixture that touched them carried a
    // stale "Waiting for metadata to be back again" marker.
    //
    // Chosen for stability rather than fame: each has a modest, settled body
    // of work, so a fixture is not one bulk OpenLibrary edit away from a
    // different book count. Verify with
    // https://openlibrary.org/authors/{id}.json before changing one.
    public static class OpenLibraryFixtureData
    {
        // 6 works, top work "The Last Day". The Calendar fixture depends on
        // this author having a dated book; see the note there about why the
        // date is discovered at run time rather than hard-coded.
        public const string AndrewHunterMurrayId = "OL7822211A";
        public const string AndrewHunterMurrayName = "Andrew Hunter Murray";

        // The Cormoran Strike pseudonym. OpenLibrary has two records under
        // this name: OL10720328A with 34 works and this one with 8. The
        // smaller is the right choice and not by a small margin -- adding the
        // 34-work record refreshes every one of them from OpenLibrary at
        // roughly a second each, and get_all_author failed with "Commands
        // still processing" after CommandClient.WaitAll's 60s timeout. The
        // suite only needs "a second, different author".
        public const string RobertGalbraithId = "OL1960016A";
        public const string RobertGalbraithName = "Robert Galbraith";

        // Lookup-only, never added to a library. 4 works.
        public const string PhilipErringtonId = "OL1422008A";
        public const string PhilipErringtonName = "Philip W. Errington";
    }
}
