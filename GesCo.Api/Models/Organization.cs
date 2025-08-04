using System.ComponentModel.DataAnnotations;

namespace GesCo.Api.Models
{
    public enum OrganizationType
    {
        ADI,            // Asociación de Desarrollo Integral
        Municipality,   // Municipalidad
        Cooperative,    // Cooperativa
        Company,        // Empresa
        NGO,           // ONG
        Other          // Otros
    }
    public class Organization
    {
        [Key]
        [MaxLength(36)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Code { get; set; }

        [Required]
        public OrganizationType Type { get; set; }

        [MaxLength(100)]
        [EmailAddress]
        public string? ContactEmail { get; set; }

        [MaxLength(20)]
        public string? ContactPhone { get; set; }

        [MaxLength(200)]
        public string? Address { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(36)]
        public string? SubscriptionId { get; set; }

        // Propiedades de navegación
        public virtual ICollection<User> Users { get; set; } = new List<User>();
        public virtual Subscription? Subscription { get; set; }
    }
}