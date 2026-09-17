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
        private readonly string _erpConnStr = ErpConfig.ConnStr;
        private readonly IScannerLogLookupService _scannerLogLookup;

        public PackingCardErpService(IScannerLogLookupService scannerLogLookup = null)
        {
            _scannerLogLookup = scannerLogLookup ?? new ScannerLogLookupService();
        }

        public List<PackingCardModel> GetPendingPackingCards()
        {
            var list = new List<PackingCardModel>();

            // ⚠️ ไม่มีคอลัมน์ Group (FGDCODE) ยืนยันจากผู้ใช้ในรอบล่าสุด แต่หน้าจอต้องใช้จัดกลุ่มคลัง
            // จึงดึง FGDCODE มาด้วยตามสเปกเดิม - ถ้าคอลัมน์นี้ไม่มีจริงในตาราง ให้ลบบรรทัด src.FGDCODE ออก
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
                    while (rdr.Read())
                    {
                        list.Add(new PackingCardModel
                        {
                            TicketNo = rdr["TicketNo"] == DBNull.Value ? "" : rdr["TicketNo"].ToString().Trim(),
                            ItemNo = rdr["ItemNo"] == DBNull.Value ? "" : rdr["ItemNo"].ToString().Trim(),
                            GroupCode = rdr["GroupCode"] == DBNull.Value ? "" : rdr["GroupCode"].ToString().Trim(),
                            LotNo = rdr["LotNo"] == DBNull.Value ? "" : rdr["LotNo"].ToString().Trim(),
                            MaterialCode = rdr["MaterialCode"] == DBNull.Value ? "" : rdr["MaterialCode"].ToString().Trim(),
                            WorkOrder = rdr["WorkOrder"] == DBNull.Value ? "" : rdr["WorkOrder"].ToString().Trim(),
                            TicketDate = rdr["TicketDate"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(rdr["TicketDate"]),
                            JobName = rdr["JobName"] == DBNull.Value ? "" : rdr["JobName"].ToString().Trim(),
                            Qty = rdr["Qty"] == DBNull.Value ? 0 : Convert.ToDecimal(rdr["Qty"])
                        });
                    }
                }
            }

            // กรองรายการที่ถูกเบิกไปแล้วออก โดยเทียบ key TicketNo + ItemNo กับ Log ของ Scanner ฝั่ง Stock
            return list.Where(x => !_scannerLogLookup.IsAlreadyScanned(x.TicketNo, x.ItemNo)).ToList();
        }
    }
}
