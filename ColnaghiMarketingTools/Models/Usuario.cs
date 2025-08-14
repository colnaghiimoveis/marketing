using System;

namespace ColnaghiMarketingTools.Models
{
    public class Usuario
    {
        public long Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public bool Ativo { get; set; }
    }
} 