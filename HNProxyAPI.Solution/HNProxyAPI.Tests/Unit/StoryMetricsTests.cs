using FluentAssertions;
using HNProxyAPI.Data;

namespace HNProxyAPI.Tests.Unit
{
    public class StoryMetricsTests
    {
        private const int BaseSize = 24 + 8 + 4; // Header(24) + Long(8) + Int(4) = 36 bytes
        private const int StringBaseCost = 8 + 26; // Reference(8) + Overhead(26) = 34 bytes

        [Fact]
        public void EstimateMemorySizeOf_Should_Return_BaseSize_When_AllStringsAreNull()
        {
            // Arrange
            // Create a Story where all properties are null except for value types (int, long/datetime)
            var story = new Story
            {
                id = 1,
                score = 100,
                // Assuming 'time' is DateTime or long. Calculation uses sizeof(long), occupying 8 bytes.
                time = DateTime.UtcNow,
                title = null,
                uri = null,
                postedBy = null
            };

            // Act
            long size = StoryMetrics.EstimateMemorySizeOf(in story);

            // #ASSERT
            // Expected calculation: 24 (Header) + 8 (Long) + 4 (Int) = 36 bytes
            size.Should().Be(36);
        }

        [Fact]
        public void EstimateMemorySizeOf_Should_Calculate_EmptyStrings_Correctly()
        {
            var story = new Story
            {
                id = 1,
                title = "",
                uri = "",
                postedBy = ""
            };
            long size = StoryMetrics.EstimateMemorySizeOf(in story);

            // #ASSERT
            // Base (36) + 3 empty strings
            // Empty string cost: 8 (Ref) + 26 (Overhead) + 0 (Chars) = 34 bytes
            // Total: 36 + (34 * 3) = 138 bytes
            size.Should().Be(138);
        }

        [Fact]
        public void EstimateMemorySizeOf_Should_Calculate_PopulatedStrings_Correctly()
        {
            var story = new Story
            {
                id = 1,
                title = "12345",      // 5 chars
                uri = "12",           // 2 chars
                postedBy = "1"        // 1 char
            };
            long size = StoryMetrics.EstimateMemorySizeOf(in story);

            // #ASSERT
            // Base: 36

            // Title: 34 (BaseStr) + (5 chars * 2 bytes) = 44 bytes
            // Uri:   34 (BaseStr) + (2 chars * 2 bytes) = 38 bytes
            // By:    34 (BaseStr) + (1 char  * 2 bytes) = 36 bytes

            // Expected Total: 36 + 44 + 38 + 36 = 154 bytes
            size.Should().Be(154);
        }

        [Fact]
        public void EstimateMemorySizeOf_Should_Handle_Mixed_Null_And_Populated_Strings()
        {
            var story = new Story
            {
                id = 99,
                title = "A",    // Present (1 char)
                uri = null,     // Null (Cost 0 in this specific implementation)
                postedBy = null // Null (Cost 0)
            };
            long size = StoryMetrics.EstimateMemorySizeOf(in story);

            // #ASSERT
            // Base: 36
            // Title: 34 + (1 * 2) = 36 bytes
            // Uri: 0
            // By: 0
            // Total: 36 + 36 = 72 bytes
            size.Should().Be(72);
        }

        [Theory]
        [InlineData(null, 36)] // Base only
        [InlineData("", 70)]   // Base(36) + StrBase(34) + 0 = 70
        [InlineData("a", 72)]  // Base(36) + StrBase(34) + 2 = 72
        [InlineData("ab", 74)] // Base(36) + StrBase(34) + 4 = 74
        public void EstimateMemorySizeOf_Should_Scale_Linearly_With_TitleLength(string title, long expectedSize)
        {
            var story = new Story
            {
                title = title,
                uri = null,
                postedBy = null
            };
            long size = StoryMetrics.EstimateMemorySizeOf(in story);

            // #ASSERT
            size.Should().Be(expectedSize);
        }
    }
}
