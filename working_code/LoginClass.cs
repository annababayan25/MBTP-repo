using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using MBTP.Interfaces;

namespace MBTP.Logins
{
    public class LoginClass
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public LoginClass(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

        public static string EncryptPassword(string passwordTxt)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(passwordTxt);
                byte[] hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        public bool ValidateLogin(string username, string passwordTxt, out string LID, out string accID)
        {
            LID = "0";
            accID = string.Empty;

            using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
            {
                sqlConn.Open();

                SqlCommand fetchCmd = new SqlCommand("SELECT Password FROM LoginsHope WHERE Username = @Username", sqlConn);
                fetchCmd.Parameters.Add("@Username", SqlDbType.NVarChar, 15).Value = username.Trim();
                string storedEncryptedPassword = fetchCmd.ExecuteScalar()?.ToString();

                string encryptedPassword = EncryptPassword(passwordTxt.Trim());

                if (storedEncryptedPassword != null && storedEncryptedPassword == encryptedPassword)
                {
                    SqlCommand cmd = new SqlCommand("dbo.ValidateLogin", sqlConn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };

                    cmd.Parameters.Add("@username", SqlDbType.NVarChar, 15).Value = username.Trim();
                    cmd.Parameters.Add("@pwd", SqlDbType.NVarChar, 50).Value = encryptedPassword;
                    SqlParameter lidParam = cmd.Parameters.Add("@LID", SqlDbType.Int);
                    lidParam.Direction = ParameterDirection.Output;
                    SqlParameter accIdParam = cmd.Parameters.Add("@accID", SqlDbType.SmallInt);
                    accIdParam.Direction = ParameterDirection.Output;
                    SqlParameter returnParam = cmd.Parameters.Add("@ReturnVal", SqlDbType.Bit);
                    returnParam.Direction = ParameterDirection.ReturnValue;

                    try
                    {
                        cmd.ExecuteNonQuery();
                        bool result = Convert.ToBoolean(returnParam.Value);

                        if (result)
                        {
                            LID = lidParam.Value.ToString();
                            accID = accIdParam.Value.ToString();
                            return true;
                        }

                        return false;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Login error: {e.Message}");
                        return false;
                    }
                }

                return false;
            }
        }
    }
}



       // Method to encrypt existing passwords
       /*public static void EncryptExistingPasswordsDave(string connectionString)
       {
           using (SqlConnection sqlConn = new SqlConnection(connectionString))
           {
               sqlConn.Open();


               // Fetch all usernames and passwords
               SqlCommand fetchCmd = new SqlCommand("SELECT LID, Password FROM LoginsHope", sqlConn);
               SqlDataReader reader = fetchCmd.ExecuteReader();


               DataTable dt = new DataTable();
               dt.Load(reader);


               // Encrypt each password and update the database
               foreach (DataRow row in dt.Rows)
               {
                   int LID = (int)row["LID"];
                   string plainPassword = row["Password"].ToString();
                   string encryptedPassword = EncryptPassword(plainPassword.Trim());


                   SqlCommand updateCmd = new SqlCommand("UPDATE LoginsHope SET Password = @Password WHERE LID = @LID", sqlConn);
                   updateCmd.Parameters.Add("@Password", SqlDbType.NVarChar).Value = encryptedPassword;
                   updateCmd.Parameters.Add("@LID", SqlDbType.Int).Value = LID;


                   updateCmd.ExecuteNonQuery();
               }


               sqlConn.Close();
               Console.WriteLine("All passwords have been encrypted and updated in the database.");
           }
       }*/
   