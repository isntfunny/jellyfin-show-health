using System;
using System.Text.RegularExpressions;
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

    /// <summary>
    /// Returns all series from the Jellyfin library instantly (no IMDb calls).
    /// </summary>
    /// <returns>List of series with basic Jellyfin data.</returns>
    [HttpGet("Series")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<SeriesListResponse> GetSeries()
    {
        var series = _analyzer.GetSeriesList();
        return Ok(series);
    }

    /// <summary>
    /// Analyzes a single series against IMDb by its IMDb ID.
    /// </summary>
    /// <param name="imdbId">The IMDb ID (e.g. tt1234567).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health result for the series, or 404 if not found.</returns>
    [HttpGet("Analyze/{imdbId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeriesHealthResult>> AnalyzeSeries(string imdbId, CancellationToken cancellationToken)
    {
        if (!Regex.IsMatch(imdbId, @"^tt\d{7,10}$", RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            return BadRequest("Invalid IMDb ID format");
        }

        var result = await _analyzer.AnalyzeSeriesAsync(imdbId, cancellationToken).ConfigureAwait(false);
        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }
}
