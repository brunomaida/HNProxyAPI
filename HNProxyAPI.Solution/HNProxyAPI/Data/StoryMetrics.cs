namespace HNProxyAPI.Data
{
    /// <summary>
    /// Specific Story functions
    /// </summary>
    public static class StoryMetrics
    {
        /// <summary>
        /// Estimates the memory size of a Story object
        /// </summary>
        /// <param name="story">The record struct object</param>
        /// <returns>The size of memoty in bytes</returns>
        public static long EstimateMemorySizeOf(in Story story)
        {
            // Overhead constants
            const int ObjectHeaderSize = 24;
            const int ReferenceSize = 8;
            const int StringOverhead = 26;

            long size = ObjectHeaderSize;

            if (story.title != null)
                size += ReferenceSize + StringOverhead + story.title.Length * 2;

            if (story.uri != null)
                size += ReferenceSize + StringOverhead + story.uri.Length * 2;

            if (story.postedBy != null)
                size += ReferenceSize + StringOverhead + story.postedBy.Length * 2;

            size += sizeof(long); // Score or Time 
            size += sizeof(int);  // Id

            return size;
        }
    }
}
