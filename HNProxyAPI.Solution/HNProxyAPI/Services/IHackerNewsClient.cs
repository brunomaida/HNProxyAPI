using HNProxyAPI.Data;

namespace HNProxyAPI.Services
{
    public interface IHackerNewsClient
    {
        Task<int[]> GetCurrentStoriesAsync(CancellationToken ct);
        Task<Story> GetStoryDetailsAsync(int id, CancellationToken ct);
    }
}
