using ClosedXML.Excel;
using System.IO;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StoreSteels.Models;
using System;
using System.Collections.Generic;

namespace StoreSteels.Services
{
    public class ExportService
    {
        public void GenerateA4Pdf(List<ProductControlModel> items)
        {
            // กำหนด Path ไปยัง Desktop (Dynamic สำหรับทุกเครื่อง)
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string folderPath = Path.Combine(desktopPath, "StoreSteels_Export");
            string fileName = $"Export_QR_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string fullPath = Path.Combine(folderPath, fileName);

            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            {
                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1, Unit.Centimetre);
                        page.Content().Grid(grid =>
                        {
                            grid.Columns(4);
                            foreach (var item in items)
                            {
                                grid.Item().AlignCenter().Padding(5).Column(col =>
                                {
                                    // ✅ รวมข้อมูลตาม Format ที่นนท์ต้องการให้เหมือน Log
                                    // ใช้ ?? เพื่อใส่ค่าว่าง (N/A หรือ 0) กันพังกรณีข้อมูลใน DB เป็น Null
                                    // แก้ไขใน ExportService.cs
                                    // รูปแบบ: Category | PartCode | PartName | PackSize | Stock (หรือข้อมูลอื่นๆ ที่ระบบสแกนต้องการ)
                                    string fullDetailForQR = $"{item.Category ?? "N/A"}|" +
                                                             $"{item.PartCode ?? "N/A"}|" +
                                                             $"{item.PartName ?? "N/A"}|" +
                                                             $"{item.PackSize ?? "0"}|" +
                                                             $"{item.Stock ?? "0"}";

                                    // สร้าง QR Code (ใช้ ECCLevel.M เพื่อให้สแกนง่ายในขณะที่ข้อมูลค่อนข้างยาว)
                                    QRCodeData qrCodeData = qrGenerator.CreateQrCode(fullDetailForQR, QRCodeGenerator.ECCLevel.M);
                                    PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
                                    byte[] qrBytes = qrCode.GetGraphic(20);

                                    // 1. แสดงรูป QR Code
                                    col.Item().Width(85).AlignCenter().Image(qrBytes);

                                    // 2. แสดงรหัสและชื่อใต้ภาพเพื่อให้คนอ่านออกด้วย
                                    col.Item().AlignCenter().PaddingTop(2).Text(item.PartCode).Bold().FontSize(9);
                                    col.Item().AlignCenter().Text(item.PartName).FontSize(7).FontColor(Colors.Grey.Medium);

                                    col.Item().PaddingBottom(15);
                                });
                            }
                        });
                    });
                }).GeneratePdf(fullPath);
            }
        }


        #region === [ Export PR to Excel ] ===

        // ใน ExportService.cs
        public void ExportPRToExcel(IEnumerable<PRModel> dataList)
        {
            string targetPath = @"\\192.168.10.3\01_นารีพร 31-10-15 17.00\1.Pongpakorn_Non\5. RPA - Product ( All Part )\RPA - PR\RPA - PR Spare Part\PR StoreSP.xlsx";

            try
            {
                if (!File.Exists(targetPath))
                {
                    // ใช้ Exception เฉพาะทางเพื่อให้ catch แยกประเภทได้ชัดเจน
                    throw new FileNotFoundException($"ไม่พบไฟล์ที่เส้นทาง: {targetPath}");
                }

                if (IsFileLocked(new FileInfo(targetPath)))
                {
                    // ตัวนี้แหละครับที่จะเด้งไปที่ Catch
                    throw new Exception("ไฟล์ Excel ถูกเปิดค้างอยู่ กรุณาปิดไฟล์ก่อนทำการ Export");
                }

                using (var workbook = new XLWorkbook(targetPath))
                {
                    var worksheet = workbook.Worksheet(1);
                    var lastRowUsed = worksheet.LastRowUsed();
                    int currentRow = (lastRowUsed != null) ? lastRowUsed.RowNumber() + 1 : 2;

                    foreach (var item in dataList)
                    {
                        var dateCell = worksheet.Cell(currentRow, 1);
                        dateCell.Value = item.PR_DATE;
                        dateCell.Style.DateFormat.Format = "dd/MM/yyyy";

                        worksheet.Cell(currentRow, 2).Value = item.Department;
                        worksheet.Cell(currentRow, 3).Value = item.PartCode;
                        worksheet.Cell(currentRow, 4).Value = item.PartName;
                        worksheet.Cell(currentRow, 5).Value = item.QTY;
                        worksheet.Cell(currentRow, 6).Value = item.PR_REM;
                        worksheet.Cell(currentRow, 7).Value = "ชนนิกานต์";
                        worksheet.Cell(currentRow, 8).Value = "41304131-ปั๊ม 1@469214,403";           // <-- เพิ่มเติม : ถ้าเป็นค่า Fix นอกเหนือจากโปรแกรมสามารถเพิ่มเข้าไปที่ช่องนี้ได้เลยครับ

                        worksheet.Range(currentRow, 1, currentRow, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        currentRow++;
                    }
                    workbook.Save();
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        // Helper Function สำหรับเช็คไฟล์ Lock
        private bool IsFileLocked(FileInfo file)
        {
            try
            {
                using (FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException) { return true; }
            return false;
        }

        #endregion

    }
}