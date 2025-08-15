namespace ColnaghiMarketingTools.Models
{
    public class CampanhaImovel
    {
        public long Id { get; set; }
        public long CampanhaId { get; set; }
        public string CodigoImovel { get; set; } = string.Empty;
        public DateTime DataAdicionado { get; set; }
        public bool Ativo { get; set; }
        public decimal? ValorVenda { get; set; }
        public int? Dormitorios { get; set; }
        public int? Vagas { get; set; }
        public decimal? AreaPrivativa { get; set; }
        public decimal? ValorVendaEspecial { get; set; }
        public string? Promocao { get; set; }
        public string? Tipo { get; set; }
        public string? Bairro { get; set; }
        public string? FotoDestaquePequena { get; set; }
        public string? UrlLink { get; set; }
    }
} 