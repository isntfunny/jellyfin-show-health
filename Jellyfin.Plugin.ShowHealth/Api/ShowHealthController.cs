using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ShowHealth.Models;
using Jellyfin.Plugin.ShowHealth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ShowHealth.Api;

/// <summary>
/// API controller for Show Health status.
/// </summary>
[ApiController]
[Route("ShowHealth")]
[Authorize]
public class ShowHealthController : ControllerBase
{
    private readonly ShowHealthAnalyzer _analyzer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowHealthController"/> class.
    /// </summary>
    public ShowHealthController(ShowHealthAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    /// <summary>
    /// Gets the health status of all TV series in the library.
    /// Compares local episodes/seasons against IMDb data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health status for all series.</returns>
    [HttpGet("Status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ShowHealthResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var result = await _analyzer.AnalyzeAsync(cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}
