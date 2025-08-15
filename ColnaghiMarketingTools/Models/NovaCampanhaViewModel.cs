using System.ComponentModel.DataAnnotations;

namespace ColnaghiMarketingTools.Models
{
    public class NovaCampanhaViewModel
    {
        [Required(ErrorMessage = "O nome da campanha é obrigatório")]
        [Display(Name = "Nome da Campanha")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "A pasta é obrigatória")]
        [Display(Name = "Pasta")]
        public long PastaId { get; set; }

        [Required(ErrorMessage = "O tipo de template é obrigatório")]
        [Display(Name = "Tipo de Template")]
        public int TipoTemplate { get; set; }
    }
} 