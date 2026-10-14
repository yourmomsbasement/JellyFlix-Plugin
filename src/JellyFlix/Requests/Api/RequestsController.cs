using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using JellyFlix.Requests.Api.Models;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JellyFlix.Requests.Api;

/// <summary>
/// Requesting titles that aren't in the library, through Seerr.
/// </summary>
[ApiController]
[Authorize]
[Route("JellyFlix/Requests")]
[Produces("application/json")]
public class RequestsController : ControllerBase
{
    private readonly RequestService _requestService;
    private readonly IAuthorizationContext _authorizationContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestsController"/> class.
    /// </summary>
    /// <param name="requestService">The request service.</param>
    /// <param name="authorizationContext">The authorization context.</param>
    public RequestsController(RequestService requestService, IAuthorizationContext authorizationContext)
    {
        _requestService = requestService;
        _authorizationContext = authorizationContext;
    }

    /// <summary>
    /// Finds movies and shows matching a search that aren't in the library.
    /// </summary>
    /// <param name="query">The search text.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The titles.</returns>
    [HttpGet("Search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<RequestSearchDto>> Search([FromQuery, Required] string query, CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync().ConfigureAwait(false);
        try
        {
            return await _requestService.SearchAsync(userId, query, cancellationToken).ConfigureAwait(false);
        }
        catch (SeerrException ex)
        {
            return Problem(ex);
        }
    }

    /// <summary>
    /// Requests a movie, or every season of a show.
    /// </summary>
    /// <param name="request">What to request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Nothing.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Create([FromBody, Required] CreateRequestDto request, CancellationToken cancellationToken)
    {
        var userId = await GetUserIdAsync().ConfigureAwait(false);
        try
        {
            await _requestService.RequestAsync(userId, request, cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (SeerrException ex)
        {
            return Problem(ex);
        }
    }

    /// <summary>
    /// Checks the Seerr connection with the saved settings.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The Seerr version.</returns>
    [HttpPost("Test")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<SeerrTestDto>> Test(CancellationToken cancellationToken)
    {
        try
        {
            return new SeerrTestDto { Version = await _requestService.TestAsync(cancellationToken).ConfigureAwait(false) };
        }
        catch (SeerrException ex)
        {
            return Problem(ex);
        }
    }

    private async Task<Guid> GetUserIdAsync()
        => (await _authorizationContext.GetAuthorizationInfo(HttpContext).ConfigureAwait(false)).UserId;

    private ObjectResult Problem(SeerrException ex)
    {
        // Seerr's own authentication failures mean the server's API key is wrong, not that the user is signed out
        var status = (int)ex.StatusCode is StatusCodes.Status401Unauthorized ? StatusCodes.Status502BadGateway : (int)ex.StatusCode;
        return Problem(detail: ex.Message, statusCode: status);
    }
}
