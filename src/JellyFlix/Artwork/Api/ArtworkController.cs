using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using JellyFlix.Artwork.Api.Models;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JellyFlix.Artwork.Api;

/// <summary>
/// Each profile's own choice of posters and backdrops.
/// </summary>
[ApiController]
[Authorize]
[Route("JellyFlix/Artwork")]
public class ArtworkController : ControllerBase
{
    private readonly ArtworkService _artworkService;
    private readonly IAuthorizationContext _authorizationContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtworkController"/> class.
    /// </summary>
    /// <param name="artworkService">The artwork service.</param>
    /// <param name="authorizationContext">The authorization context.</param>
    public ArtworkController(ArtworkService artworkService, IAuthorizationContext authorizationContext)
    {
        _artworkService = artworkService;
        _authorizationContext = authorizationContext;
    }

    /// <summary>
    /// Gets the images the signed-in profile sees instead of the library's.
    /// </summary>
    /// <returns>Paths under the server, by title id.</returns>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<Dictionary<string, TitleArtworkDto>>> GetOverrides()
    {
        var user = await GetUserAsync().ConfigureAwait(false);
        return user is null ? Unauthorized() : _artworkService.GetOverrides(user);
    }

    /// <summary>
    /// Lists the posters or backdrops the signed-in profile can choose from for a title.
    /// </summary>
    /// <param name="itemId">The title.</param>
    /// <param name="kind">Poster or backdrop.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The options.</returns>
    [HttpGet("{itemId}/Options")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ArtworkOptionsDto>> GetOptions([FromRoute, Required] Guid itemId, [FromQuery] ArtworkKind kind, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync().ConfigureAwait(false);
        if (user is null)
        {
            return Unauthorized();
        }

        try
        {
            return await _artworkService.GetOptionsAsync(user, itemId, kind, cancellationToken).ConfigureAwait(false);
        }
        catch (ArtworkException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

    /// <summary>
    /// Chooses a poster or backdrop for the signed-in profile only.
    /// </summary>
    /// <param name="itemId">The title.</param>
    /// <param name="choice">What to choose.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Nothing.</returns>
    [HttpPost("{itemId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Choose([FromRoute, Required] Guid itemId, [FromBody, Required] ChooseArtworkDto choice, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync().ConfigureAwait(false);
        if (user is null)
        {
            return Unauthorized();
        }

        try
        {
            await _artworkService.ChooseAsync(user, itemId, choice.Kind, choice.OptionId, cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (ArtworkException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (HttpRequestException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    /// <summary>
    /// Goes back to the library's poster or backdrop for the signed-in profile.
    /// </summary>
    /// <param name="itemId">The title.</param>
    /// <param name="kind">Poster or backdrop.</param>
    /// <returns>Nothing.</returns>
    [HttpDelete("{itemId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Reset([FromRoute, Required] Guid itemId, [FromQuery] ArtworkKind kind)
    {
        var user = await GetUserAsync().ConfigureAwait(false);
        if (user is null)
        {
            return Unauthorized();
        }

        _artworkService.Reset(user, itemId, kind);
        return NoContent();
    }

    /// <summary>
    /// Serves a downloaded image. Images load in <c>img</c> tags, which can't send credentials; the names can't be
    /// guessed and the images are the metadata providers' public artwork.
    /// </summary>
    /// <param name="name">The file name.</param>
    /// <returns>The image.</returns>
    [HttpGet("Images/{name}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetImage([FromRoute, Required] string name)
    {
        var path = _artworkService.GetImagePath(name);
        if (path is null)
        {
            return NotFound();
        }

        // The name is the image's hash, so it never changes
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return PhysicalFile(path, ArtworkFiles.MediaType(name));
    }

    private async Task<User?> GetUserAsync()
        => (await _authorizationContext.GetAuthorizationInfo(HttpContext).ConfigureAwait(false)).User;
}
