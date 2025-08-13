using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;



namespace MBTP.Interfaces
{
    public interface IDatabaseConnectionService { 
        bool IsConnectionOpen(); } 
        public class DatabaseConnectionService : IDatabaseConnectionService { private readonly IConfiguration _configuration; public DatabaseConnectionService(IConfiguration configuration) { 
            _configuration = configuration; 
            } 
            public bool IsConnectionOpen() { 
                using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"))) 
                { try { 
                    connection.Open(); 
                    return connection.State == ConnectionState.Open; 
                    } catch { 
                        return false; 
                    } 
                } 
            } 
    }

}
