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
                            return new ScanItemModel
                            {
                                PartId = rdr["PT_ID"] != DBNull.Value ? Convert.ToInt32(rdr["PT_ID"]) : 0,
                                PartCode = rdr["PT_CODE"].ToString(),
                                PartName = rdr["PT_DESC"].ToString(),
                                PartNo = rdr["PT_NO"] != DBNull.Value ? rdr["PT_NO"].ToString() : string.Empty,
                                PartACode = rdr["PT_ACODE"] != DBNull.Value ? rdr["PT_ACODE"].ToString() : string.Empty,
                                Qty = rdr["PT_PSZ"] != DBNull.Value ? Convert.ToInt32(rdr["PT_PSZ"]) : 1,
                                // PartImg = rdr["PT_IMG"] != DBNull.Value ? rdr["PT_IMG"].ToString() : string.Empty, -- ดึงค่าจาก SQL
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
                // ✅ แก้ไข SQL: ให้ดึง t.PT_ACODE จากตาราง Transaction ตรงๆ ตามที่นนท์เพิ่มคอลัมน์เข้ามาครับ
                string sql = @"SELECT 
                                    ISNULL(m.PT_CODE, '') AS PT_CODE, 
                                    ISNULL(m.PT_DESC, 'Unknown Part') AS PT_DESC, 
                                    ISNULL(t.PT_ACODE, '') AS PT_ACODE, -- ดึงจากตารางตักประวัติสแกน
                                    ISNULL(m.PT_NO, '') AS PT_NO,
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
                                PartNo = rdr["PT_NO"].ToString(),
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
        public bool UpdateStock(int ptId, string partCode, string partACode, int qty, string userId, string refNo)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                SqlTransaction trans = conn.BeginTransaction();

                try
                {
                    string updateSql = @"UPDATE MST_PART 
                                 SET QTY_STK = QTY_STK + @Qty, 
                                     QTY_STKB = QTY_STKB + 1 
                                 WHERE PT_ID = @PtId";

                    // ✅ เพิ่มคอลัมน์ REF_NO
                    string insertLogSql = @"INSERT INTO TRN_SCAN (USR_ID, TX_QTY, TX_TYPE, TX_DATE, PT_ID, PT_ACODE, REF_NO)
                                    VALUES (@UserId, @Qty, 'IN', GETDATE(), @PtId, @PtACode, @RefNo)";

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
                        string checkSql = @"SELECT ISNULL(QTY_STK, 0) FROM MST_PART WHERE PT_ID = @PtId";
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
                                     SET QTY_STK = QTY_STK - @Qty, 
                                         QTY_STKB = CASE WHEN QTY_STKB > 0 THEN QTY_STKB - 1 ELSE 0 END 
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
                string sql = "SELECT ISNULL(QTY_STK, 0) FROM MST_PART WHERE PT_ID = @PtId";
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

        //จริงๆต้องอัพเดทิงฟังก์ชันนี้ให้ดึงยอดคงคลังทั้งสองค่า (QTY_STK และ QTY_STKB) ด้วย
        // ==========================================
        // 🔍 ฟังก์ชันดึงยอดคงคลัง: ปรับเพื่อให้ได้ยอด "กล่อง" กลับมาด้วย
        // ==========================================
        // เพื่อความคลีน พี่แนะนำให้ปรับฟังก์ชันดึงยอดเดิม ให้ส่งกลับมาเป็น Object (Tuple) ทั้งยอดชิ้นและยอดกล่องครับ
        //public (int StockPcs, int StockBox) GetInventoryBalanceBoth(int ptId)
        //{
        //    using (SqlConnection conn = new SqlConnection(_connectionString))
        //    {
        //        // 🔥 คิวรีดึงมาทั้งคู่เลย QTY_STK และ QTY_STKB
        //        string sql = "SELECT ISNULL(QTY_STK, 0) AS QTY_STK, ISNULL(QTY_STKB, 0) AS QTY_STKB FROM MST_PART WHERE PT_ID = @PtId";
        //        using (SqlCommand cmd = new SqlCommand(sql, conn))
        //        {
        //            cmd.Parameters.AddWithValue("@PtId", ptId);
        //            try
        //            {
        //                conn.Open();
        //                using (SqlDataReader rdr = cmd.ExecuteReader())
        //                {
        //                    if (rdr.Read())
        //                    {
        //                        return (Convert.ToInt32(rdr["QTY_STK"]), Convert.ToInt32(rdr["QTY_STKB"]));
        //                    }
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                System.Diagnostics.Debug.WriteLine($"GetInventoryBalanceBoth Error: {ex.Message}");
        //            }
        //        }
        //    }
        //    return (0, 0);
        //}
    }
}
