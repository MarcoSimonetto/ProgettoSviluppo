using System.ComponentModel.DataAnnotations;

namespace ProvaMVC.Models
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
