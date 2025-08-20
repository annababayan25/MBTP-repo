using System;
using System.Data;
using Microsoft.Data.SqlClient;
using MBTP.Interfaces;
using GenericSupport;

namespace SQLStuff
{
    public class SQLSupport
    {
        private readonly IDatabaseConnectionService _dbConnectionService;
        private SqlCommand _cmd;
        private SqlConnection _sqlConn;

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IDatabaseConnectionService dbConnectionService)
        {
            GenericRoutines.Initialize(dbConnectionService);
        }


        public SQLSupport(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

        public bool PrepareForImport(string procName)
        {
            try
            {
                _sqlConn = _dbConnectionService.CreateConnection();
                _cmd = new SqlCommand(procName, _sqlConn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                _cmd.Parameters.Add("@TransDate", SqlDbType.Date).Value = GenericRoutines.repDateStr;
                _cmd.Parameters.Add("@status", SqlDbType.NVarChar, 4000).Direction = ParameterDirection.Output;

                return true;
            }
            catch (Exception e)
            {
                GenericRoutines.UpdateAlerts(0, "FATAL ERROR",
                    $"Problem encountered preparing {procName}: {e}");
                return false;
            }
        }

        public void AddSQLParameter(string paramName, SqlDbType sqlType, double val, bool updateExisting = false)
        {
            if (!updateExisting)
            {
                _cmd.Parameters.Add(paramName, sqlType).Value = val;
            }
            else
            {
                double currentVal = Convert.ToDouble(_cmd.Parameters[paramName].Value);
                _cmd.Parameters[paramName].Value = currentVal + val;
            }
        }

        public void AddSQLParameterString(string paramName, SqlDbType sqlType, string val)
        {
            _cmd.Parameters.Add(paramName, sqlType).Value = val;
        }

        public string ExecuteStoredProcedure(byte pcIDIn)
        {
            string returnVal = "Failure";

            _sqlConn.Open();
            _cmd.ExecuteNonQuery();

            if (_cmd.Parameters["@status"].Value?.ToString() == "SUCCESS")
            {
                GenericRoutines.UpdateAlerts(pcIDIn, "SUCCESS", "");
                returnVal = "SUCCESS";
            }
            else
            {
                GenericRoutines.UpdateAlerts(pcIDIn, "FATAL ERROR",
                    $"{_cmd.CommandText} failed: {_cmd.Parameters["@status"].Value}");
            }

            _sqlConn.Close();
            return returnVal;
        }

        public void RemoveParameters()
        {
            // Keep TransDate and @status (usually first 2 parameters)
            while (_cmd.Parameters.Count > 2)
            {
                _cmd.Parameters.RemoveAt(2);
            }
        }

        public static void UpdateAlertsTable(IDatabaseConnectionService dbConnectionService, byte pcidIn, string severityIn, string textIn)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            using (var conn = dbConnectionService.CreateConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("dbo.UpdateAlerts", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@TransDate", SqlDbType.Date).Value = GenericRoutines.repDateStr;
                    cmd.Parameters.Add("@PCID", SqlDbType.TinyInt).Value = pcidIn;
                    cmd.Parameters.Add("@Severity", SqlDbType.VarChar, 50).Value = severityIn;
                    cmd.Parameters.Add("@AlertText", SqlDbType.VarChar, 4000).Value = textIn;
                    cmd.Parameters.Add("@status", SqlDbType.NVarChar, 4000).Direction = ParameterDirection.Output;
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}