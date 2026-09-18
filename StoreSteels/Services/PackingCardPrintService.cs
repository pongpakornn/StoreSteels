using StoreSteels.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Controls;

namespace StoreSteels.Services
{
    // พิมพ์ Packing Card ผ่าน Brother b-PAC SDK (COM) แทน PrintDialog/PrintVisual + PrintTicket ของ
    // WPF เดิม เหตุผล: .NET PrintTicket.PageMediaSize แบบ custom (62mm x 75.4mm) ไม่ถูกไดรเวอร์
    // Brother QL-800 ยอมรับเป็น preset ที่ถูกต้อง ทำให้ขึ้น popup เตือน "ม้วนฉลากหรือเทปภายในเครื่อง
    // ไม่ตรงกับที่เลือกไว้ในแอปพลิเคชัน" ทุกครั้งที่พิมพ์ ต้องกด "ดำเนินการต่อ" เอง ซึ่งพัง flow การวน
    // พิมพ์หลายใบรวดเดียวใน RunPrintFlow (PackingCardView.xaml.cs) ไม่ว่าจะตั้งค่าฝั่งไดรเวอร์ตรงแค่ไหน
    // ก็ตาม เพราะ .NET ส่งขนาดกระดาษผ่าน Print Spooler แบบ GDI ทั่วไป ซึ่งไดรเวอร์ label ของ Brother
    // ไม่รองรับขนาด custom ผ่านทางนี้
    //
    // b-PAC พิมพ์ผ่าน COM ของ Brother โดยตรง (ไม่ผ่าน Print Spooler แบบ GDI) จึงไม่มี popup เตือนนี้
    // และกำหนดความยาว label (Document.Length) ได้ตรงๆ ในโค้ด ไม่ต้องพึ่งค่า default ของไดรเวอร์เลย
    //
    // === ขั้นตอนติดตั้ง/setup ก่อนใช้งาน (ทำครั้งเดียวต่อเครื่อง dev/เครื่องที่ build) ===
    //
    // 1) ติดตั้ง Brother b-PAC SDK
    //    ดาวน์โหลดจากเว็บ Brother Developer Center (ค้นหา "Brother b-PAC SDK") แล้วติดตั้งตามปกติ
    //    (เครื่องนี้ตรวจสอบแล้วว่ามี SDK ติดตั้งอยู่ที่ "C:\Program Files\Brother bPAC3 SDK" และ COM
    //    component "bpac.dll" ลงทะเบียนอยู่ที่ "C:\Program Files\Common Files\Brother\b-PAC\bpac.dll"
    //    เรียบร้อยแล้ว - ไม่ต้องติดตั้งซ้ำ)
    //
    // 2) เพิ่ม COM Reference ให้โปรเจกต์ (มีอยู่แล้วใน StoreSteels.csproj เป็น <COMReference Include="bpac">)
    //    ถ้าต้องเพิ่มเองใหม่ผ่าน Visual Studio: Solution Explorer > โปรเจกต์ StoreSteels > Dependencies
    //    (คลิกขวา) > Add COM Reference... > เลือก "Brother b-PAC 3.4 Type Library" > OK
    //    (ถ้าไม่เห็นในลิสต์ แปลว่ายังไม่ได้ติดตั้ง SDK ตามข้อ 1)
    //
    // 3) สร้างไฟล์ label template ด้วย Brother P-touch Editor (บังคับ - b-PAC ไม่รองรับการสร้าง/แก้ไข
    //    layout จากโค้ดโดยตรง ต้องออกแบบผ่านโปรแกรม P-touch Editor เท่านั้นตามสเปกทางการของ Brother)
    //    เปิด P-touch Editor > เลือกเครื่องพิมพ์ Brother QL-800 > ม้วนเทปต่อเนื่อง 62mm > สร้าง object
    //    ต่อไปนี้ (ตั้งชื่อ object ให้ตรงเป๊ะ เพราะโค้ดด้านล่างอ้างอิงด้วยชื่อพวกนี้ - คลิกขวาที่ object
    //    ในโปรแกรม > Properties > Name เพื่อเปลี่ยนชื่อ):
    //      - Text object ชื่อ "TicketNo"    (เลข Bill)
    //      - Text object ชื่อ "GroupCode"   (คลัง/Group)
    //      - Text object ชื่อ "WorkOrder"   (Work Order)
    //      - Text object ชื่อ "LotNo"       (Lot No.)
    //      - Text object ชื่อ "JobName"     (ชื่อ Part - เปิด Word Wrap ไว้เผื่อข้อความยาว)
    //      - Text object ชื่อ "Qty"         (จำนวน)
    //      - Text object ชื่อ "TicketDate"  (วันที่)
    //      - Barcode object ชื่อ "QrCode" ประเภท Protocol = QR Code วางไว้มุมขวาบนตามเลย์เอาต์เดิม
    //      - Label คงที่อื่นๆ (หัวบริษัท "CH. RADIATORS CO.LTD.", ป้ายกำกับ "Bill:", "Group:",
    //        "Work Order:", "LOT NO.:", "Part Name:", "Quantity:", "Date:") พิมพ์เป็นข้อความนิ่งตรงๆ
    //        ในโปรแกรมได้เลย ไม่ต้องตั้งชื่อ object เพราะโค้ดไม่ได้ไปยุ่งกับพวกนี้
    //    ตั้งความยาว label เริ่มต้นในโปรแกรมเป็นเท่าไรก็ได้ (โค้ดจะ override เป็น 75.4mm เองตอนพิมพ์
    //    ทุกครั้งผ่าน Document.Length) จากนั้น Save As เป็นไฟล์ .lbx ที่:
    //        Assets/Labels/PackingCard.lbx
    //    (สร้างโฟลเดอร์ Assets/Labels ถ้ายังไม่มี - ไฟล์นี้ถูกตั้งให้ copy ไปที่ output โดยอัตโนมัติ
    //    ใน StoreSteels.csproj อยู่แล้ว)
    //
    // เมื่อทำครบ 3 ข้อแล้วจะพิมพ์ได้ปกติโดยไม่มี popup เตือนม้วนฉลากไม่ตรงอีกต่อไป
    public class PackingCardPrintService
    {
        private const double LabelLengthMm = 75.4; // ความยาวป้ายต่อดวง (ปรับได้ตามที่ทดสอบพิมพ์ได้จริง)
        private const double TwipsPerMm = 1440.0 / 25.4; // หน่วยของ Document.Length คือ 1/1440 นิ้ว

