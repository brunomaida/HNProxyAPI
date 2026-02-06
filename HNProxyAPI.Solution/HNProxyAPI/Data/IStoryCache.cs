using System.Collections;

namespace HNProxyAPI.Data
{
    /// <summary>
    /// Interface to control Story items and its order in memory
    /// </summary>
    public interface IStoryCache
    {
        bool IsEmpty { get; } // Required for Cache "Healthy" validation status.

        bool Contains(int id);

        bool TryAdd(Story story);

        void RemoveOldIds(IEnumerable<int> oldIds);

        IReadOnlyList<Story> GetOrderedList();

        Task RebuildOrderedListAsync();
    }
}
