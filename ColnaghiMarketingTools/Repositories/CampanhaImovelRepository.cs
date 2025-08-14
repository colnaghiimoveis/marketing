using ColnaghiMarketingTools.Models;
using Dapper;
using MySql.Data.MySqlClient; // Troca aqui

namespace ColnaghiMarketingTools.Repositories
{
    public class CampanhaImovelRepository
    {
        private readonly string _connectionString;

        public CampanhaImovelRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void RemoverPorCampanha(long campanhaId)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Execute("DELETE FROM CampanhaImovel WHERE CampanhaId = @CampanhaId", new { CampanhaId = campanhaId });
            }
        }

        public void InserirImoveis(long campanhaId, List<CampanhaImovel> imoveis)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                foreach (var imovel in imoveis)
                {
                    conn.Execute(@"INSERT INTO CampanhaImovel 
                        (CampanhaId, CodigoImovel, DataAdicionado, Ativo, ValorVenda, Dormitorios, Vagas, AreaPrivativa, ValorVendaEspecial, Promocao, Tipo, Bairro, FotoDestaquePequena, UrlLink)
                        VALUES (@CampanhaId, @CodigoImovel, NOW(), 1, @ValorVenda, @Dormitorios, @Vagas, @AreaPrivativa, @ValorVendaEspecial, @Promocao, @Tipo, @Bairro, @FotoDestaquePequena, @UrlLink)",
                        new
                        {
                            CampanhaId = campanhaId,
                            CodigoImovel = imovel.CodigoImovel,
                            ValorVenda = imovel.ValorVenda,
                            Dormitorios = imovel.Dormitorios,
                            Vagas = imovel.Vagas,
                            AreaPrivativa = imovel.AreaPrivativa,
                            ValorVendaEspecial = imovel.ValorVendaEspecial,
                            Promocao = imovel.Promocao,
                            Tipo = imovel.Tipo,
                            Bairro = imovel.Bairro,
                            FotoDestaquePequena = imovel.FotoDestaquePequena,
                            UrlLink = imovel.UrlLink
                        });
                }
            }
        }
    }
}
