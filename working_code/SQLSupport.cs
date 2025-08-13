using GenericSupport;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Web;
using System.Configuration;

namespace SQLStuff
{
    public class SQLSupport
    {
            static readonly SqlConnection sqlConn = new(new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("ConnectionStrings")["DefaultConnection"]);
#nullable enable
        static SqlCommand? cmd;
#nullable disable
        public static bool PrepareForImport(string procName)
        {
            // This method defines a connection to the SQL database and then defines the two parameters that are used by all update stored procedures
            try
            {
                cmd = new SqlCommand(procName, sqlConn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure
                };
                // add input parameter for transaction date
                cmd.Parameters.Add("@TransDate", SqlDbType.Date);
                cmd.Parameters["@TransDate"].Value = GenericRoutines.repDateStr;
                // add an output parameter to check if stored procedures executed cleanly
                cmd.Parameters.Add("@status", SqlDbType.NVarChar, 4000);
                cmd.Parameters["@status"].Direction = ParameterDirection.Output;
                return true;
            }
            catch (Exception e)
            {
                GenericRoutines.UpdateAlerts(0, "FATAL ERROR", "Problem encountered preparing " + procName + ":" + e.ToString());
                return false;
            }
        }
        public static void AddSQLParameter(string paramName, SqlDbType sqltype, double val, bool updateExisting = false)
        {
            if (updateExisting == false)
            {
                cmd.Parameters.Add(paramName, sqltype);
                cmd.Parameters[cmd.Parameters.Count - 1].Value = val;
            }
            else
            {
                double tempVal = (double)cmd.Parameters[paramName].Value + val;
                cmd.Parameters[paramName].Value = tempVal;
            }
        }
        public static void AddSQLParameterString(string paramName, SqlDbType sqltype, string val)
        {
            cmd.Parameters.Add(paramName, sqltype);
            cmd.Parameters[cmd.Parameters.Count - 1].Value = val;
        }
        public static string ExecuteStoredProcedure(byte pcIDIn)
        // Open the SQL connection, execute the procedure, act on result, close and delete the SQL variables
        {
            string returnVal = "Failure";
            sqlConn.Open();
            //for (int ip = 0; ip < cmd.Parameters.Count; ip++)
            //{
            //    System.Diagnostics.Debug.WriteLine(cmd.Parameters[ip].ParameterName + " " + cmd.Parameters[ip].TypeName.GetType().ToString() + " " + cmd.Parameters[ip].Value);
            //}
            cmd.ExecuteNonQuery();
            if (cmd.Parameters["@status"].Value.ToString() == "SUCCESS") // Successful execution, clear previous alerts for this date for the store
            {
                GenericRoutines.UpdateAlerts(pcIDIn, "SUCCESS", "");
                returnVal = "SUCCESS";
            }
            else // record error in alerts table
            {
                GenericRoutines.UpdateAlerts(pcIDIn, "FATAL ERROR", cmd.CommandText + " failed: " + cmd.Parameters["@status"].Value.ToString());
            }
            if (sqlConn.State == ConnectionState.Open) // should never get here without the connection being open, but...
            {
                sqlConn.Close();
            }
            return returnVal;
        }
        public static void RemoveParameters() 
        { 
            for (int i = cmd.Parameters.Count - 1; i >= 2; i--)  // Remove from the end backwards, but do not delete @TransDate or @Status
            {
                cmd.Parameters.RemoveAt(i);
            }
        }
    }
}