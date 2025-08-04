using System.ComponentModel.DataAnnotations;

namespace GesCo.Api.Models
{
    public class UserSession
    {
        [Key]
        [MaxLength(36)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string SessionToken { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string RefreshToken { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string TokenHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; }

        public DateTime? RevokedAt { get; set; }

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        [MaxLength(100)]
        public string? DeviceName { get; set; }

        // Propiedad calculada para verificar si la sesión está activa
        public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;

        // Propiedades de navegación
        public virtual User User { get; set; } = null!;
    }
}