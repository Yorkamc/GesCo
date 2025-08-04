using System.ComponentModel.DataAnnotations;

namespace GesCo.Api.Models.DTOs;

// DTO para la solicitud de login
public class LoginRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    public string? DeviceName { get; set; }
}

// DTO para la respuesta de login exitoso
public class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
    public OrganizationDto? Organization { get; set; }
}

// DTO para información del usuario
public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool EmailVerified { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? OrganizationId { get; set; }
}

// DTO para información de la organización
public class OrganizationDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public SubscriptionDto? Subscription { get; set; }
}

// DTO para información de suscripción
public class SubscriptionDto
{
    public string Id { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime EndDate { get; set; }
    public int MaxUsers { get; set; }
    public int DaysRemaining { get; set; }
    public bool IsExpired { get; set; }
}

// DTO para solicitud de refresh token
public class RefreshTokenRequestDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

// DTO para registro de usuarios
public class RegisterRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    public string? OrganizationId { get; set; }
}

// DTO para respuestas de API
public class ApiResponseDto<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }

    public static ApiResponseDto<T> SuccessResponse(T data, string message = "Operación exitosa")
    {
        return new ApiResponseDto<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    public static ApiResponseDto<T> ErrorResponse(string message, List<string>? errors = null)
    {
        return new ApiResponseDto<T>
        {
            Success = false,
            Message = message,
            Errors = errors ?? new List<string>()
        };
    }
}