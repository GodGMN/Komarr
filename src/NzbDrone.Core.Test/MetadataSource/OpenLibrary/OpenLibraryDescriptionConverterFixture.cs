using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource.OpenLibrary.Resources;

namespace NzbDrone.Core.Test.MetadataSource.OpenLibrary
{
    // OL is famously inconsistent about how it returns `description`
    // and `bio`. Every shape that has been observed in real responses
    // gets a row here so we notice regressions when Newtonsoft is
    // bumped or the OL schema drifts again.
    [TestFixture]
    public class OpenLibraryDescriptionConverterFixture
    {
        private class Wrap
        {
            [JsonConverter(typeof(OpenLibraryDescriptionConverter))]
            public string Description { get; set; }
        }

        [Test]
        public void should_read_bare_string()
        {
            var json = "{\"description\":\"a foundation story\"}";

            var wrap = JsonConvert.DeserializeObject<Wrap>(json);

            wrap.Description.Should().Be("a foundation story");
        }

        [Test]
        public void should_read_canonical_type_text_object()
        {
            var json = "{\"description\":{\"type\":\"/type/text\",\"value\":\"canonical text\"}}";

            var wrap = JsonConvert.DeserializeObject<Wrap>(json);

            wrap.Description.Should().Be("canonical text");
        }

        [Test]
        public void should_fall_back_to_text_key_when_value_missing()
        {
            // Older OL records sometimes use "text" instead of "value".
            var json = "{\"description\":{\"type\":\"/type/text\",\"text\":\"legacy text\"}}";

            var wrap = JsonConvert.DeserializeObject<Wrap>(json);

            wrap.Description.Should().Be("legacy text");
        }

        [Test]
        public void should_join_array_of_strings_with_newlines()
        {
            var json = "{\"description\":[\"first paragraph\",\"second paragraph\"]}";

            var wrap = JsonConvert.DeserializeObject<Wrap>(json);

            wrap.Description.Should().Be("first paragraph\nsecond paragraph");
        }

        [Test]
        public void should_drop_empty_strings_in_array()
        {
            var json = "{\"description\":[\"good\",\"\",\"  \",\"also good\"]}";

            var wrap = JsonConvert.DeserializeObject<Wrap>(json);

            wrap.Description.Should().Be("good\nalso good");
        }

        [Test]
        public void should_return_null_for_array_of_non_strings()
        {
            // Vanishingly rare but observed once — an array of numbers.
            // Don't throw; logging-then-null is the contract.
            var json = "{\"description\":[1,2,3]}";

            var wrap = JsonConvert.DeserializeObject<Wrap>(json);

            wrap.Description.Should().BeNull();
        }

        [Test]
        public void should_return_null_when_explicit_null()
        {
            var json = "{\"description\":null}";

            var wrap = JsonConvert.DeserializeObject<Wrap>(json);

            wrap.Description.Should().BeNull();
        }

        [Test]
        public void should_return_null_for_nested_object_value()
        {
            // {"value": {...}} with no usable scalar — safer to return
            // null than to flatten a deep blob of unknown shape.
            var json = "{\"description\":{\"type\":\"/type/text\",\"value\":{\"nested\":\"oops\"}}}";

            var wrap = JsonConvert.DeserializeObject<Wrap>(json);

            wrap.Description.Should().BeNull();
        }

        [Test]
        public void should_return_null_for_unexpected_scalar()
        {
            // A bool — never seen in the wild but the contract is
            // "don't blow up, just skip."
            var json = "{\"description\":true}";

            var wrap = JsonConvert.DeserializeObject<Wrap>(json);

            wrap.Description.Should().BeNull();
        }
    }
}
