using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SadcOMS.API.Auth;
using SadcOMS.API.DTOs;

namespace SadcOMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly DevJwtTokenService _tokenService;
    private readonly AuthOptions _authOptions;
    private readonly IWebHostEnvironment _environment;

    public AuthController(
        DevJwtTokenService tokenService,
        IOptions<AuthOptions> authOptions,
        IWebHostEnvironment environment)
    {
        _tokenService = tokenService;
        _authOptions = authOptions.Value;
        _environment = environment;
    }

    /// <summary>
    /// Mints a dev JWT signed with Auth:SigningKey. Available in Development or when Auth:DevBypass is on.
    /// </summary>
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult MintToken()
    {
        if (!_environment.IsDevelopment() && !_authOptions.DevBypass)
            return NotFound();

        var (accessToken, expiresIn) = _tokenService.CreateAccessToken();
        return Ok(new TokenResponse(accessToken, expiresIn));
    }
}
