using System.ComponentModel.DataAnnotations;

namespace GesCo.Api.Models
{
    public enum LoginResult
    {
        Success,
        InvalidCredentials,
        AccountLocked,
        AccountInactive,
        EmailNotVerified,
        UnknownError
    }
    public class LoginAttempt
    {
     [Key]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(100)]
    public string AttemptedEmail { get; set; } = string.Empty;

    public string? UserId { get; set; }

    [Required]
    public LoginResult Result { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    // Propiedades de navegación
    public virtual User? User { get; set; }
    }
}