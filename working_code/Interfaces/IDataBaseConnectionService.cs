using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace MBTP.Interfaces
{
    public interface IDatabaseConnectionService
    {
        SqlConnection CreateConnection();
    }

    public class DatabaseConnectionService : IDatabaseConnectionService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DatabaseConnectionService(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
        }

        public SqlConnection CreateConnection()
        {
            // var env = environmentOveride ?? _httpContextAccessor.HttpContext?.Session?.GetString("EnvironmentMode") ?? "Live";
            var env = _httpContextAccessor.HttpContext?.Session?.GetString("EnvironmentMode") ?? "Live";
            var key = env == "Test" ? "TestConnection" : "DefaultConnection";
            var connString = _configuration.GetConnectionString(key);

            var sessionDbName = _httpContextAccessor.HttpContext?.Session?.GetString("sqlConnString");

            if (!string.IsNullOrEmpty(sessionDbName))
            {
                var builder = new SqlConnectionStringBuilder(connString)
                {
                    InitialCatalog = sessionDbName
                };
                connString = builder.ConnectionString;
            }
            return new SqlConnection(connString);
        }
        public string GetActiveConnectionStringName()
        {
            var mode = _httpContextAccessor.HttpContext?.Session.GetString("EnvironmentMode") ?? "Live";
            return mode == "Test" ? "TestConnection" : "DefaultConnection";
        }
    } 
}
