using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using GesCo.Api.Models.DTOs;
using GesCo.Api.Services;

namespace GesCo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Iniciar sesión con email y contraseña
    /// </summary>
    /// <param name="request">Datos de login</param>
    /// <returns>Token de acceso y información del usuario</returns>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponseDto<LoginResponseDto>>> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .SelectMany(x => x.Value!.Errors)
                .Select(x => x.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponseDto<LoginResponseDto>.ErrorResponse("Datos de entrada inválidos", errors));
        }

        var ipAddress = GetClientIpAddress();
        var userAgent = GetUserAgent();

        var result = await _authService.LoginAsync(request, ipAddress, userAgent);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Renovar token de acceso usando refresh token
    /// </summary>
    /// <param name="request">Refresh token</param>
    /// <returns>Nuevo token de acceso</returns>
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponseDto<LoginResponseDto>>> RefreshToken([FromBody] RefreshTokenRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponseDto<LoginResponseDto>.ErrorResponse("Refresh token requerido"));
        }

        var ipAddress = GetClientIpAddress();
        var userAgent = GetUserAgent();

        var result = await _authService.RefreshTokenAsync(request.RefreshToken, ipAddress, userAgent);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Cerrar sesión actual
    /// </summary>
    /// <returns>Confirmación de logout</returns>
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponseDto<bool>>> Logout()
    {
        var sessionToken = GetSessionTokenFromHeader();
        
        if (string.IsNullOrEmpty(sessionToken))
        {
            return BadRequest(ApiResponseDto<bool>.ErrorResponse("Token de sesión no encontrado"));
        }

        var result = await _authService.LogoutAsync(sessionToken);
        return Ok(result);
    }

    /// <summary>
    /// Registrar nuevo usuario
    /// </summary>
    /// <param name="request">Datos del nuevo usuario</param>
    /// <returns>Información del usuario creado</returns>
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponseDto<UserDto>>> Register([FromBody] RegisterRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .SelectMany(x => x.Value!.Errors)
                .Select(x => x.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponseDto<UserDto>.ErrorResponse("Datos de entrada inválidos", errors));
        }

        var result = await _authService.RegisterAsync(request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Obtener información del usuario actual
    /// </summary>
    /// <returns>Información del usuario autenticado</returns>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<ApiResponseDto<object>> GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var name = User.FindFirst(ClaimTypes.Name)?.Value;
        var organizationId = User.FindFirst("organization_id")?.Value;

        var userInfo = new
        {
            Id = userId,
            Email = email,
            Name = name,
            OrganizationId = organizationId,
            Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
        };

        return Ok(ApiResponseDto<object>.SuccessResponse(userInfo, "Información del usuario obtenida exitosamente"));
    }

    /// <summary>
    /// Verificar el estado del servidor
    /// </summary>
    /// <returns>Estado del servidor</returns>
    [HttpGet("health")]
    public ActionResult<ApiResponseDto<object>> Health()
    {
        var health = new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
        };

        return Ok(ApiResponseDto<object>.SuccessResponse(health, "Servidor funcionando correctamente"));
    }

    #region Métodos Helper

    private string GetClientIpAddress()
    {
        // Verificar headers comunes de proxies/load balancers
        var ipAddress = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(ipAddress))
            ipAddress = HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(ipAddress))
            ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        // Si viene de proxy, tomar la primera IP (cliente original)
        if (!string.IsNullOrEmpty(ipAddress) && ipAddress.Contains(','))
        {
            ipAddress = ipAddress.Split(',')[0].Trim();
        }

        return ipAddress ?? "Unknown";
    }

    private string GetUserAgent()
    {
        return HttpContext.Request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown";
    }

    private string? GetSessionTokenFromHeader()
    {
        var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return null;
        }

        return authHeader.Substring("Bearer ".Length).Trim();
    }

    #endregion
}