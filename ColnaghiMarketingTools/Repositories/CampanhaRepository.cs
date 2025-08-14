using ColnaghiMarketingTools.Models;
using Dapper;
using MySql.Data.MySqlClient;

namespace ColnaghiMarketingTools.Repositories
{
    public class CampanhaRepository
    {
        private readonly string _connectionString;

        public CampanhaRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public long SalvarRascunho(Campanha campanha)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                var sql = @"
                    INSERT INTO Campanhas (Nome, PastaId, TipoTemplate, Titulo, Chamada, CorpoTexto, Rodape, TipoExibicao, DataCriacao, Ativo, Rascunho)
                    VALUES (@Nome, @PastaId, @TipoTemplate, @Titulo, @Chamada, @CorpoTexto, @Rodape, @TipoExibicao, @DataCriacao, @Ativo, @Rascunho);
                    SELECT LAST_INSERT_ID();";

                campanha.DataCriacao = DateTime.Now;
                campanha.Ativo = true;
                campanha.Rascunho = true;

                return conn.QuerySingle<long>(sql, campanha);
            }
        }

        public void AtualizarCampanha(Campanha campanha)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                var sql = @"
                    UPDATE Campanhas 
                    SET Titulo = @Titulo, Chamada = @Chamada, CorpoTexto = @CorpoTexto, Rodape = @Rodape, TipoExibicao = @TipoExibicao, Rascunho = 0
                    WHERE Id = @Id";

                conn.Execute(sql, campanha);
            }
        }

        public void Deletar(long id)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Execute("UPDATE Campanhas SET Ativo = 0 WHERE Id = @Id", new { Id = id });
            }
        }

        public IEnumerable<Campanha> ListarRascunhos()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                var sql = @"
                    SELECT c.*, p.Nome as PastaNome 
                    FROM Campanhas c 
                    INNER JOIN Pastas p ON c.PastaId = p.Id 
                    WHERE c.Rascunho = 1 AND c.Ativo = 1 
                    ORDER BY c.DataCriacao DESC";

                return conn.Query<Campanha, Pasta, Campanha>(sql, (campanha, pasta) =>
                {
                    campanha.Pasta = pasta;
                    return campanha;
                }, splitOn: "PastaNome");
            }
        }

        public IEnumerable<Campanha> ListarHistorico(int qtd)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                var sql = @"
                    SELECT c.*, p.* 
                    FROM Campanhas c
                    INNER JOIN Pastas p ON c.PastaId = p.Id
                    ORDER BY c.DataCriacao DESC
                    LIMIT @Qtd";

                return conn.Query<Campanha, Pasta, Campanha>(sql, (campanha, pasta) =>
                {
                    campanha.Pasta = pasta;
                    return campanha;
                }, new { Qtd = qtd }, splitOn: "Id");
            }
        }

        public IEnumerable<Campanha> ListarPorPasta(long pastaId)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                var sql = @"
                    SELECT c.*, p.Nome as PastaNome 
                    FROM Campanhas c 
                    INNER JOIN Pastas p ON c.PastaId = p.Id 
                    WHERE c.PastaId = @PastaId
                    AND c.Ativo = 1
                    AND p.Ativo = 1
                    ORDER BY c.DataCriacao DESC";

                return conn.Query<Campanha, Pasta, Campanha>(sql, (campanha, pasta) =>
                {
                    campanha.Pasta = pasta;
                    return campanha;
                }, new { PastaId = pastaId }, splitOn: "PastaNome");
            }
        }

        public Campanha? ObterPorId(long id)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                var sql = @"
                    SELECT c.*, p.Nome as PastaNome 
                    FROM Campanhas c 
                    INNER JOIN Pastas p ON c.PastaId = p.Id 
                    WHERE c.Id = @Id AND c.Ativo = 1";

                var result = conn.Query<Campanha, Pasta, Campanha>(sql, (campanha, pasta) =>
                {
                    campanha.Pasta = pasta;
                    return campanha;
                }, new { Id = id }, splitOn: "PastaNome");

                var campanhaObj = result.FirstOrDefault();
                if (campanhaObj != null)
                {
                    campanhaObj.Imoveis = conn.Query<CampanhaImovel>(
                        "SELECT * FROM CampanhaImovel WHERE CampanhaId = @CampanhaId AND Ativo = 1",
                        new { CampanhaId = id }).ToList();
                }

                return campanhaObj;
            }
        }
    }
}
