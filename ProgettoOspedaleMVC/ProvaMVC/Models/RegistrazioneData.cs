using System.ComponentModel.DataAnnotations;

namespace ProvaMVC.Models
{
    public class RegistrazioneData
    {
        [Required]
        [MinLength(16), MaxLength(16, ErrorMessage = "Il Codice Fiscale deve contenere esattamente 16 caratteri.")]
        public string CF { get; set; }

        [Required]
        [RegularExpression(@"^(?=.[A-Z])(?=.\d)(a-z).{8,15}$",
        ErrorMessage = "La password deve contenere almeno 8 caratteri, massimo 15, una lettera maiuscola e un numero.")]
        public string Password { get; set; }

        [Required]
        public string Ruolo { get; set; }

        [Required]
        public int IDReparto { get; set; }

        [Required]
        public string Nome { get; set; }

        [Required]
        public string Cognome { get; set; }

        public List<Reparto>? Reparti { get; set; }
    }
}