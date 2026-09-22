using Microsoft.Data.SqlClient;
using StoreSteels.Core;
using StoreSteels.Models;
using System;
using System.Collections.Generic;
using System.Data;

namespace StoreSteels.Services
{
    public class ScanService
    {
        private readonly string _connectionString = GlobalConfig.ConnStr;

        public ScanItemModel GetPartByScan(string barcode)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_GetPartByScan", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@BarcodeInput", barcode.Trim());

                    conn.Open();
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            // schema ใหม่ตัด PT_ACODE/PT_NO ออกจาก MST_PART แล้ว - PT_CODE เป็นตัวระบุหลัก
                            // ตัวเดียว ใส่ PartACode = PartCode ไว้เพื่อความเข้ากันได้กับหน้าจอเดิมที่ยัง
                            // ผูก binding กับ PartACode อยู่
                            string code = rdr["PT_CODE"].ToString();
                            return new ScanItemModel
                            {
                                PartId = rdr["PT_ID"] != DBNull.Value ? Convert.ToInt32(rdr["PT_ID"]) : 0,
                                PartCode = code,
                                PartName = rdr["PT_DESC"].ToString(),
                                PartNo = string.Empty,
                                PartACode = code,
                                Qty = rdr["PT_PSZ"] != DBNull.Value ? Convert.ToInt32(rdr["PT_PSZ"]) : 1,
                                UpdateTime = DateTime.Now
                            };
                        }
                    }
                }
            }
            return null;
        }

        public List<ScanItemModel> GetTodayTransactions()
        {
            var list = new List<ScanItemModel>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                // schema ใหม่: MST_PART ไม่มี PT_NO แล้ว - ตัดออกจาก SELECT (TRN_SCAN.PT_ACODE เป็นคอลัมน์
                // ของตัวเองในตาราง log ไม่เกี่ยวกับ MST_PART.PT_ACODE ที่ถูกลบ เลยยังอ่านได้ตามเดิม)
                string sql = @"SELECT
                                    ISNULL(m.PT_CODE, '') AS PT_CODE,
                                    ISNULL(m.PT_DESC, 'Unknown Part') AS PT_DESC,
                                    ISNULL(t.PT_ACODE, '') AS PT_ACODE,
                                    t.PT_ID,
                                    t.TX_QTY,
                                    t.TX_DATE,
                                    t.TX_TYPE
                               FROM TRN_SCAN t
                               LEFT JOIN MST_PART m ON t.PT_ID = m.PT_ID
                               WHERE CAST(t.TX_DATE AS DATE) = CAST(GETDATE() AS DATE)
                               ORDER BY t.TX_DATE ASC";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    conn.Open();
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            list.Add(new ScanItemModel
                            {
                                PartId = rdr["PT_ID"] != DBNull.Value ? Convert.ToInt32(rdr["PT_ID"]) : 0,
                                PartCode = rdr["PT_CODE"].ToString(),
                                PartName = rdr["PT_DESC"].ToString(),
                                PartACode = rdr["PT_ACODE"].ToString(),
                                PartNo = string.Empty,
                                Qty = Convert.ToInt32(rdr["TX_QTY"]),
                                UpdateTime = Convert.ToDateTime(rdr["TX_DATE"]),
                                Status = rdr["TX_TYPE"].ToString()
                            });
                        }
                    }
                }
            }
            return list;
        }

        // ==========================================
        // 📥 ขาเข้า: UpdateStock
        // ==========================================
        // ✅ เพิ่มพารามิเตอร์ refNo = บาร์โค้ดดิบ "ทั้งชุด" ที่แสกนเนอร์ยิงเข้ามา เก็บลง REF_NO
        // schema ใหม่: MST_PART เหลือ QTY_STKB ตัวเดียว (ไม่มี QTY_STK แยกกล่อง/ชิ้นอีกต่อไป) - บวก/ลบ
        // ตรงๆ ด้วยจำนวนที่สแกนเข้ามาจริง (qty) แทนการ +1 กล่องแบบเดิม
        // txType: ปกติ "IN" (ค่า default คงพฤติกรรมเดิม) ใช้ "RETURN" สำหรับกรณีรับคืนเหล็กเหลือจากการผลิต
        // เพื่อแยกสถานะออกจากการรับเข้าปกติใน TRN_SCAN (คอลัมน์ TX_TYPE เป็น varchar(20) รองรับได้สบาย)
        public bool UpdateStock(int ptId, string partCode, string partACode, int qty, string userId, string refNo, string txType = "IN")
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                SqlTransaction trans = conn.BeginTransaction();

                try
                {
                    string updateSql = @"UPDATE MST_PART
                                 SET QTY_STKB = ISNULL(QTY_STKB, 0) + @Qty
                                 WHERE PT_ID = @PtId";

                    // ✅ เพิ่มคอลัมน์ REF_NO
                    string insertLogSql = @"INSERT INTO TRN_SCAN (USR_ID, TX_QTY, TX_TYPE, TX_DATE, PT_ID, PT_ACODE, REF_NO)
                                    VALUES (@UserId, @Qty, @TxType, GETDATE(), @PtId, @PtACode, @RefNo)";

                    using (SqlCommand cmdUpdate = new SqlCommand(updateSql, conn, trans))
                    {
                        cmdUpdate.Parameters.AddWithValue("@Qty", qty);
                        cmdUpdate.Parameters.AddWithValue("@PtId", ptId);
                        cmdUpdate.ExecuteNonQuery();
                    }

                    using (SqlCommand cmdLog = new SqlCommand(insertLogSql, conn, trans))
                    {
                        cmdLog.Parameters.AddWithValue("@UserId", userId);
                        cmdLog.Parameters.AddWithValue("@Qty", qty);
                        cmdLog.Parameters.AddWithValue("@TxType", string.IsNullOrWhiteSpace(txType) ? "IN" : txType);
                        cmdLog.Parameters.AddWithValue("@PtId", ptId);
                        cmdLog.Parameters.AddWithValue("@PtACode", string.IsNullOrWhiteSpace(partACode) ? DBNull.Value : (object)partACode.Trim());

                        // 🛡️ กัน Truncate Error เผื่อบาร์โค้ดยาวเกินขนาดคอลัมน์ REF_NO ที่ตั้งไว้
                        string safeRefNo = string.IsNullOrWhiteSpace(refNo)
                            ? null
                            : (refNo.Length > 100 ? refNo.Substring(0, 100) : refNo);
                        cmdLog.Parameters.AddWithValue("@RefNo", (object)safeRefNo ?? DBNull.Value);

                        cmdLog.ExecuteNonQuery();
                    }

                    trans.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"UpdateStock Error: {ex.Message}");
                    trans.Rollback();
                    return false;
                }
            }
        }

        // ==========================================
        // 📤 ขาออก: UpdateStockOut
        // ==========================================
        public bool UpdateStockOut(int ptId, string partCode, string partACode, int qty, string userId, string refNo)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        string checkSql = @"SELECT ISNULL(QTY_STKB, 0) FROM MST_PART WHERE PT_ID = @PtId";
                        int currentStock = 0;
                        using (SqlCommand cmdCheck = new SqlCommand(checkSql, conn, trans))
                        {
                            cmdCheck.Parameters.Add("@PtId", SqlDbType.Int).Value = ptId;
                            var res = cmdCheck.ExecuteScalar();
                            if (res != null) currentStock = Convert.ToInt32(res);
                        }

                        if (currentStock < qty)
                        {
                            trans.Rollback();
                            return false;
                        }

                        string updateSql = @"UPDATE MST_PART
                                     SET QTY_STKB = ISNULL(QTY_STKB, 0) - @Qty
                                     WHERE PT_ID = @PtId";

                        using (SqlCommand cmdUpdate = new SqlCommand(updateSql, conn, trans))
                        {
                            cmdUpdate.Parameters.Add("@Qty", SqlDbType.Int).Value = qty;
                            cmdUpdate.Parameters.Add("@PtId", SqlDbType.Int).Value = ptId;
                            cmdUpdate.ExecuteNonQuery();
                        }

                        // ✅ เพิ่มคอลัมน์ REF_NO
                        string insertLogSql = @"INSERT INTO TRN_SCAN (USR_ID, TX_QTY, TX_TYPE, TX_DATE, PT_ID, PT_ACODE, REF_NO)
                                        VALUES (@UserId, @Qty, 'OUT', GETDATE(), @PtId, @PtACode, @RefNo)";
                        using (SqlCommand cmdLog = new SqlCommand(insertLogSql, conn, trans))
                        {
                            cmdLog.Parameters.Add("@UserId", SqlDbType.NVarChar).Value = userId;
                            cmdLog.Parameters.Add("@Qty", SqlDbType.Int).Value = qty;
                            cmdLog.Parameters.Add("@PtId", SqlDbType.Int).Value = ptId;
                            cmdLog.Parameters.Add("@PtACode", SqlDbType.NVarChar).Value =
                                string.IsNullOrWhiteSpace(partACode) ? DBNull.Value : (object)partACode.Trim();

                            string safeRefNo = string.IsNullOrWhiteSpace(refNo)
                                ? null
                                : (refNo.Length > 100 ? refNo.Substring(0, 100) : refNo);
                            cmdLog.Parameters.Add("@RefNo", SqlDbType.VarChar, 100).Value = (object)safeRefNo ?? DBNull.Value;

                            cmdLog.ExecuteNonQuery();
                        }

                        trans.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"UpdateStockOut Error: {ex.Message}");
                        trans.Rollback();
                        return false;
                    }
                }
            }
        }

        public int GetInventoryBalance(int ptId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                // คิวรีดึงยอดคงเหลือปัจจุบันจาก Master Table ตรงๆ
                string sql = "SELECT ISNULL(QTY_STKB, 0) FROM MST_PART WHERE PT_ID = @PtId";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@PtId", ptId);
                    try
                    {
                        conn.Open();
                        var res = cmd.ExecuteScalar();
                        return res != null ? Convert.ToInt32(res) : 0;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"GetInventoryBalance Error: {ex.Message}");
                        return 0;
                    }
                }
            }
        }
    }
}
