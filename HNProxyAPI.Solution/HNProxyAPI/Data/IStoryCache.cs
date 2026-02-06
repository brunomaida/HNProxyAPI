using System.Collections;

namespace HNProxyAPI.Data
{
    /// <summary>
    /// Interface to control Story items and its order in memory
    /// </summary>
    public interface IStoryCache
    {
        /// <summary>
        /// Returns if cache (memory) is empty (has no data)
        /// </summary>
        bool IsEmpty { get; } // Required for Cache "Healthy" validation status

        /// <summary>
        /// Checks if corresponding ID is in cache
        /// </summary>
        /// <param name="id">Story ID</param>
        /// <returns>If the cache contains the ID</returns>
        bool Contains(int id);

        /// <summary>
        /// Tries to add a new Story into the cache
        /// </summary>
        /// <param name="story">Current story to add</param>
        /// <returns>If the operation succeeds</returns>
        bool TryAdd(Story story);

        /// <summary>
        /// Removes the listed IDs from the cache
        /// </summary>
        /// <param name="oldIds">List of Story IDs</param>
        void RemoveOldIds(IEnumerable<int> oldIds);

        /// <summary>
        /// Retrieves the ordered list of Stories in cache
        /// </summary>
        /// <returns>Descending ordered list of Stories</returns>
        IReadOnlyList<Story> GetOrderedList();

        /// <summary>
        /// Asynchronosly rebuilds the list in desceding order
        /// </summary>
        /// <returns>The process to execute the reorder mechanism</returns>
        Task RebuildOrderedListAsync();
    }
}
