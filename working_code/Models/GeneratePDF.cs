using System;
using IronPdf;

namespace MBTP.Converter{
public class PDFConverter{
    public static void GeneratePDFFromHtml(string htmlContent, string outputPath){
    try
    {  
    var renderer = new HtmlToPdf(); 
    var pdf = renderer.RenderHtmlAsPdf(htmlContent); 
    pdf.SaveAs(outputPath); 
    Console.WriteLine("PDF successfully created and saved at" + outputPath); 
    } catch (Exception ex) { 
        Console.WriteLine("An error occurred in generation: " + ex.Message); 
        Console.WriteLine("Output path was " + outputPath);
        throw;
}
Console.WriteLine(htmlContent);
}
}
}