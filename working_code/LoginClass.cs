using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;


namespace MBTP.Logins
{
   public class LoginClass
   {
       private readonly IConfiguration _configuration;


       public LoginClass(IConfiguration configuration)
       {
           _configuration = configuration;
       }


       // Method to encrypt the password using SHA256 with UTF-8 encoding
       public static string EncryptPassword(string passwordTxt)
       {
           using (SHA256 sha256 = SHA256.Create())
           {
               //Console.WriteLine(passwordTxt);
               byte[] bytes = Encoding.UTF8.GetBytes(passwordTxt);
               byte[] hash = sha256.ComputeHash(bytes);
                //Console.WriteLine(Convert.ToBase64String(hash));
               return Convert.ToBase64String(hash);
           }
       }


       // Method to validate login credentials by connecting to the SQL database and executing the stored procedure
       public bool ValidateLogin(string username, string passwordTxt, out string LID, out string accID)
       {
           LID = "0";
           accID = string.Empty;


           // Retrieve the connection string from the configuration file
           string connectionString = _configuration.GetConnectionString("DefaultConnection");


           if (string.IsNullOrEmpty(connectionString))
           {
               throw new Exception("Database connection string is not configured.");
           }


           using (SqlConnection sqlConn = new SqlConnection(connectionString))
           {
               sqlConn.Open();


               // Fetch the stored encrypted password for the given username
               SqlCommand fetchCmd = new SqlCommand("SELECT Password FROM LoginsHope WHERE Username = @Username", sqlConn);
               fetchCmd.Parameters.Add("@Username", SqlDbType.NVarChar, 15).Value = username.Trim();
               string storedEncryptedPassword = fetchCmd.ExecuteScalar()?.ToString();
               //Console.WriteLine($"Stored Encrypted Password: {storedEncryptedPassword}");


               // Encrypt the input password
               string encryptedPassword = EncryptPassword(passwordTxt.Trim());
               //Console.WriteLine($"Input password cleartext: {passwordTxt}");
               //Console.WriteLine($"Input Encrypted Password: {encryptedPassword}");


               // Compare the passwords
               if (storedEncryptedPassword != null && storedEncryptedPassword == encryptedPassword)
               {
                   SqlCommand cmd = new SqlCommand("dbo.ValidateLogin", sqlConn)
                   {
                       CommandType = CommandType.StoredProcedure
                   };


                   // Add parameters for the procedure
                   cmd.Parameters.Add("@username", SqlDbType.NVarChar, 15).Value = username.Trim();
                   cmd.Parameters.Add("@pwd", SqlDbType.NVarChar, 50).Value = encryptedPassword;
                   SqlParameter LIDParameter = cmd.Parameters.Add("@LID", SqlDbType.Int);
                   LIDParameter.Direction = ParameterDirection.Output;
                   SqlParameter accIDParameter = cmd.Parameters.Add("@accID", SqlDbType.SmallInt);
                   accIDParameter.Direction = ParameterDirection.Output;


                   SqlParameter returnParameter = cmd.Parameters.Add("@ReturnVal", SqlDbType.Bit);
                   returnParameter.Direction = ParameterDirection.ReturnValue;


                   try
                   {
                       // Execute the procedure
                       cmd.ExecuteNonQuery();
                       bool result = Convert.ToBoolean(cmd.Parameters["@ReturnVal"].Value);


                       if (result)
                       {
                           //Console.WriteLine($"Login successful. LID: {LID}, Access: {accID}");
                       LID = cmd.Parameters["@LID"].Value.ToString();
                       accID = cmd.Parameters["@accID"].Value.ToString();
                           return true;
                       }
                       else
                       {
                           Console.WriteLine("Login failed. Invalid username or password.");
                           return false;
                       }
                   }
                   catch (Exception e)
                   {
                       // Log the exception message to the console
                       Console.WriteLine(e.Message);
                       return false;
                   }
               }
               else
               {
                   Console.WriteLine("Login failed. Passwords do not match.");
                    //Console.WriteLine($"Input Encrypted Password Dave: {encryptedPassword}");
                   return false;
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
   }
}