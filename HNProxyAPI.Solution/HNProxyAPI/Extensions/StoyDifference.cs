namespace HNProxyAPI.Extensions
{
    public static class StoryDifferences
    {
        /// <summary>
        /// Calculates the difference between two collections of IDs.
        /// Returns what needs to be added (in incoming but not in current) 
        /// and what needs to be removed (in current but not in incoming).
        /// Complexity: O(N) when using HashSet lookups.
        /// </summary>
        public static (ICollection<int> idsToAdd, ICollection<int> idsToDel) GetDelta(
            IEnumerable<int> incomingIds,
            IEnumerable<int> currentCachedIds)
        {
            // Optimization: If inputs are null, treat as empty
            if (incomingIds == null) incomingIds = Array.Empty<int>();
            if (currentCachedIds == null) currentCachedIds = Array.Empty<int>();

            // 1. Create HashSets for O(1) lookups.
            // Check if the input is already a HashSet to avoid allocation overhead.
            var incomingSet = incomingIds as HashSet<int> ?? new HashSet<int>(incomingIds);
            var cachedSet = currentCachedIds as HashSet<int> ?? new HashSet<int>(currentCachedIds);

            // 2. Calculate IDs to ADD: Present in Incoming, Missing in Cache
            var toAdd = new List<int>();
            foreach (var id in incomingSet)
            {
                if (!cachedSet.Contains(id))
                {
                    toAdd.Add(id);
                }
            }

            // 3. Calculate IDs to REMOVE: Present in Cache, Missing in Incoming
            var toRemove = new List<int>();
            foreach (var id in cachedSet)
            {
                if (!incomingSet.Contains(id))
                {
                    toRemove.Add(id);
                }
            }

            return (toAdd, toRemove);
        }
    }
}
