using System.ComponentModel.DataAnnotations;

namespace ProvaMVC.Models
{
    public class Terapia
    {
        [Key]
        public int ID { get; set; }
        [Required]
        public int IDPaziente { get; set; }
        [Required]
        public string Farmaco { get; set; }
        [Required]
        public string Dosaggio { get; set; }
        [Required]
        public TimeOnly OrarioSomministrazione { get; set; }
        [Required]
        public DateOnly DataInizio { get; set; }
        [Required]
        public DateOnly DataFine { get; set; }
        [Required]
        public int MatricolaMedico { get; set; }
    }
}