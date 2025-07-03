using System.ComponentModel.DataAnnotations;

namespace ProvaProgettoSERVER.Models
{
    public class Reparto
    {
        [Key]
        public int ID { get; set; }
        [Required]
        public string Nome { get; set; }
        [Required]
        public int NumeroLetti { get; set; }
    }
}
