using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using JellyFlix.Companion.Api.Models;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JellyFlix.Companion.Api;

/// <summary>
/// What to offer when a title ends.
/// </summary>
[ApiController]
[Authorize]
[Route("JellyFlix/Companion")]
[Produces("application/json")]
public class CompanionController : ControllerBase
{
    private readonly CompanionService _companionService;
    private readonly IAuthorizationContext _authorizationContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanionController"/> class.
    /// </summary>
    /// <param name="companionService">The companion service.</param>
    /// <param name="authorizationContext">The authorization context.</param>
    public CompanionController(CompanionService companionService, IAuthorizationContext authorizationContext)
    {
        _companionService = companionService;
        _authorizationContext = authorizationContext;
    }

    /// <summary>
    /// Gets the next title in the collection, the extras and the soundtrack for a title that finished.
    /// </summary>
    /// <param name="itemId">The title.</param>
    /// <returns>The offers.</returns>
    [HttpGet("{itemId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanionDto>> Get([FromRoute, Required] Guid itemId)
    {
        var user = (await _authorizationContext.GetAuthorizationInfo(HttpContext).ConfigureAwait(false)).User;
        if (user is null)
        {
            return Unauthorized();
        }

        var result = _companionService.Get(user, itemId);
        return result is null ? NotFound() : result;
    }
}