        private static readonly string TemplatePath =
            Path.Combine(AppContext.BaseDirectory, "Assets", "Labels", "PackingCard.lbx");

        // คืนรายการที่พิมพ์สำเร็จจริง (เรียงตามลำดับที่พิมพ์) - onProgress แจ้งความคืบหน้าจริงทีละใบ
        // (คง signature เดิมไว้ทุกประการ เพื่อไม่ต้องแก้ RunPrintFlow ใน PackingCardView.xaml.cs เลย)
        public List<PackingCardModel> PrintCards(IList<PackingCardModel> items, Action<int, int> onProgress = null)
        {
            var printed = new List<PackingCardModel>();
            if (items == null || items.Count == 0) return printed;

            if (!File.Exists(TemplatePath))
            {
                throw new InvalidOperationException(
                    "ไม่พบไฟล์ label template: " + TemplatePath + Environment.NewLine + Environment.NewLine +
                    "ต้องสร้างไฟล์นี้ด้วย Brother P-touch Editor ก่อน (b-PAC ไม่รองรับการสร้าง layout จากโค้ด):" + Environment.NewLine +
                    "1. เปิด P-touch Editor > เลือกเครื่องพิมพ์ Brother QL-800 > ม้วนเทปต่อเนื่อง 62mm" + Environment.NewLine +
                    "2. สร้าง Text object ตั้งชื่อให้ตรงเป๊ะ: TicketNo, GroupCode, WorkOrder, LotNo, JobName, Qty, TicketDate" + Environment.NewLine +
                    "3. สร้าง Barcode object ชื่อ QrCode ประเภท Protocol = QR Code" + Environment.NewLine +
                    "4. ป้ายกำกับ/โลโก้อื่นๆ พิมพ์เป็นข้อความนิ่งได้เลย ไม่ต้องตั้งชื่อ" + Environment.NewLine +
                    "5. Save As เป็นไฟล์ที่: " + TemplatePath + Environment.NewLine + Environment.NewLine +
                    "(รายละเอียดเพิ่มเติมในคอมเมนต์ต้นไฟล์ PackingCardPrintService.cs)");
            }

            // ใช้ PrintDialog ของ WPF แค่ "เลือกเครื่องพิมพ์ + ให้กด Cancel ได้" เหมือนของเดิม
            // (ไม่แตะ PrintTicket.PageMediaSize/PageOrientation อีกต่อไป - ต้นเหตุของ popup เตือน
            // ที่เกิดจากการส่งขนาดกระดาษ custom ผ่าน GDI Print Spooler ของ .NET)
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() != true) return printed;

