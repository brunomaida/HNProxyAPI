using FluentAssertions;
using HNProxyAPI.Data;
using System.Text.Json;

namespace HNProxyAPI.Tests.Unit
{
    public class StoryConverterTests
    {
        private readonly JsonSerializerOptions _options;

        public StoryConverterTests()
        {
            _options = new JsonSerializerOptions();
            // Assuming your converter is registered or added to options here
            _options.Converters.Add(new StoryConverter());
        }

        [Fact]
        public void Deserialize_RealHackerNewsJson_ShouldSucceed_1()
        {
            // Real JSON sample with extra fields like 'kids' and 'descendants'
            string json = """
            {
                "by": "dhouston",
                "descendants": 71,
                "id": 8863,
                "kids": [ 8952, 9224, 8917 ],
                "score": 111,
                "time": 1175714200,
                "title": "My YC app: Dropbox - Throw away your USB drive",
                "type": "story",
                "url": "http://www.getdropbox.com/u/2/screencast.html"
            }
            """;

            // Act
            var story = JsonSerializer.Deserialize<Story>(json, _options);

            // Assert
            story.Should().NotBeNull();
            story.id.Should().Be(8863);
            story.score.Should().Be(111);
            story.title.Should().Contain("Dropbox");
            story.time.Year.Should().Be(2007);
            story.time.Month.Should().Be(4);
        }

        [Fact]
        public void Deserialize_WhenTimeIsMissing_ShouldNotThrow()
        {
            // Arrange - JSON without the 'time' property
            string json = @"{ ""id"": 123, ""title"": ""No Time Story"" }";

            // Act
            var story = JsonSerializer.Deserialize<Story>(json, _options);

            // Assert
            story.id.Should().Be(123);
            story.time.Should().Be(default(DateTime));
        }

        [Fact]
        public void Deserialize_WhenFieldsAreInDifferentOrder_ShouldStillWork()
        {
            // Arrange - JSON order changed
            string json = @"{ ""title"": ""Ordered"", ""id"": 999, ""score"": 10 }";

            // Act
            var story = JsonSerializer.Deserialize<Story>(json, _options);

            // Assert
            story.id.Should().Be(999);
            story.title.Should().Be("Ordered");
            story.score.Should().Be(10);
        }

        [Fact]
        public void Deserialize_InvalidTimeFormat_ShouldHandleDefensively()
        {
            // Arrange - Time as a string instead of number (if your converter supports it)
            string json = @"{ ""id"": 1, ""time"": ""1175714200"" }";

            // Act
            var story = JsonSerializer.Deserialize<Story>(json, _options);

            // Assert
            // With the TryParse logic added previously, this should now pass
            story.time.Year.Should().Be(2007);
        }

        [Fact]
        public void Serialize_StoryObject_ShouldProduceValidJson()
        {
            // Arrange
            var story = new Story
            {
                id = 55,
                title = "Export Test",
                score = 20,
                time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            // Act
            var json = JsonSerializer.Serialize(story, _options);

            // Assert
            json.Should().Contain(@"""id"":55");
            json.Should().Contain(@"""title"":""Export Test""");
            json.Should().Contain(@"""score"":20");
            json.Should().Contain(@"""time"":""2026-01-01T00:00:00Z""");
        }
    }
}