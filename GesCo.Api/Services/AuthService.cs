using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using GesCo.Api.Data;
using GesCo.Api.Models;
using GesCo.Api.Models.DTOs;

namespace GesCo.Api.Services;

public interface IAuthService
{
    Task<ApiResponseDto<LoginResponseDto>> LoginAsync(LoginRequestDto request, string ipAddress, string userAgent);
    Task<ApiResponseDto<LoginResponseDto>> RefreshTokenAsync(string refreshToken, string ipAddress, string userAgent);
    Task<ApiResponseDto<bool>> LogoutAsync(string sessionToken);
    Task<ApiResponseDto<UserDto>> RegisterAsync(RegisterRequestDto request);
}

public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<User> userManager,
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ApiResponseDto<LoginResponseDto>> LoginAsync(LoginRequestDto request, string ipAddress, string userAgent)
    {
        try
        {
            // Registrar intento de login
            var loginAttempt = new LoginAttempt
            {
                AttemptedEmail = request.Email,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Result = LoginResult.InvalidCredentials
            };

            // Buscar usuario por email
            var user = await _userManager.FindByEmailAsync(request.Email);
            
            if (user == null)
            {
                await SaveLoginAttemptAsync(loginAttempt);
                return ApiResponseDto<LoginResponseDto>.ErrorResponse("Credenciales inválidas");
            }

            loginAttempt.UserId = user.Id;

            // Verificar si la cuenta está bloqueada
            if (user.IsLockedOut)
            {
                loginAttempt.Result = LoginResult.AccountLocked;
                loginAttempt.ErrorMessage = $"Cuenta bloqueada hasta {user.LockedUntil}";
                await SaveLoginAttemptAsync(loginAttempt);
                return ApiResponseDto<LoginResponseDto>.ErrorResponse("Cuenta temporalmente bloqueada debido a múltiples intentos fallidos");
            }

            // Verificar si la cuenta está activa
            if (!user.IsActive)
            {
                loginAttempt.Result = LoginResult.AccountInactive;
                loginAttempt.ErrorMessage = "Cuenta inactiva";
                await SaveLoginAttemptAsync(loginAttempt);
                return ApiResponseDto<LoginResponseDto>.ErrorResponse("Cuenta inactiva");
            }

            // Verificar contraseña
            var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
            
            if (!passwordValid)
            {
                // Incrementar intentos fallidos
                user.FailedLoginAttempts++;
                
                var maxAttempts = _configuration.GetValue<int>("Security:MaxFailedAttempts", 5);
                var lockoutMinutes = _configuration.GetValue<int>("Security:LockoutMinutes", 15);

                if (user.FailedLoginAttempts >= maxAttempts)
                {
                    user.LockedUntil = DateTime.UtcNow.AddMinutes(lockoutMinutes);
                    loginAttempt.Result = LoginResult.AccountLocked;
                    loginAttempt.ErrorMessage = $"Cuenta bloqueada por {lockoutMinutes} minutos";
                    await _userManager.UpdateAsync(user);
                    await SaveLoginAttemptAsync(loginAttempt);
                    return ApiResponseDto<LoginResponseDto>.ErrorResponse($"Cuenta bloqueada por {lockoutMinutes} minutos debido a múltiples intentos fallidos");
                }

                await _userManager.UpdateAsync(user);
                await SaveLoginAttemptAsync(loginAttempt);
                return ApiResponseDto<LoginResponseDto>.ErrorResponse("Credenciales inválidas");
            }

            // Login exitoso - resetear intentos fallidos
            user.FailedLoginAttempts = 0;
            user.LockedUntil = null;
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            // Generar tokens
            var (accessToken, refreshToken, expiresAt) = await GenerateTokensAsync(user);

            // Crear sesión
            var session = new UserSession
            {
                UserId = user.Id,
                SessionToken = accessToken,
                RefreshToken = refreshToken,
                TokenHash = BCrypt.Net.BCrypt.HashPassword(accessToken, 12),
                ExpiresAt = expiresAt,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                DeviceName = request.DeviceName
            };

            _context.UserSessions.Add(session);

            // Registrar login exitoso
            loginAttempt.Result = LoginResult.Success;
            await SaveLoginAttemptAsync(loginAttempt);
            await _context.SaveChangesAsync();

            // Cargar datos adicionales
            await _context.Entry(user)
                .Reference(u => u.Organization)
                .LoadAsync();

            if (user.Organization != null)
            {
                await _context.Entry(user.Organization)
                    .Reference(o => o.Subscription)
                    .LoadAsync();
            }

            // Crear respuesta
            var response = new LoginResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt,
                User = MapToUserDto(user),
                Organization = user.Organization != null ? MapToOrganizationDto(user.Organization) : null
            };

            _logger.LogInformation("Usuario {Email} inició sesión exitosamente desde {IpAddress}", user.Email, ipAddress);

            return ApiResponseDto<LoginResponseDto>.SuccessResponse(response, "Login exitoso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante el login para {Email}", request.Email);
            return ApiResponseDto<LoginResponseDto>.ErrorResponse("Error interno del servidor");
        }
    }

    public async Task<ApiResponseDto<LoginResponseDto>> RefreshTokenAsync(string refreshToken, string ipAddress, string userAgent)
    {
        try
        {
            var session = await _context.UserSessions
                .Include(s => s.User)
                    .ThenInclude(u => u.Organization)
                        .ThenInclude(o => o!.Subscription)
                .FirstOrDefaultAsync(s => s.RefreshToken == refreshToken && s.IsActive);

            if (session == null)
            {
                return ApiResponseDto<LoginResponseDto>.ErrorResponse("Token de actualización inválido");
            }

            // Revocar sesión anterior
            session.RevokedAt = DateTime.UtcNow;

            // Generar nuevos tokens
            var (newAccessToken, newRefreshToken, expiresAt) = await GenerateTokensAsync(session.User);

            // Crear nueva sesión
            var newSession = new UserSession
            {
                UserId = session.UserId,
                SessionToken = newAccessToken,
                RefreshToken = newRefreshToken,
                TokenHash = BCrypt.Net.BCrypt.HashPassword(newAccessToken, 12),
                ExpiresAt = expiresAt,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                DeviceName = session.DeviceName
            };

            _context.UserSessions.Add(newSession);
            await _context.SaveChangesAsync();

            var response = new LoginResponseDto
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = expiresAt,
                User = MapToUserDto(session.User),
                Organization = session.User.Organization != null ? MapToOrganizationDto(session.User.Organization) : null
            };

            return ApiResponseDto<LoginResponseDto>.SuccessResponse(response, "Token actualizado exitosamente");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la actualización del token");
            return ApiResponseDto<LoginResponseDto>.ErrorResponse("Error interno del servidor");
        }
    }

    public async Task<ApiResponseDto<bool>> LogoutAsync(string sessionToken)
    {
        try
        {
            var session = await _context.UserSessions
                .FirstOrDefaultAsync(s => s.SessionToken == sessionToken && s.IsActive);

            if (session != null)
            {
                session.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Sesión cerrada para usuario {UserId}", session.UserId);
            }

            return ApiResponseDto<bool>.SuccessResponse(true, "Sesión cerrada exitosamente");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante el logout");
            return ApiResponseDto<bool>.ErrorResponse("Error interno del servidor");
        }
    }

    public async Task<ApiResponseDto<UserDto>> RegisterAsync(RegisterRequestDto request)
    {
        try
        {
            var user = new User
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                OrganizationId = request.OrganizationId,
                EmailConfirmed = false // Cambiar según necesidades
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description).ToList();
                return ApiResponseDto<UserDto>.ErrorResponse("Error al crear el usuario", errors);
            }

            _logger.LogInformation("Usuario {Email} registrado exitosamente", user.Email);

            return ApiResponseDto<UserDto>.SuccessResponse(MapToUserDto(user), "Usuario registrado exitosamente");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante el registro del usuario {Email}", request.Email);
            return ApiResponseDto<UserDto>.ErrorResponse("Error interno del servidor");
        }
    }

    #region Métodos Privados

    private async Task<(string accessToken, string refreshToken, DateTime expiresAt)> GenerateTokensAsync(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"]!;
        var key = Encoding.ASCII.GetBytes(secretKey);
        var expiryMinutes = jwtSettings.GetValue<int>("ExpiryMinutes", 60);
        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim("organization_id", user.OrganizationId ?? ""),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);

        // Generar refresh token
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        return (accessToken, refreshToken, expiresAt);
    }

    private async Task SaveLoginAttemptAsync(LoginAttempt attempt)
    {
        _context.LoginAttempts.Add(attempt);
        await _context.SaveChangesAsync();
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            IsActive = user.IsActive,
            EmailVerified = user.EmailConfirmed,
            LastLoginAt = user.LastLoginAt,
            OrganizationId = user.OrganizationId
        };
    }

    private static OrganizationDto MapToOrganizationDto(Organization organization)
    {
        return new OrganizationDto
        {
            Id = organization.Id,
            Name = organization.Name,
            Code = organization.Code,
            Type = organization.Type.ToString(),
            ContactEmail = organization.ContactEmail,
            Subscription = organization.Subscription != null ? new SubscriptionDto
            {
                Id = organization.Subscription.Id,
                Plan = organization.Subscription.Plan.ToString(),
                Status = organization.Subscription.Status.ToString(),
                EndDate = organization.Subscription.EndDate,
                MaxUsers = organization.Subscription.MaxUsers,
                DaysRemaining = organization.Subscription.DaysRemaining,
                IsExpired = organization.Subscription.IsExpired
            } : null
        };
    }

    #endregion
}