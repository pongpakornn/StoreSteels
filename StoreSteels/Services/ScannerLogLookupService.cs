namespace StoreSteels.Services
{
    // TODO: ยังไม่ทราบชื่อตาราง/คอลัมน์ของ Log Scanner ฝั่ง Stock DB ที่ใช้เทียบว่ารายการไหน
    // ถูกเบิกไปแล้ว (key คู่ TicketNo + ItemNo) กรุณาแจ้งชื่อตาราง/ฟิลด์ที่ถูกต้อง แล้วเขียน query
    // จริงแทนใน method นี้ - ระหว่างนี้คืนค่า false เสมอ (ถือว่ายังไม่เคยถูกเบิก) เพื่อไม่บล็อกการใช้งาน
    public interface IScannerLogLookupService
    {
        bool IsAlreadyScanned(string ticketNo, string itemNo);
    }

    public class ScannerLogLookupService : IScannerLogLookupService
    {
        public bool IsAlreadyScanned(string ticketNo, string itemNo)
        {
            // Placeholder - ยังไม่ผูกกับตาราง Log Scanner จริง
            return false;
        }
    }
}