            string printerName = printDialog.PrintQueue?.Name;

            bpac.Document doc = new bpac.DocumentClass();
            try
            {
                if (!doc.Open(TemplatePath))
                {
                    throw new InvalidOperationException(
                        $"เปิด Packing Card template ไม่สำเร็จ (b-PAC ErrorCode={doc.ErrorCode}): {TemplatePath}");
                }

                if (!string.IsNullOrEmpty(printerName))
                {
                    doc.SetPrinter(printerName, false);
                }

                // กำหนดความยาว label ตรงๆ ผ่าน b-PAC (หน่วย 1/1440 นิ้ว) ไม่พึ่งค่า default ของไดรเวอร์
                doc.Length = (int)Math.Round(LabelLengthMm * TwipsPerMm);

                int qrIndex = doc.GetBarcodeIndex("QrCode");

                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    ApplyFields(doc, item, qrIndex);

                    doc.StartPrint("", bpac.PrintOptionConstants.bpoDefault);
                    doc.PrintOut(1, bpac.PrintOptionConstants.bpoDefault);
                    doc.EndPrint();

                    printed.Add(item);
                    onProgress?.Invoke(printed.Count, items.Count);
                }
            }
            finally
            {
                doc.Close();
                Marshal.ReleaseComObject(doc);
            }

            return printed;
        }

        private static void ApplyFields(bpac.Document doc, PackingCardModel item, int qrIndex)
        {
            SetText(doc, "TicketNo", item.TicketNo);
            SetText(doc, "GroupCode", item.GroupCode);
            SetText(doc, "WorkOrder", item.WorkOrder);
            SetText(doc, "LotNo", item.LotNo);
            SetText(doc, "JobName", item.JobName);
            SetText(doc, "Qty", item.Qty.ToString("0.##"));
            SetText(doc, "TicketDate", item.TicketDate == DateTime.MinValue ? "" : item.TicketDate.ToString("dd-MM-yyyy"));

            // Object.Text ใช้ตั้งค่าได้เฉพาะ object ประเภทข้อความ (bobText) เท่านั้น - barcode/QR ต้อง
            // ตั้งผ่าน Document.SetBarcodeData(index, data) โดยเฉพาะตามสเปกของ b-PAC
            if (qrIndex >= 0)
            {
                doc.SetBarcodeData(qrIndex, item.QrText ?? "");
            }
        }

        // GetObject คืนค่า null ถ้าไม่เจอ object ชื่อนั้นในเทมเพลต (เช่น template ยังสร้างไม่ครบ) -
        // ข้ามเงียบๆ แทนที่จะพัง เพื่อให้ยังพิมพ์ field อื่นที่มีอยู่ได้ตามปกติ
        private static void SetText(bpac.Document doc, string objectName, string value)
        {
            var obj = doc.GetObject(objectName);
            if (obj != null) obj.Text = value ?? "";
        }
    }
}
