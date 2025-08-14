using ColnaghiMarketingTools.Models;
using Dapper;
using MySql.Data.MySqlClient;

namespace ColnaghiMarketingTools.Repositories
{
    public class FolderRepository
    {
        private readonly string _connectionString;

        public FolderRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IEnumerable<Pasta> Listar()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                return conn.Query<Pasta>("SELECT * FROM Pastas WHERE Ativo = 1 ORDER BY Nome");
            }
        }

        public void Adicionar(string nome)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Execute("INSERT INTO Pastas (Nome, DataCriacao, Ativo) VALUES (@Nome, NOW(), 1)", new { Nome = nome });
            }
        }

        public void Excluir(long id)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Execute("UPDATE Pastas SET Ativo = 0 WHERE Id = @Id", new { Id = id });
            }
        }
    }
}
