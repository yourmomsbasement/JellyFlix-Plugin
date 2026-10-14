using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JellyFlix.Search.Api.Models;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JellyFlix.Search.Api;

/// <summary>
/// Searching the library by description.
/// </summary>
[ApiController]
[Authorize]
[Route("JellyFlix/Search")]
[Produces("application/json")]
public class SearchController : ControllerBase
{
    private const int MaxLimit = 50;

    private static readonly DtoOptions _dtoOptions = new()
    {
        Fields = [ItemFields.Overview, ItemFields.Genres, ItemFields.PrimaryImageAspectRatio, ItemFields.ChildCount],
        ImageTypes = [ImageType.Primary, ImageType.Backdrop, ImageType.Logo],
        ImageTypeLimit = 1,
        EnableUserData = true
    };

    private readonly DescriptionSearchService _searchService;
    private readonly IDtoService _dtoService;
    private readonly IAuthorizationContext _authorizationContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchController"/> class.
    /// </summary>
    /// <param name="searchService">The description search service.</param>
    /// <param name="dtoService">The DTO service.</param>
    /// <param name="authorizationContext">The authorization context.</param>
    public SearchController(DescriptionSearchService searchService, IDtoService dtoService, IAuthorizationContext authorizationContext)
    {
        _searchService = searchService;
        _dtoService = dtoService;
        _authorizationContext = authorizationContext;
    }

    /// <summary>
    /// Finds movies and shows matching a description, such as "space movie with a stranded astronaut".
    /// </summary>
    /// <param name="query">The description.</param>
    /// <param name="limit">How many titles at most.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The titles, best first.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<DescriptionSearchDto>> Search([FromQuery, Required] string query, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        var auth = await _authorizationContext.GetAuthorizationInfo(HttpContext).ConfigureAwait(false);
        if (auth.User is null)
        {
            return Unauthorized();
        }

        var (items, isSemantic) = await _searchService.SearchAsync(auth.User, query, Math.Clamp(limit, 1, MaxLimit), cancellationToken).ConfigureAwait(false);
        return new DescriptionSearchDto
        {
            Items = _dtoService.GetBaseItemDtos(items, _dtoOptions, auth.User, skipVisibilityCheck: true),
            IsSemantic = isSemantic
        };
    }

    /// <summary>
    /// Gets the state of the search indexes.
    /// </summary>
    /// <returns>The status.</returns>
    [HttpGet("Status")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<SearchStatusDto>> Status()
        => await _searchService.GetStatusAsync().ConfigureAwait(false);

    /// <summary>
    /// Checks the embeddings API with the saved settings.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The model and its vector size.</returns>
    [HttpPost("Test")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<SemanticTestDto>> Test(CancellationToken cancellationToken)
    {
        try
        {
            return await _searchService.TestAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException { InnerException: TimeoutException })
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    /// <summary>
    /// Starts embedding the titles that are new or changed. Follow along with <c>GET Status</c>.
    /// </summary>
    /// <returns>The status.</returns>
    [HttpPost("Build")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<SearchStatusDto>> Build()
    {
        _searchService.StartBuildingEmbeddings();
        return await _searchService.GetStatusAsync().ConfigureAwait(false);
    }
}
