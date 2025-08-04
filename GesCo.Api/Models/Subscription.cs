using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GesCo.Api.Models
{
    public enum SubscriptionPlan
    {
        Trial,      // Prueba gratuita
        Basic,      // Plan básico
        Premium,    // Plan premium
        Enterprise  // Plan empresarial
    }

    public enum SubscriptionStatus
    {
        Active,     // Activa
        Expired,    // Expirada
        Cancelled,  // Cancelada
        Suspended   // Suspendida
    }
    public class Subscription
    {
        [Key]
        [MaxLength(36)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(36)]
        public string OrganizationId { get; set; } = string.Empty;

        [Required]
        public SubscriptionPlan Plan { get; set; }

        [Required]
        public SubscriptionStatus Status { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public int MaxUsers { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal MonthlyPrice { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CancelledAt { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        // Propiedad calculada para verificar si está vencida
        public bool IsExpired => DateTime.UtcNow > EndDate;

        // Propiedad calculada para días restantes
        public int DaysRemaining => Math.Max(0, (EndDate - DateTime.UtcNow).Days);

        // Propiedades de navegación
        public virtual Organization Organization { get; set; } = null!;

    }
}