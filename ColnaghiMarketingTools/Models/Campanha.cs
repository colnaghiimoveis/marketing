namespace ColnaghiMarketingTools.Models
{
    public class Campanha
    {
        public long Id { get; set; }
        public string? Nome { get; set; }
        public long PastaId { get; set; }
        public int TipoTemplate { get; set; }
        public string? Titulo { get; set; }
        public string? Chamada { get; set; }
        public string? CorpoTexto { get; set; }
        public string? Rodape { get; set; }
        public DateTime DataCriacao { get; set; }
        public bool Ativo { get; set; }
        public bool Rascunho { get; set; }
        public int TipoExibicao { get; set; }
        
        // Propriedade de navegação
        public Pasta? Pasta { get; set; }
        public List<CampanhaImovel>? Imoveis { get; set; }
    }
} 