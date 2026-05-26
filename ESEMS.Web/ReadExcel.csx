using ClosedXML.Excel;
using System;

var workbook = new XLWorkbook(@"C:\Users\kalmi\OneDrive\Desktop\MB1\ESEMS.Web\full.xlsx");
Console.WriteLine($"=== WORKBOOK: {workbook.Worksheets.Count} sheets ===\n");

foreach (var sheet in workbook.Worksheets)
{
    Console.WriteLine($"=== SHEET: {sheet.Name} ===");
    var range = sheet.RangeUsed();
    if (range == null) { Console.WriteLine("(Empty sheet)\n"); continue; }
    
    int rowCount = range.RowCount();
    int colCount = range.ColumnCount();
    Console.WriteLine($"Rows: {rowCount}, Columns: {colCount}\n");
    
    // Headers
    Console.WriteLine("Headers:");
    for (int col = 1; col <= Math.Min(colCount, 25); col++)
    {
        var header = sheet.Cell(1, col).GetString();
        if (!string.IsNullOrEmpty(header)) Console.WriteLine($"  Col {col}: {header}");
    }
    Console.WriteLine();
    
    // Sample data
    Console.WriteLine("Sample Data (rows 2-15):");
    for (int row = 2; row <= Math.Min(rowCount, 15); row++)
    {
        var cells = new System.Collections.Generic.List<string>();
        for (int col = 1; col <= Math.Min(colCount, 15); col++)
        {
            var val = sheet.Cell(row, col).GetString();
            if (!string.IsNullOrEmpty(val)) cells.Add($"[{col}]{val}");
        }
        if (cells.Count > 0) Console.WriteLine($"  R{row}: {string.Join(" | ", cells)}");
    }
    Console.WriteLine();
}
