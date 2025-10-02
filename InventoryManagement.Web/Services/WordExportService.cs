using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace InventoryManagement.Web.Services
{
    public class WordExportService : IWordExportService
    {
        public byte[] GenerateDepartmentInventoryDocument(
            DepartmentViewModel department,
            List<ProductViewModel> products)
        {
            using var memoryStream = new MemoryStream();

            using (var wordDocument = WordprocessingDocument.Create(
                memoryStream, WordprocessingDocumentType.Document, true))
            {
                var mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = mainPart.Document.AppendChild(new Body());

                // Add document title (centered, bordered)
                AddTitle(body);

                // Add spacing
                body.AppendChild(CreateEmptyParagraph());

                // Add logo placeholders (centered)
                AddLogoSection(body);

                // Add spacing
                body.AppendChild(CreateEmptyParagraph());

                // Add handover committee section
                AddCommitteeSection(body, department);

                // Add spacing
                body.AppendChild(CreateEmptyParagraph());

                // Add date
                AddDate(body);

                // Add spacing
                body.AppendChild(CreateEmptyParagraph());

                // Add main products table
                AddInventoryTable(body, products);

                // Add spacing
                body.AppendChild(CreateEmptyParagraph());

                // Add signature section
                AddSignatureSection(body, department);

                // Add footer
                AddFooter(body, department);
            }

            return memoryStream.ToArray();
        }

        private void AddTitle(Body body)
        {
            // Create the title paragraph with border
            var titlePara = new Paragraph();

            var paraProp = new ParagraphProperties();
            paraProp.Append(new Justification { Val = JustificationValues.Center });

            // Add border around the title
            var paraBorder = new ParagraphBorders(
                new TopBorder { Val = BorderValues.Single, Size = 12, Space = 4, Color = "000000" },
                new BottomBorder { Val = BorderValues.Single, Size = 12, Space = 4, Color = "000000" },
                new LeftBorder { Val = BorderValues.Single, Size = 12, Space = 4, Color = "000000" },
                new RightBorder { Val = BorderValues.Single, Size = 12, Space = 4, Color = "000000" }
            );
            paraProp.Append(paraBorder);

            // Add spacing
            paraProp.Append(new SpacingBetweenLines { Before = "240", After = "240" });

            titlePara.Append(paraProp);

            var run = new Run();
            var runProp = new RunProperties();
            runProp.Append(new Bold());
            runProp.Append(new FontSize { Val = "28" });
            run.Append(runProp);
            run.Append(new Text("\"İT\" AVADANLIQLARININ İNVENTARİZASİYASI"));

            titlePara.Append(run);
            body.Append(titlePara);
        }

        private void AddLogoSection(Body body)
        {
            // Create a paragraph for logo placeholders (centered)
            var logoPara = new Paragraph();
            var paraProp = new ParagraphProperties();
            paraProp.Append(new Justification { Val = JustificationValues.Center });
            logoPara.Append(paraProp);

            var run = new Run();
            var runProp = new RunProperties();
            runProp.Append(new FontSize { Val = "20" });
            run.Append(runProp);
            run.Append(new Text("[LOGO]                    [LOGO]"));

            logoPara.Append(run);
            body.Append(logoPara);
        }

        private void AddCommitteeSection(Body body, DepartmentViewModel department)
        {
            // "Təhvil-təslim Heyəti:" header with border
            var committeePara = new Paragraph();
            var paraProp = new ParagraphProperties();

            var paraBorder = new ParagraphBorders(
                new TopBorder { Val = BorderValues.Single, Size = 6, Color = "000000" },
                new BottomBorder { Val = BorderValues.Single, Size = 6, Color = "000000" },
                new LeftBorder { Val = BorderValues.Single, Size = 6, Color = "000000" },
                new RightBorder { Val = BorderValues.Single, Size = 6, Color = "000000" }
            );
            paraProp.Append(paraBorder);
            paraProp.Append(new SpacingBetweenLines { Before = "120", After = "120" });

            committeePara.Append(paraProp);

            var run = new Run();
            var runProp = new RunProperties();
            runProp.Append(new Bold());
            runProp.Append(new FontSize { Val = "22" });
            run.Append(runProp);
            run.Append(new Text("Təhvil-təslim Heyəti:"));

            committeePara.Append(run);
            body.Append(committeePara);

            // Committee member name (in bordered box)
            var namePara = new Paragraph();
            var nameParaProp = new ParagraphProperties();

            var nameBorder = new ParagraphBorders(
                new BottomBorder { Val = BorderValues.Single, Size = 6, Color = "000000" },
                new LeftBorder { Val = BorderValues.Single, Size = 6, Color = "000000" },
                new RightBorder { Val = BorderValues.Single, Size = 6, Color = "000000" }
            );
            nameParaProp.Append(nameBorder);
            nameParaProp.Append(new SpacingBetweenLines { Before = "120", After = "120" });

            namePara.Append(nameParaProp);

            var nameRun = new Run();
            var nameRunProp = new RunProperties();
            nameRunProp.Append(new FontSize { Val = "22" });
            nameRun.Append(nameRunProp);
            nameRun.Append(new Text($"Kənan Əhədzadə"));

            namePara.Append(nameRun);
            body.Append(namePara);
        }

        private void AddDate(Body body)
        {
            var datePara = new Paragraph();
            var paraProp = new ParagraphProperties();

            var paraBorder = new ParagraphBorders(
                new TopBorder { Val = BorderValues.Single, Size = 6, Color = "000000" },
                new BottomBorder { Val = BorderValues.Single, Size = 6, Color = "000000" },
                new LeftBorder { Val = BorderValues.Single, Size = 6, Color = "000000" },
                new RightBorder { Val = BorderValues.Single, Size = 6, Color = "000000" }
            );
            paraProp.Append(paraBorder);
            paraProp.Append(new SpacingBetweenLines { Before = "120", After = "120" });

            datePara.Append(paraProp);

            var run = new Run();
            var runProp = new RunProperties();
            runProp.Append(new Bold());
            runProp.Append(new FontSize { Val = "22" });
            run.Append(runProp);
            run.Append(new Text($"Tarix: {DateTime.Now:dd.MM.yyyy}"));

            datePara.Append(run);
            body.Append(datePara);
        }

        private void AddInventoryTable(Body body, List<ProductViewModel> products)
        {
            var table = new Table();

            // Table properties
            var tblProp = new TableProperties();
            tblProp.Append(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct });

            // Table borders
            var tblBorders = new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                new BottomBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                new LeftBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                new RightBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6, Color = "000000" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 6, Color = "000000" }
            );
            tblProp.Append(tblBorders);

            // Table layout
            tblProp.Append(new TableLayout { Type = TableLayoutValues.Fixed });

            table.Append(tblProp);

            // Add header row
            var headerRow = new TableRow();
            headerRow.Append(CreateHeaderCell("İnventarın adı", 2000));
            headerRow.Append(CreateHeaderCell("İnventarın modeli", 2000));
            headerRow.Append(CreateHeaderCell("İnventar kodu", 1000));
            headerRow.Append(CreateHeaderCell("Kateqoriya", 1500));
            headerRow.Append(CreateHeaderCell("İşçi", 1500));
            table.Append(headerRow);

            // Group products by vendor (similar to how the original groups by type)
            var groupedProducts = products
                .OrderBy(p => p.Vendor)
                .ThenBy(p => p.InventoryCode)
                .ToList();

            // Add data rows
            foreach (var product in groupedProducts)
            {
                var dataRow = new TableRow();

                dataRow.Append(CreateDataCell(product.Vendor ?? "N/A"));
                dataRow.Append(CreateDataCell(product.Model ?? "N/A"));
                dataRow.Append(CreateDataCell(product.InventoryCode.ToString()));
                dataRow.Append(CreateDataCell(product.CategoryName ?? "N/A"));
                dataRow.Append(CreateDataCell(product.Worker ?? "-"));

                table.Append(dataRow);
            }

            // Add total row
            var totalRow = new TableRow();
            var totalCell = new TableCell();

            var totalCellProp = new TableCellProperties();
            totalCellProp.Append(new GridSpan { Val = 2 });
            totalCellProp.Append(new TableCellWidth { Width = "4000", Type = TableWidthUnitValues.Dxa });
            totalCell.Append(totalCellProp);

            var totalPara = new Paragraph();
            var totalRun = new Run();
            var totalRunProp = new RunProperties();
            totalRunProp.Append(new Bold());
            totalRunProp.Append(new FontSize { Val = "22" });
            totalRun.Append(totalRunProp);
            totalRun.Append(new Text("Cəmi:"));
            totalPara.Append(totalRun);
            totalCell.Append(totalPara);

            totalRow.Append(totalCell);

            // Total count cell
            var countCell = new TableCell();
            var countCellProp = new TableCellProperties();
            countCellProp.Append(new GridSpan { Val = 3 });
            countCell.Append(countCellProp);

            var countPara = new Paragraph();
            var countRun = new Run();
            var countRunProp = new RunProperties();
            countRunProp.Append(new Bold());
            countRunProp.Append(new FontSize { Val = "22" });
            countRun.Append(countRunProp);
            countRun.Append(new Text($"{products.Count}"));
            countPara.Append(countRun);
            countCell.Append(countPara);

            totalRow.Append(countCell);
            table.Append(totalRow);

            body.Append(table);
        }

        private void AddSignatureSection(Body body, DepartmentViewModel department)
        {
            // Create signature table
            var signatureTable = new Table();

            var tblProp = new TableProperties();
            tblProp.Append(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct });

            // Table borders
            var tblBorders = new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 6, Color = "000000" },
                new BottomBorder { Val = BorderValues.Single, Size = 6, Color = "000000" },
                new LeftBorder { Val = BorderValues.Single, Size = 6, Color = "000000" },
                new RightBorder { Val = BorderValues.Single, Size = 6, Color = "000000" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 6, Color = "000000" }
            );
            tblProp.Append(tblBorders);

            signatureTable.Append(tblProp);

            var signatureRow = new TableRow();

            // Left cell - Təhvil verdi (Handed over by)
            var leftCell = new TableCell();
            var leftCellProp = new TableCellProperties();
            leftCellProp.Append(new TableCellWidth { Width = "2500", Type = TableWidthUnitValues.Pct });
            leftCellProp.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
            leftCell.Append(leftCellProp);

            var leftPara = new Paragraph();
            var leftRun = new Run();
            var leftRunProp = new RunProperties();
            leftRunProp.Append(new FontSize { Val = "22" });
            leftRun.Append(leftRunProp);
            leftRun.Append(new Text("Təhvil verdi: Y.Bağıyev _______________"));
            leftPara.Append(leftRun);
            leftCell.Append(leftPara);

            signatureRow.Append(leftCell);

            // Right cell - Təhvil aldı (Received by)
            var rightCell = new TableCell();
            var rightCellProp = new TableCellProperties();
            rightCellProp.Append(new TableCellWidth { Width = "2500", Type = TableWidthUnitValues.Pct });
            rightCellProp.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
            rightCell.Append(rightCellProp);

            var rightPara = new Paragraph();
            var rightRun = new Run();
            var rightRunProp = new RunProperties();
            rightRunProp.Append(new FontSize { Val = "22" });
            rightRun.Append(rightRunProp);
            rightRun.Append(new Text($"Təhvil aldı: {department.Name} Müdiri _______________"));
            rightPara.Append(rightRun);
            rightCell.Append(rightPara);

            signatureRow.Append(rightCell);

            signatureTable.Append(signatureRow);
            body.Append(signatureTable);
        }

        private void AddFooter(Body body, DepartmentViewModel department)
        {
            body.Append(CreateEmptyParagraph());

            var footerPara = new Paragraph();
            var paraProp = new ParagraphProperties();
            paraProp.Append(new Justification { Val = JustificationValues.Center });
            footerPara.Append(paraProp);

            var run = new Run();
            var runProp = new RunProperties();
            runProp.Append(new Bold());
            runProp.Append(new Underline { Val = UnderlineValues.Single });
            runProp.Append(new FontSize { Val = "24" });
            run.Append(runProp);
            run.Append(new Text($"{department.Name}"));

            footerPara.Append(run);
            body.Append(footerPara);
        }

        // Helper methods
        private TableCell CreateHeaderCell(string text, int width)
        {
            var cell = new TableCell();

            var cellProp = new TableCellProperties();
            cellProp.Append(new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa });
            cellProp.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = "D9D9D9" });
            cellProp.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
            cell.Append(cellProp);

            var para = new Paragraph();
            var paraProp = new ParagraphProperties();
            paraProp.Append(new Justification { Val = JustificationValues.Center });
            para.Append(paraProp);

            var run = new Run();
            var runProp = new RunProperties();
            runProp.Append(new Bold());
            runProp.Append(new FontSize { Val = "20" });
            run.Append(runProp);
            run.Append(new Text(text));

            para.Append(run);
            cell.Append(para);

            return cell;
        }

        private TableCell CreateDataCell(string text)
        {
            var cell = new TableCell();

            var cellProp = new TableCellProperties();
            cellProp.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
            cell.Append(cellProp);

            var para = new Paragraph();

            var run = new Run();
            var runProp = new RunProperties();
            runProp.Append(new FontSize { Val = "20" });
            run.Append(runProp);
            run.Append(new Text(text));

            para.Append(run);
            cell.Append(para);

            return cell;
        }

        private Paragraph CreateEmptyParagraph()
        {
            return new Paragraph(new Run(new Text("")));
        }
    }
}