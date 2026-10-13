using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JellyFlix.WebUpdater.Api;

/// <summary>
/// Web Updater status and manual checks, for the JellyFlix settings page. Administrators only.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("JellyFlix/WebUpdate")]
[Produces("application/json")]
public class WebUpdateController : ControllerBase
{
    private readonly WebUpdateService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebUpdateController"/> class.
    /// </summary>
    /// <param name="service">The web update service.</param>
    public WebUpdateController(WebUpdateService service)
    {
        _service = service;
    }

    /// <summary>
    /// Gets the Web Updater status.
    /// </summary>
    /// <response code="200">Status returned.</response>
    /// <returns>The status.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<WebUpdateStatus> GetStatus() => _service.GetStatus();

    /// <summary>
    /// Checks for and installs a new web client now.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">Check finished; the status says what happened.</response>
    /// <returns>The status.</returns>
    [HttpPost("Check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<WebUpdateStatus>> Check(CancellationToken cancellationToken)
        => await _service.CheckAndInstallAsync(cancellationToken).ConfigureAwait(false);
}
