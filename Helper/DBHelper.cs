// REFERENCE COPY ONLY: the other project already has this file. Do not copy it; it exists so this project compiles on its own.
#nullable disable
using Dapper;
using System.Data;
using System.Data.SqlClient;

namespace InternalWebApp.Helper
{
    public  class DBHelper
    {
        private readonly string _connectionString;
        public DBHelper(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("NotificationDb");
        }

        public IDbConnection CreateConnection()
            => new SqlConnection(_connectionString);

        public async Task<int> ExecuteAsync(string sql, object param = null)
        {
            using var db = CreateConnection();
            return await db.ExecuteAsync(sql, param);
        }

        public async Task<T> QueryFirstOrDefaultAsync<T>(string sql, object param = null)
        {
            using var db = CreateConnection();
            return await db.QueryFirstOrDefaultAsync<T>(sql, param);
        }

        public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object param = null)
        {
            using var db = CreateConnection();
            return await db.QueryAsync<T>(sql, param, commandTimeout: 120);
        }
    }
}
