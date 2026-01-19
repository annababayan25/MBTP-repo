using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using MBTP.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MBTP.Interfaces;
using System.Globalization;
using MBTP.Retrieval;
using System.Text.Json;
using System.Linq;

namespace MBTP.Services
{
    public class TransactionFlowApi : NewbookBaseApi
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public TransactionFlowApi(HttpClient client, IDatabaseConnectionService dbConnectionService) : base(client)
        {
            _dbConnectionService = dbConnectionService;
        }

        public async Task<List<TransactionFlow>> PopulateTransactions(DateTime startDate, DateTime endDate)
        {
            var requestBody = new
            {
                region = region,
                api_key = apiKey,
                period_from = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                period_to = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                return_all_data = "true"
            };

            var json = await PostAsync("reports_transaction_flow", requestBody);

            var result = JsonConvert.DeserializeObject<dynamic>(json.ToString());
            var transactionFlow = new List<TransactionFlow>();

            foreach (var item in result.data)
            {
                var transactions = new TransactionFlow
                {
                    ItemId = item.item_id,
                    AccountFor = item.account_for,
                    AccountForId = item.account_for_id,
                    PaymentMethod = item.payment_transaction_method,
                    PaymentDescription = item.item_description,
                    PaymentTypeReference = item.payment_type_reference,
                    TranslatedPaymentType = item.translated_payment_type,
                    PaymentTypeAction = item.payment_type_action,
                    Category = item.category_name,
                    TransType = item.type,
                    TransDate = item.item_date,
                    ClientAccount = item.client_account,
                    GeneratedBy = item.user_name,
                    Description = item.description,
                    Amount = item.amount,
                    ArrivalDate = item.booking_period_from,
                    DepartureDate = item.booking_period_to,
                    HasArrived = null,
                    BookingCheckedIn = null,
                    Deposit = item.deposit,
                };

                var checkedInInfo = await GetCheckedInInfo(transactions.AccountForId, transactions.TransDate);
                transactions.HasArrived = checkedInInfo.HasArrived;
                transactions.BookingCheckedIn = checkedInInfo.BookingCheckedIn;

                if (item.item_type == "payments_raised")
                {
                    transactions.TransType = "Payments Raised";
                    transactions.PaymentTypeAction = "Payments";
                }
                if (item.item_type == "refunds_raised")
                {
                    transactions.TransType = "Refunds Raised";
                    transactions.PaymentTypeAction = "Refunds";
                }
                if (item.item_type == "payments_voided")
                {
                    transactions.TransType = "Voided Payments Voided";
                    transactions.PaymentTypeAction = "Payments";
                }
                if (item.item_type == "refunds_voided")
                {
                    transactions.TransType = "Voided Refunds Voided";
                    transactions.PaymentTypeAction = "Refunds";
                }

                if (item.payment_transaction_method == "cc_gateway")
                {
                    transactions.PaymentMethod = "Authorize.Net";
                }
                if (item.payment_transaction_method == "manual")
                {
                    transactions.PaymentMethod = "Manual Entry";
                }

                transactions.FormattedTransDate = FormatDate(transactions.TransDate);

                transactions.FormattedTransNumber =
                    $"{transactions.TransType} #{transactions.ItemId}" +
                    (!string.IsNullOrWhiteSpace(transactions.PaymentTypeReference)
                        ? $" (Ref #{transactions.PaymentTypeReference})"
                        : "");

                var paymentDetail =
                    !string.IsNullOrWhiteSpace(transactions.TranslatedPaymentType)
                        ? transactions.TranslatedPaymentType
                        : transactions.PaymentTypeReference;

                transactions.FormattedPaymentMethod =
                    $"{transactions.PaymentMethod} {paymentDetail} {transactions.PaymentTypeAction} - For {FormatShortDate(transactions.TransDate)}";

                transactionFlow.Add(transactions);
                
            }

            return transactionFlow;
        }

        private static string FormatDate(DateTime date)
        {
            return date.ToString("MMM dd yyyy hh:mm tt", CultureInfo.InvariantCulture);
        }

        private static string FormatShortDate(DateTime date)
        {
            return date.ToString("MMM dd yyyy", CultureInfo.InvariantCulture);
        }


        private async Task<(bool HasArrived, DateTime? BookingCheckedIn)> GetCheckedInInfo(int accountForId, DateTime transDate)
        {
            using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
            {
                await sqlConn.OpenAsync();

                var query = "SELECT BookingCheckedIn FROM CheckedIn WHERE BookingId = @BookingId AND BookingName NOT LIKE '%EMERGENCY%'";
                using (var command = new SqlCommand(query, sqlConn))
                {
                    command.Parameters.AddWithValue("@BookingId", accountForId);

                    var result = await command.ExecuteScalarAsync();

                    if (result != null) // Entry exists in the table
                    {
                        if (DateTime.TryParse(result.ToString(), out var bookingCheckedInDate))
                        {
                            // Check if BookingCheckedIn is before the TransDate
                            if (bookingCheckedInDate < transDate)
                            {
                                return (true, bookingCheckedInDate);
                            }
                            else
                            {
                                // Checked in after the transaction date
                                return (false, null);
                            }
                        }
                        else
                        {
                            // Entry exists, but BookingCheckedIn is blank (user hasn't checked in yet)
                            return (false, null);
                        }
                    }
                }
            }

            // No entry exists for the given BookingId
            return (false, null);
        }

    }
}