namespace HNProxyAPI.Data
{
    public static class StoryMetrics
    {
        public static long EstimateMemorySizeOf(in Story story)
        {
            // Overhead constats
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

            size += sizeof(long); // Score ou Time (se for long)
            size += sizeof(int);  // Id

            return size;
        }
    }
}
