using FluentAssertions;
using HNProxyAPI.Extensions;

namespace HNProxyAPI.Tests.Unit
{
    public class DeltaCalculatorTests
    {
        [Fact]
        public void GetDelta_Should_Identify_New_And_Removed_Items_Correctly()
        {
            var currentCache = new[] { 1, 2, 3, 4, 5 };
            var incomingApi = new[] { 3, 4, 5, 6, 7 };
            // Logic: 
            // 1 and 2 are gone (Remove)
            // 3, 4, 5 stayed (Do nothing)
            // 6 and 7 are new (Add)

            var (toAdd, toRemove) = StoryDifferences.GetDelta(incomingApi, currentCache);

            // #ASSERT
            toAdd.Should().HaveCount(2);
            toAdd.Should().Contain(new[] { 6, 7 });

            toRemove.Should().HaveCount(2);
            toRemove.Should().Contain(new[] { 1, 2 });
        }

        [Fact]
        public void GetDelta_Should_Handle_Empty_Cache_ColdStart()
        {
            var currentCache = Array.Empty<int>();
            var incomingApi = new[] { 10, 20, 30 };
            var (toAdd, toRemove) = StoryDifferences.GetDelta(incomingApi, currentCache);

            // #ASSERT
            toAdd.Should().BeEquivalentTo(incomingApi);
            toRemove.Should().BeEmpty();
        }

        [Fact]
        public void GetDelta_Should_Handle_Empty_Api_FullCleanup()
        {
            var currentCache = new[] { 10, 20, 30 };
            var incomingApi = Array.Empty<int>();
            var (toAdd, toRemove) = StoryDifferences.GetDelta(incomingApi, currentCache);

            // #ASSERT
            toAdd.Should().BeEmpty();
            toRemove.Should().BeEquivalentTo(currentCache);
        }

        [Fact]
        public void GetDelta_Should_Handle_No_Changes()
        {
            var currentCache = new[] { 1, 2, 3 };
            var incomingApi = new[] { 1, 2, 3 };
            var (toAdd, toRemove) = StoryDifferences.GetDelta(incomingApi, currentCache);

            // #ASSERT
            toAdd.Should().BeEmpty();
            toRemove.Should().BeEmpty();
        }

        [Fact]
        public void GetDelta_Should_Ignore_Duplicates_In_Input()
        {
            var currentCache = new[] { 1 };
            var incomingApi = new[] { 2, 2, 2, 3 }; // API sends duplicates by mistake
            var (toAdd, toRemove) = StoryDifferences.GetDelta(incomingApi, currentCache);

            // #ASSERT
            toAdd.Should().Contain(new[] { 2, 3 });
            toAdd.Count.Should().Be(2, "HashSet logic should deduplicate inputs automatically");
        }
    }
}
