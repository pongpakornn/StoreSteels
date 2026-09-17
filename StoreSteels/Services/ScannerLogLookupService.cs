using Microsoft.Data.SqlClient;
using StoreSteels.Core;
using System;

namespace StoreSteels.Services
{
    public interface IScannerLogLookupService
    {
        bool IsAlreadyScanned(string ticketNo, string lotNo);
    }

    // เทียบกับ TRN_SCAN.REF_NO ของระบบ MULTI-SCAN (IN)/(OUT) เดิม (ScanInView/ScanOutView + ScanService)
    // REF_NO เก็บบาร์โค้ด/ข้อความ QR ดิบ "ทั้งชุด" ที่แสกนเนอร์ยิงเข้ามาตอนสแกน ซึ่งก็คือ QR ที่พิมพ์บน
    // Packing Card นี้เอง (ขึ้นต้นด้วย TicketNo และมี LotNo อยู่ในข้อความ) จึงเช็คว่าเคยถูกสแกนไปแล้วหรือยัง
    // ด้วยการหา REF_NO ที่มีทั้ง TicketNo และ LotNo อยู่ในตัวมัน
    public class ScannerLogLookupService : IScannerLogLookupService
    {
        private readonly string _connStr = GlobalConfig.ConnStr;

        public bool IsAlreadyScanned(string ticketNo, string lotNo)
        {
            if (string.IsNullOrWhiteSpace(ticketNo)) return false;

            const string sql = @"SELECT COUNT(1) FROM TRN_SCAN
                                  WHERE REF_NO LIKE '%' + @TicketNo + '%'
                                    AND (@LotNo = '' OR REF_NO LIKE '%' + @LotNo + '%')";

            using (var conn = new SqlConnection(_connStr))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@TicketNo", ticketNo.Trim());
                cmd.Parameters.AddWithValue("@LotNo", (lotNo ?? "").Trim());

                conn.Open();
                int count = Convert.ToInt32(cmd.ExecuteScalar());
                return count > 0;
            }
        }
    }
}
