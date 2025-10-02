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
        private readonly IWebHostEnvironment _environment;

        // The golden brand color #FFC000 - we'll use this for text highlighting
        private const string BRAND_COLOR = "FFC000";

        public WordExportService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

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

                // Set reduced page margins for more content space
                SetPageMargins(mainPart);

                // 1. Add header with larger logo and colored title
                AddHeaderWithLogoAndTitle(body, mainPart);

                // Small spacing
                body.AppendChild(CreateSmallSpacingParagraph());

                // 2. Add committee section with text highlighting only
                AddCommitteeSection(body);

                // 3. Add date section with text highlighting only
                AddDateSection(body);

                // Small spacing before table
                body.AppendChild(CreateSmallSpacingParagraph());

                // 4. Add inventory table with golden headers and black borders
                AddInventoryTable(body, products);

                // Small spacing after table
                body.AppendChild(CreateSmallSpacingParagraph());

                // 5. Add department name in black (no color)
                AddDepartmentName(body, department);

                // Small spacing
                body.AppendChild(CreateSmallSpacingParagraph());

                // 6. Add signature section with full-width golden highlighting
                // This section is marked to keep together (won't split across pages)
                AddSignatureSection(body, department);
            }

            return memoryStream.ToArray();
        }



        /// <summary>
        /// Sets reduced page margins to maximize content space.
        /// Reduces top margin to 0.5 inch and bottom margin to 0.5 inch.
        /// This gives you much more usable space on each page.
        /// </summary>
        private void SetPageMargins(MainDocumentPart mainPart)
        {
            var sectionProperties = new SectionProperties();
            var pageMargin = new PageMargin()
            {
                Top = 720,      // 0.5 inch (reduced from 1 inch)
                Right = 1440U,  // 1 inch (standard)
                Bottom = 720,   // 0.5 inch (reduced from 1 inch)
                Left = 1440U,   // 1 inch (standard)
                Header = 720U,
                Footer = 720U,
                Gutter = 0U
            };
            sectionProperties.Append(pageMargin);
            mainPart.Document.Body!.Append(sectionProperties);
        }



        /// <summary>
        /// Creates the header with an even larger logo (200x200 points) and the colored title.
        /// The logo is now significantly larger to be more prominent in the document.
        /// </summary>
        private void AddHeaderWithLogoAndTitle(Body body, MainDocumentPart mainPart)
        {
            var headerTable = new Table();

            var tblProp = new TableProperties();
            tblProp.Append(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct });

            // No borders on the header table
            var tblBorders = new TableBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new InsideHorizontalBorder { Val = BorderValues.None },
                new InsideVerticalBorder { Val = BorderValues.None }
            );
            tblProp.Append(tblBorders);
            tblProp.Append(new TableCellSpacing { Width = "0", Type = TableWidthUnitValues.Dxa });

            headerTable.Append(tblProp);

            var headerRow = new TableRow();

            // Left cell - LARGER Logo (increased to 200x200)
            var logoCell = new TableCell();
            var logoCellProp = new TableCellProperties();
            logoCellProp.Append(new TableCellWidth { Width = "2500", Type = TableWidthUnitValues.Dxa });
            logoCellProp.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });

            var cellMargin = new TableCellMargin();
            cellMargin.Append(new TopMargin { Width = "0", Type = TableWidthUnitValues.Dxa });
            cellMargin.Append(new BottomMargin { Width = "0", Type = TableWidthUnitValues.Dxa });
            logoCellProp.Append(cellMargin);

            logoCell.Append(logoCellProp);

            var logoPara = new Paragraph();
            var logoParaProp = new ParagraphProperties();
            logoParaProp.Append(new SpacingBetweenLines { Before = "0", After = "0" });
            logoPara.Append(logoParaProp);

            try
            {
                var logoPath = Path.Combine(_environment.WebRootPath, "logo.jpg");

                if (File.Exists(logoPath))
                {
                    // INCREASED SIZE: from 150x150 to 200x200 for much larger logo
                    var logoRun = CreateImageRun(mainPart, logoPath, "Logo", 200, 200);
                    logoPara.Append(logoRun);
                }
                else
                {
                    var logoRun = CreateTextRun("[LOGO]", 24, true, true);
                    logoPara.Append(logoRun);
                }
            }
            catch
            {
                var logoRun = CreateTextRun("[LOGO]", 24, true, true);
                logoPara.Append(logoRun);
            }

            logoCell.Append(logoPara);
            headerRow.Append(logoCell);

            // Right cell - Title with golden TEXT color (not background)
            var titleCell = new TableCell();
            var titleCellProp = new TableCellProperties();
            titleCellProp.Append(new TableCellWidth { Width = "3000", Type = TableWidthUnitValues.Dxa });
            titleCellProp.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });

            var titleCellMargin = new TableCellMargin();
            titleCellMargin.Append(new TopMargin { Width = "0", Type = TableWidthUnitValues.Dxa });
            titleCellMargin.Append(new BottomMargin { Width = "0", Type = TableWidthUnitValues.Dxa });
            titleCellProp.Append(titleCellMargin);

            titleCell.Append(titleCellProp);

            // First line with golden text color
            var titlePara1 = new Paragraph();
            var titleParaProp1 = new ParagraphProperties();
            titleParaProp1.Append(new Justification { Val = JustificationValues.Right });
            titleParaProp1.Append(new SpacingBetweenLines { Before = "0", After = "0", Line = "240" });
            titlePara1.Append(titleParaProp1);

            var titleRun1 = CreateColoredTextRun("IT AVADANLIQLARININ", 36, true, true, BRAND_COLOR);
            titlePara1.Append(titleRun1);
            titleCell.Append(titlePara1);

            // Second line with golden text color
            var titlePara2 = new Paragraph();
            var titleParaProp2 = new ParagraphProperties();
            titleParaProp2.Append(new Justification { Val = JustificationValues.Right });
            titleParaProp2.Append(new SpacingBetweenLines { Before = "0", After = "0", Line = "240" });
            titlePara2.Append(titleParaProp2);

            var titleRun2 = CreateColoredTextRun("İNVENTARİZASİYASI", 36, true, true, BRAND_COLOR);
            titlePara2.Append(titleRun2);
            titleCell.Append(titlePara2);

            headerRow.Append(titleCell);
            headerTable.Append(headerRow);
            body.Append(headerTable);
        }



        /// <summary>
        /// Creates an image run for logo insertion.
        /// This handles the complex OpenXML structure needed for embedded images.
        /// </summary>
        private Run CreateImageRun(MainDocumentPart mainPart, string imagePath, string imageName, int widthInPoints, int heightInPoints)
        {
            ImagePart imagePart = mainPart.AddImagePart(ImagePartType.Png);

            using (FileStream stream = new FileStream(imagePath, FileMode.Open))
            {
                imagePart.FeedData(stream);
            }

            string relationshipId = mainPart.GetIdOfPart(imagePart);

            long widthInEmus = widthInPoints * 9525;
            long heightInEmus = heightInPoints * 9525;

            var element = new Drawing(
                new DW.Inline(
                    new DW.Extent() { Cx = widthInEmus, Cy = heightInEmus },
                    new DW.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                    new DW.DocProperties() { Id = 1U, Name = imageName },
                    new DW.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks() { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties() { Id = 0U, Name = imageName },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    new A.Blip() { Embed = relationshipId },
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset() { X = 0L, Y = 0L },
                                        new A.Extents() { Cx = widthInEmus, Cy = heightInEmus }),
                                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))
                        )
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
                )
                {
                    DistanceFromTop = 0U,
                    DistanceFromBottom = 0U,
                    DistanceFromLeft = 0U,
                    DistanceFromRight = 0U
                });

            return new Run(element);
        }



        /// <summary>
        /// Adds the committee section with TEXT HIGHLIGHTING only (not cell background).
        /// This creates the effect of using a highlighter pen on just the text,
        /// leaving white space visible around the edges.
        /// </summary>
        private void AddCommitteeSection(Body body)
        {
            // Header paragraph with regular text (no highlight)
            var headerPara = new Paragraph();
            var headerParaProp = new ParagraphProperties();
            headerParaProp.Append(new Justification { Val = JustificationValues.Center });
            headerParaProp.Append(new SpacingBetweenLines { Before = "120", After = "60" });
            headerPara.Append(headerParaProp);

            // Changed from CreateHighlightedTextRun to CreateTextRun
            var headerRun = CreateTextRun("Təhvil-təslim Heyəti:", 32, true, true);
            headerPara.Append(headerRun);
            body.Append(headerPara);

            // Name paragraph with regular text (no highlight)
            var namePara = new Paragraph();
            var nameParaProp = new ParagraphProperties();
            nameParaProp.Append(new Justification { Val = JustificationValues.Center });
            nameParaProp.Append(new SpacingBetweenLines { Before = "60", After = "120" });
            namePara.Append(nameParaProp);

            // Changed from CreateHighlightedTextRun to CreateTextRun
            var nameRun = CreateTextRun("Kənan Əhədzadə", 28, false, true);
            namePara.Append(nameRun);
            body.Append(namePara);
        }



        /// <summary>
        /// Adds the date section with TEXT HIGHLIGHTING only.
        /// Again, this highlights just the text itself, not the entire line.
        /// </summary>
        private void AddDateSection(Body body)
        {
            var datePara = new Paragraph();
            var dateParaProp = new ParagraphProperties();
            dateParaProp.Append(new SpacingBetweenLines { Before = "120", After = "120" });
            datePara.Append(dateParaProp);

            // Changed from CreateHighlightedTextRun to CreateTextRun
            var dateRun = CreateTextRun($"Tarix: {DateTime.Now:dd.MM.yyyy}", 32, true, true);
            datePara.Append(dateRun);
            body.Append(datePara);
        }



        /// <summary>
        /// Creates the inventory table with golden column headers and BLACK borders.
        /// All data is centered, table borders are now black (not golden).
        /// </summary>
        private void AddInventoryTable(Body body, List<ProductViewModel> products)
        {
            var table = new Table();

            var tblProp = new TableProperties();
            tblProp.Append(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct });

            // ALL borders are BLACK (000000) as requested
            var tblBorders = new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                new BottomBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                new LeftBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                new RightBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6, Color = "000000" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 6, Color = "000000" }
            );
            tblProp.Append(tblBorders);
            tblProp.Append(new TableLayout { Type = TableLayoutValues.Fixed });

            table.Append(tblProp);

            // Header row - only the header row has golden background
            var headerRow = new TableRow();
            headerRow.Append(CreateHeaderCell("Avadanlıq", 2000));
            headerRow.Append(CreateHeaderCell("Vendor", 2000));
            headerRow.Append(CreateHeaderCell("Model", 2000));
            headerRow.Append(CreateHeaderCell("İnventar kodu", 1500));
            table.Append(headerRow);

            // Sort products by category, then inventory code
            var sortedProducts = products
                .OrderBy(p => p.CategoryName)
                .ThenBy(p => p.InventoryCode)
                .ToList();

            // Data rows - all centered
            foreach (var product in sortedProducts)
            {
                var dataRow = new TableRow();

                dataRow.Append(CreateCenteredDataCell(product.CategoryName ?? "N/A"));
                dataRow.Append(CreateCenteredDataCell(product.Vendor ?? "N/A"));
                dataRow.Append(CreateCenteredDataCell(product.Model ?? "N/A"));
                dataRow.Append(CreateCenteredDataCell(product.InventoryCode.ToString()));

                table.Append(dataRow);
            }

            // Total row - "Cəmi:" left-aligned, count centered
            var totalRow = new TableRow();

            var totalLabelCell = new TableCell();
            var totalLabelCellProp = new TableCellProperties();
            totalLabelCellProp.Append(new GridSpan { Val = 3 });
            totalLabelCellProp.Append(new TableCellWidth { Width = "6000", Type = TableWidthUnitValues.Dxa });
            totalLabelCell.Append(totalLabelCellProp);

            var totalLabelPara = new Paragraph();
            var totalLabelParaProp = new ParagraphProperties();
            totalLabelParaProp.Append(new Justification { Val = JustificationValues.Left });
            totalLabelPara.Append(totalLabelParaProp);

            var totalLabelRun = CreateTextRun("Cəmi:", 22, true, true);
            totalLabelPara.Append(totalLabelRun);
            totalLabelCell.Append(totalLabelPara);
            totalRow.Append(totalLabelCell);

            var countCell = new TableCell();
            var countCellProp = new TableCellProperties();
            countCellProp.Append(new TableCellWidth { Width = "1500", Type = TableWidthUnitValues.Dxa });
            countCellProp.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
            countCell.Append(countCellProp);

            var countPara = new Paragraph();
            var countParaProp = new ParagraphProperties();
            countParaProp.Append(new Justification { Val = JustificationValues.Center });
            countPara.Append(countParaProp);

            var countRun = CreateTextRun($"{products.Count}", 22, true, true);
            countPara.Append(countRun);
            countCell.Append(countPara);
            totalRow.Append(countCell);

            table.Append(totalRow);
            body.Append(table);
        }



        /// <summary>
        /// Adds the department name in BLACK (no color applied).
        /// Bold and underlined, but standard black text color.
        /// </summary>
        private void AddDepartmentName(Body body, DepartmentViewModel department)
        {
            var deptPara = new Paragraph();
            var deptParaProp = new ParagraphProperties();
            deptParaProp.Append(new Justification { Val = JustificationValues.Left });
            deptParaProp.Append(new SpacingBetweenLines { Before = "240", After = "240" });
            deptPara.Append(deptParaProp);

            // Black color (no special color), just bold and underlined
            var deptRun = CreateTextRun(department.Name, 24, true, true);

            var runProps = deptRun.RunProperties;
            if (runProps != null)
            {
                runProps.Append(new Underline { Val = UnderlineValues.Single });
            }

            deptPara.Append(deptRun);
            body.Append(deptPara);
        }



        /// <summary>
        /// Adds the signature section with FULL-WIDTH golden highlighting.
        /// The entire line is highlighted from start to finish.
        /// Uses KeepNext property to prevent page breaks between signature lines.
        /// </summary>
        private void AddSignatureSection(Body body, DepartmentViewModel department)
        {
            // Create a table with full-width cells for complete background highlighting
            var signatureTable = new Table();

            var tblProp = new TableProperties();
            tblProp.Append(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct });

            // No borders for cleaner look
            var tblBorders = new TableBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new InsideHorizontalBorder { Val = BorderValues.None }
            );
            tblProp.Append(tblBorders);

            // IMPORTANT: Keep this content together (don't split across pages)
            // If there's not enough space, move the entire signature section to next page
            tblProp.Append(new TableStyle { Val = "TableGrid" });

            signatureTable.Append(tblProp);


            var transferredPara = new Paragraph();
            var transferredParaProp = new ParagraphProperties();
            transferredParaProp.Append(new SpacingBetweenLines { Before = "120", After = "120" });
            // Keep with next paragraph to prevent page break
            transferredParaProp.Append(new KeepNext());
            transferredPara.Append(transferredParaProp);

            // Changed from CreateHighlightedTextRun to CreateTextRun
            var transferredRun = CreateTextRun(
                "Təhvil verdi: Yusif Bağıyev ____________________",
                22,
                true,
                true);
            transferredPara.Append(transferredRun);
            body.Append(transferredPara);

            var receivedPara = new Paragraph();
            var receivedParaProp = new ParagraphProperties();
            receivedParaProp.Append(new SpacingBetweenLines { Before = "120", After = "120" });
            // Keep lines together to prevent page breaks
            receivedParaProp.Append(new KeepNext());
            receivedParaProp.Append(new KeepLines());
            receivedPara.Append(receivedParaProp);

            var departmentHeadName = !string.IsNullOrEmpty(department.DepartmentHead)
                ? department.DepartmentHead
                : "_______________";

            // Changed from CreateHighlightedTextRun to CreateTextRun
            var receivedRun = CreateTextRun(
                $"Təhvil aldı: {departmentHeadName} ____________________",
                22,
                true,
                true);
            receivedPara.Append(receivedRun);
            body.Append(receivedPara);

        }



        /// <summary>
        /// Creates a standard text run with Times New Roman font.
        /// This is for regular black text without any special highlighting.
        /// </summary>
        private Run CreateTextRun(string text, int fontSize, bool bold, bool timesNewRoman = true)
        {
            var run = new Run();
            var runProp = new RunProperties();

            if (timesNewRoman)
            {
                runProp.Append(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" });
            }

            runProp.Append(new FontSize { Val = fontSize.ToString() });

            if (bold)
            {
                runProp.Append(new Bold());
            }

            run.Append(runProp);
            run.Append(new Text(text));

            return run;
        }



        /// <summary>
        /// Creates a text run with colored text (used for the title).
        /// The text itself is colored, not the background.
        /// </summary>
        private Run CreateColoredTextRun(string text, int fontSize, bool bold, bool timesNewRoman, string color)
        {
            var run = new Run();
            var runProp = new RunProperties();

            if (timesNewRoman)
            {
                runProp.Append(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" });
            }

            runProp.Append(new FontSize { Val = fontSize.ToString() });

            if (bold)
            {
                runProp.Append(new Bold());
            }

            // Apply color to the text itself
            runProp.Append(new Color { Val = color });

            run.Append(runProp);
            run.Append(new Text(text));

            return run;
        }



        /// <summary>
        /// Creates a text run with HIGHLIGHTING (like using a highlighter pen).
        /// This is different from colored text - it adds a colored background behind the text.
        /// The key is using the Highlight property instead of Shading.
        /// </summary>
        private Run CreateHighlightedTextRun(string text, int fontSize, bool bold, bool timesNewRoman, string highlightColor)
        {
            var run = new Run();
            var runProp = new RunProperties();

            if (timesNewRoman)
            {
                runProp.Append(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" });
            }

            runProp.Append(new FontSize { Val = fontSize.ToString() });

            if (bold)
            {
                runProp.Append(new Bold());
            }

            // This is the key difference: Highlight creates text-level highlighting
            // Note: Highlight in OpenXML uses predefined color names, not hex codes
            // "yellow" is the closest to our golden color
            runProp.Append(new Highlight { Val = HighlightColorValues.Yellow });

            run.Append(runProp);
            run.Append(new Text(text));

            return run;
        }



        /// <summary>
        /// Creates a header cell with golden background.
        /// Only the column headers have this background color.
        /// </summary>
        private TableCell CreateHeaderCell(string text, int width)
        {
            var cell = new TableCell();

            var cellProp = new TableCellProperties();
            cellProp.Append(new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa });
            // Golden background for headers only
            cellProp.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = BRAND_COLOR });
            cellProp.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
            cell.Append(cellProp);

            var para = new Paragraph();
            var paraProp = new ParagraphProperties();
            paraProp.Append(new Justification { Val = JustificationValues.Center });
            para.Append(paraProp);

            var run = CreateTextRun(text, 20, true, true);
            para.Append(run);
            cell.Append(para);

            return cell;
        }



        /// <summary>
        /// Creates a centered data cell for the table.
        /// These cells have no background color - just centered text.
        /// </summary>
        private TableCell CreateCenteredDataCell(string text)
        {
            var cell = new TableCell();

            var cellProp = new TableCellProperties();
            cellProp.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
            cell.Append(cellProp);

            var para = new Paragraph();
            var paraProp = new ParagraphProperties();
            paraProp.Append(new Justification { Val = JustificationValues.Center });
            para.Append(paraProp);

            var run = CreateTextRun(text, 20, false, true);
            para.Append(run);
            cell.Append(para);

            return cell;
        }



        /// <summary>
        /// Creates a small spacing paragraph to separate sections.
        /// </summary>
        private Paragraph CreateSmallSpacingParagraph()
        {
            var para = new Paragraph();
            var paraProp = new ParagraphProperties();
            paraProp.Append(new SpacingBetweenLines { Before = "120", After = "120" });
            para.Append(paraProp);
            para.Append(new Run(new Text("")));
            return para;
        }
    }
}