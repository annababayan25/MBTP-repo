using FinancialC_;
using GenericSupport;
using System;

namespace MBTP.Retrieval
{
    public partial class ProcessExports
    {
        protected void UploadButton_Click(object sender, EventArgs e)
        {
            DateTime startDate = DateTime.Now;// DateTime.Parse(hiddenDateInput.Value);
            DateTime endDate = DateTime.Now;//DateTime.Parse(hiddenDateInput2.Value);
            bool cnvrtResult;
            for (DateTime counter = startDate; counter <= endDate; counter = counter.AddDays(1))
            {
                GenericRoutines.repDateStr = counter.ToString("yyyy-MM-dd");
                cnvrtResult = System.DateTime.TryParse(GenericRoutines.repDateStr, out GenericRoutines.repDateTmp);
                //DavesMailReader.ReadEmails();
                POSImports.ReadStoreFiles();
                POSImports.ReadArcadeFiles();
                POSImports.ReadCoffeeFiles();
                POSImports.ReadKayakFiles();
                POSImports.ReadGuestFiles();
                POSImports.ReadSpecialAddonsFile();
                NewbookImport.ReadNewbookFiles();
            }
        }
    }
}