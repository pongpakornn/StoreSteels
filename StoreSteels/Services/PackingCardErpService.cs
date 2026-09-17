using Microsoft.Data.SqlClient;
using StoreSteels.Core;
using StoreSteels.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace StoreSteels.Services
{
    // อ่านข้อมูลจาก ERP (CHR) โดยตรงแบบ Read-only เพื่อเอามาแสดง/ปริ้น Packing Card เฉยๆ
    // ไม่มีการเขียน/แก้ไขข้อมูลกลับเข้า ERP หรือสร้างตาราง queue ใดๆ ฝั่ง Stock DB
    public class PackingCardErpService
    {
        private readonly string _erpConnStr = GlobalConfig.ErpConnStr;
        private readonly IScannerLogLookupService _scannerLogLookup;

        public PackingCardErpService(IScannerLogLookupService scannerLogLookup = null)
        {
            _scannerLogLookup = scannerLogLookup ?? new ScannerLogLookupService();
        }

        public List<PackingCardModel> GetPendingPackingCards()
        {
            var list = new List<PackingCardModel>();

            // FGDCODE = คลัง (Group) ใช้จัดกลุ่มแถวในตาราง
            const string sql = @"
                SELECT
                    src.FTRNNO       AS TicketNo,
                    src.FITEMNO      AS ItemNo,
                    src.FGDCODE      AS GroupCode,
                    src.FLOTNO       AS LotNo,
                    src.FPDCODE      AS MaterialCode,
                    src.FWONO        AS WorkOrder,
                    src.FMDATE       AS TicketDate,
                    src.FREMARK      AS JobName,
                    src.FQTY         AS Qty
                FROM dbo.SD11ICTR src
                WHERE src.FTRNYEAR = YEAR(GETDATE())
                  AND src.FMOVECODE = 42
                  AND src.FMDATE >= DATEADD(DAY, -1, CAST(GETDATE() AS DATE))
                  AND src.FPDCODE NOT LIKE 'B%'
                ORDER BY src.FGDCODE, src.FMDATE DESC";

            using (var conn = new SqlConnection(_erpConnStr))
            using (var cmd = new SqlCommand(sql, conn))
            {
                conn.Open();
                using (SqlDataReader rdr = cmd.ExecuteReader(CommandBehavior.SequentialAccess))
                {
                    int ordTicketNo = rdr.GetOrdinal("TicketNo");
                    int ordItemNo = rdr.GetOrdinal("ItemNo");
                    int ordGroupCode = rdr.GetOrdinal("GroupCode");
                    int ordLotNo = rdr.GetOrdinal("LotNo");
                    int ordMaterialCode = rdr.GetOrdinal("MaterialCode");
                    int ordWorkOrder = rdr.GetOrdinal("WorkOrder");
                    int ordTicketDate = rdr.GetOrdinal("TicketDate");
                    int ordJobName = rdr.GetOrdinal("JobName");
                    int ordQty = rdr.GetOrdinal("Qty");

                    // ภายใต้ SequentialAccess ต้องอ่านแต่ละคอลัมน์ "ครั้งเดียว" และเรียงตาม ordinal จากน้อยไปมาก
                    // (เช็ค IsDBNull ก่อน ค่อยอ่านค่า - ห้ามอ่านคอลัมน์เดิมซ้ำสองครั้งแบบเดิมที่ใช้ rdr["Col"] ในทั้งเงื่อนไขและค่า)
                    while (rdr.Read())
                    {
                        list.Add(new PackingCardModel
                        {
                            TicketNo = rdr.IsDBNull(ordTicketNo) ? "" : rdr.GetString(ordTicketNo).Trim(),
                            ItemNo = rdr.IsDBNull(ordItemNo) ? "" : rdr.GetString(ordItemNo).Trim(),
                            GroupCode = rdr.IsDBNull(ordGroupCode) ? "" : rdr.GetString(ordGroupCode).Trim(),
                            LotNo = rdr.IsDBNull(ordLotNo) ? "" : rdr.GetString(ordLotNo).Trim(),
                            MaterialCode = rdr.IsDBNull(ordMaterialCode) ? "" : rdr.GetString(ordMaterialCode).Trim(),
                            WorkOrder = rdr.IsDBNull(ordWorkOrder) ? "" : rdr.GetString(ordWorkOrder).Trim(),
                            TicketDate = rdr.IsDBNull(ordTicketDate) ? DateTime.MinValue : rdr.GetDateTime(ordTicketDate),
                            JobName = rdr.IsDBNull(ordJobName) ? "" : rdr.GetString(ordJobName).Trim(),
                            Qty = rdr.IsDBNull(ordQty) ? 0 : rdr.GetDecimal(ordQty)
                        });
                    }
                }
            }

            // กรองรายการที่ถูกสแกนไปแล้วออก โดยเทียบกับ TRN_SCAN.REF_NO ของระบบ MULTI-SCAN (IN/OUT) เดิม
            return list.Where(x => !_scannerLogLookup.IsAlreadyScanned(x.TicketNo, x.LotNo)).ToList();
        }
    }
}
