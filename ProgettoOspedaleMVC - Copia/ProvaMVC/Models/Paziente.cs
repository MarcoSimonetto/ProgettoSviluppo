using System.ComponentModel.DataAnnotations;

namespace ProvaMVC.Models
{
    public class Paziente
    {
        [Key]
        public int ID { get; set; }
        [Required]
        [MinLength(16), MaxLength(16, ErrorMessage = "Il Codice Fiscale deve contenere esattamente 16 caratteri.")]
        public string CF { get; set; }
        [Required]
        public string Nome { get; set; }
        [Required]
        public string Cognome { get; set; }
        [Required]
        public DateOnly DataNascita { get; set; }
        [Required]
        public string LuogoNascita { get; set; }
        [Required]
        public DateOnly DataRicovero { get; set; }
        [Required]
        public string MotivoRicovero { get; set; }
        public DateOnly? DataDimissione { get; set; }
        [Required]
        public int IDReparto { get; set; }
        public int? NumeroLetto { get; set; }
        public string? Patologie { get; set; }
        public string? Allergie { get; set; }
        public string? AltreNote { get; set; }
    }
}
