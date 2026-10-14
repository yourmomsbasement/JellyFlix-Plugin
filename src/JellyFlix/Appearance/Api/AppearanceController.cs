using System.Threading.Tasks;
using JellyFlix.Appearance.Api.Models;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JellyFlix.Appearance.Api;

/// <summary>
/// The interface's seasonal look and color settings.
/// </summary>
[ApiController]
[Authorize]
[Route("JellyFlix/Appearance")]
[Produces("application/json")]
public class AppearanceController : ControllerBase
{
    private readonly AppearanceService _appearanceService;
    private readonly IAuthorizationContext _authorizationContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppearanceController"/> class.
    /// </summary>
    /// <param name="appearanceService">The appearance service.</param>
    /// <param name="authorizationContext">The authorization context.</param>
    public AppearanceController(AppearanceService appearanceService, IAuthorizationContext authorizationContext)
    {
        _appearanceService = appearanceService;
        _authorizationContext = authorizationContext;
    }

    /// <summary>
    /// Gets the current season, its curated shelves and whether dynamic colors are on.
    /// </summary>
    /// <returns>The look.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<AppearanceDto>> Get()
    {
        var user = (await _authorizationContext.GetAuthorizationInfo(HttpContext).ConfigureAwait(false)).User;
        return user is null ? Unauthorized() : _appearanceService.Get(user);
    }
}
