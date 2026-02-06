using HNProxyAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace HackerNewsQueryAPI.Controllers
{
    /// <summary>
    /// Main controller for functions related to Hacker news best stories.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class BestStoriesController : ControllerBase
    {
        private readonly ILogger<BestStoriesController> _logger;
        private readonly HackerNewsQueryService _hackerNewsService;

        public BestStoriesController(ILogger<BestStoriesController> logger, 
                                     HackerNewsQueryService hackerNewsService)
        {
            _logger = logger;
            _hackerNewsService = hackerNewsService;
        }

        /// <summary>
        /// Retrieves the N first stories in descending order by score.
        /// </summary>
        /// <param name="n">First N records.</param>
        /// <param name="ct">The Cancellation token used in the entire request.</param>
        /// <returns></returns>
        [HttpGet(Name = "GetBestStories")]
        public async Task<IActionResult> Get([FromQuery] int n, CancellationToken ct)
        {
            if (n <= 0)
            {
                _logger.LogWarning("GetBestStories called with non-positive n (first ones): {n}", n);
                return BadRequest("'n' parameter must greater than 0. Used as reference for the first 'n' stories.");
            }

            try
            {
                var allStories = await _hackerNewsService.GetBestStoriesOrderedByScoreAsync(ct);
                var result = allStories.Take(n);
                int totalStories = allStories.Count();
                if (n > result.Count())
                    _logger.LogInformation("n({n}) > countof Stories({totalStories}): Will return all the ordered list in memory.", n, totalStories);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while retrieving the best stories.");
                return StatusCode(500, "An internal error has occured while retrieving the best stories.");
            }
        }
    }
}
