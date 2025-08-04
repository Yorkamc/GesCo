using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace GesCo.Api.Models
{
    public class User : IdentityUser
    {
        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        public string FullName => $"{FirstName} {LastName}";

        public bool IsActive { get; set; } = true;

        public bool EmailVerified { get; set; } = false;

        public int FailedLoginAttempts { get; set; } = 0;

        public DateTime? LockedUntil { get; set; }

        [MaxLength(36)]
        public string? OrganizationId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginAt { get; set; }

        // Propiedades de navegación
        public virtual Organization? Organization { get; set; }
        public virtual ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
        public virtual ICollection<LoginAttempt> LoginAttempts { get; set; } = new List<LoginAttempt>();

        // Método para verificar si la cuenta está bloqueada
        public bool IsLockedOut => LockedUntil.HasValue && LockedUntil.Value > DateTime.UtcNow;
    }
}