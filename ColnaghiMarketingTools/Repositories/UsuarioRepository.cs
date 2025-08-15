using ColnaghiMarketingTools.Models;
using Dapper;
using MySql.Data.MySqlClient; // Substituição do provider

namespace ColnaghiMarketingTools.Repositories
{
    public class UsuarioRepository
    {
        private readonly string _connectionString;

        public UsuarioRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public Usuario? ObterPorEmailESenha(string email, string senha)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                var sql = "SELECT * FROM Usuarios WHERE Email = @Email AND Senha = @Senha AND Ativo = 1";
                return conn.QueryFirstOrDefault<Usuario>(sql, new { Email = email, Senha = senha });
            }
        }
    }
}
