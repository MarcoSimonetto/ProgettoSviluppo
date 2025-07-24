using System.ComponentModel.DataAnnotations;
namespace ProvaMVC.Models
{
    public class Somministrazione
    {
        [Key]
        public int ID { get; set; }
        [Required]
        public int MatricolaUtente { get; set; }
        [Required]
        public int IDTerapia { get; set; }
        [Required]
        public DateOnly Data { get; set; }
    }
}
