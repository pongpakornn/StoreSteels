using Microsoft.Data.SqlClient;
using StoreSteels.Models;
using StoreSteels.Core;
using System;
using System.Collections.Generic;
using System.Data;


namespace StoreSteels.Services
{
    public class PRService
    {
        private readonly string _connectionString = GlobalConfig.ConnStr;

        public List<PRModel> GetPRList(string searchText = "")
        {
            var list = new List<PRModel>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_GetPRList", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@SearchText", searchText ?? "");

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new PRModel
                            {
                                PR_NO = reader["PR_NO"].ToString(),
                                USR_ID = reader["USR_ID"].ToString(),
                                Requester = reader["REQUESTER"].ToString(), // จะได้ 'N/A' แทน NULL เพราะ ISNULL ใน View
                                PartCode = reader["PT_CODE"]?.ToString() ?? "N/A", // เพิ่มบรรทัดนี้เพื่อรับค่า Code
                                PartName = reader["PT_DESC"].ToString(),
                                QTY = reader["QTY_REQ"] != DBNull.Value ? Convert.ToInt32(reader["QTY_REQ"]) : 0,
                                Department = reader["REQ_DEPT"].ToString(),
                                Status = reader["PR_STAT"].ToString(),
                                PR_DATE = Convert.ToDateTime(reader["PR_DATE"]),
                                PR_REM = reader["PR_REM"]?.ToString()
                            });
                        }
                    }
                }
            }
            return list;
        }

        public string GetNextPRNo()
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                // 1. ดึงเลขปี ค.ศ. 2 หลักสุดท้าย (เช่น 2026 -> 26)
                string year = DateTime.Now.ToString("yy", System.Globalization.CultureInfo.InvariantCulture);
                string prefix = year + "PR"; // ผลลัพธ์: 26PR

                // 2. ค้นหาเลขล่าสุดที่ขึ้นต้นด้วย 26PR
                string sql = "SELECT TOP 1 PR_NO FROM TRN_PR_H WHERE PR_NO LIKE @Prefix + '%' ORDER BY PR_NO DESC";

                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Prefix", prefix);
                    var result = cmd.ExecuteScalar();

                    if (result == null || result == DBNull.Value)
                    {
                        // ถ้ายังไม่มีเลยในฐานข้อมูล เริ่มต้นที่ 00000001
                        return prefix + "00000001";
                    }
                    else
                    {
                        // ถ้ามีแล้ว (เช่น 26PR00000005) ให้ตัดเอาเลข 8 หลักสุดท้ายมาบวก 1
                        // เปลี่ยนจาก Substring(4) เป็นการเอา 8 หลักสุดท้าย
                        string lastNo = result.ToString().Trim();
                        if (lastNo.Length >= 8)
                        {
                            string lastDigitStr = lastNo.Substring(lastNo.Length - 8); // เอา 8 หลักท้ายแน่นอน
                            if (int.TryParse(lastDigitStr, out int lastDigit))
                            {
                                return prefix + (lastDigit + 1).ToString("D8");
                            }
                        }
                        return prefix + "00000001"; // Fallback กรณี Parse ไม่ได้
                    }
                }
            }
        }

        // Approve

        public bool UpdatePRStatus(string prNo, string newStatus, string approverId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql;

                if (newStatus == "Rejected")
                {
                    // ลบตามลำดับ: ลบรายการสินค้า (D) ก่อน แล้วค่อยลบหัวเอกสาร (H)
                    sql = @"DELETE FROM TRN_PR_D WHERE PR_NO = @PrNo;
                    DELETE FROM TRN_PR_H WHERE PR_NO = @PrNo;";
                }
                else
                {
                    // ถ้า Approved หรือสถานะอื่น ให้ Update ปกติ
                    sql = @"UPDATE TRN_PR_H 
                    SET PR_STAT = @Status, 
                        APP_USR_ID = CASE WHEN @Status = 'Approved' THEN @AppUserId ELSE NULL END, 
                        APP_DATE = CASE WHEN @Status = 'Approved' THEN GETDATE() ELSE NULL END,
                        IS_EXPORT = CASE WHEN @Status = 'Approved' THEN 'Y' ELSE 'N' END
                    WHERE PR_NO = @PrNo";
                }

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Status", newStatus);
                    cmd.Parameters.AddWithValue("@AppUserId", approverId);
                    cmd.Parameters.AddWithValue("@PrNo", prNo);

                    int rows = cmd.ExecuteNonQuery();
                    // หมายเหตุ: การลบ 2 ตารางพร้อมกันแบบนี้ rows ที่ได้จะเป็นจำนวนแถวรวมที่ถูกลบ
                    return rows > 0;
                }
            }
        }

        public bool InsertPR(string prNo, string userId, string dept, string partDesc, int qty, string remark, string targetDept)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        // เปลี่ยน 'Waiting' เป็น @Status เพื่อให้ตรงกับ Parameters.Add ด้านล่าง
                        string sqlHeader = @"INSERT INTO TRN_PR_H (PR_NO, PR_DATE, USR_ID, REQ_DEPT, TRG_DEPT, PR_STAT, PR_REM) 
                                   VALUES (@PrNo, GETDATE(), @UserId, @Dept, @TrgDept, @Status, @Remark)";

                        string sqlDetail = @"INSERT INTO TRN_PR_D (PR_NO, PT_DESC, QTY_REQ) 
                                   VALUES (@PrNo, @PartDesc, @Qty)";

                        using (SqlCommand cmdH = new SqlCommand(sqlHeader, conn, trans))
                        {
                            cmdH.Parameters.AddWithValue("@PrNo", prNo);
                            cmdH.Parameters.AddWithValue("@UserId", userId);
                            cmdH.Parameters.AddWithValue("@Dept", dept);
                            cmdH.Parameters.AddWithValue("@TrgDept", targetDept ?? "");
                            cmdH.Parameters.AddWithValue("@Remark", remark ?? "");
                            cmdH.Parameters.AddWithValue("@Status", "Waiting"); // 
                            cmdH.ExecuteNonQuery();
                        }

                        using (SqlCommand cmdD = new SqlCommand(sqlDetail, conn, trans))
                        {
                            cmdD.Parameters.AddWithValue("@PrNo", prNo);
                            cmdD.Parameters.AddWithValue("@PartDesc", partDesc);
                            cmdD.Parameters.AddWithValue("@Qty", qty);
                            cmdD.ExecuteNonQuery();
                        }

                        trans.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Insert PR Error: {ex.Message}");
                        trans.Rollback();
                        return false;
                    }
                }
            }
        }

        // เพิ่มฟังก์ชันนี้ใน PRService.cs
        public List<string> GetProductSuggestions(string searchText)
        {
            var suggestions = new List<string>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                // ดึงเฉพาะชื่อสินค้าที่ Active และตรงกับที่พิมพ์
                string sql = "SELECT PT_DESC FROM MST_PART WHERE PT_DESC LIKE @Search + '%' AND IS_ACTIVE = 1";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Search", searchText);
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            suggestions.Add(reader["PT_DESC"].ToString());
                        }
                    }
                }
            }
            return suggestions;
        }

        // ตรวจสอบว่าสินค้ามีอยู่ใน MST_PART หรือไม่
        public bool IsProductExists(string productName)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string sql = "SELECT COUNT(1) FROM MST_PART WHERE PT_DESC = @Name AND IS_ACTIVE = 1";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Name", productName);
                    conn.Open();
                    int count = (int)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
        }

        public List<PRModel> GetPendingExportList()
        {
            var list = new List<PRModel>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                // แนะนำให้ใส่เงื่อนไขดักไว้อีกชั้นใน SQL เลยครับนนท์ เพื่อความชัวร์
                string sql = "SELECT * FROM v_PRReady WHERE IS_EXPORT = 'Y' ORDER BY PR_NO ASC";

                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new PRModel
                            {
                                PR_NO = reader["PR_NO"].ToString(),
                                // ... (แมพค่าอื่นๆ ตามเดิม)
                            });
                        }
                    }
                }
            }
            return list;
        }
        public bool UpdateAfterExport(string prNo, string userId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string sql = @"UPDATE TRN_PR_H 
                       SET IS_EXPORT = 'N', 
                           EXPORT_REMARK = @UserId, 
                           EXPORT_DATE = GETDATE() 
                       WHERE PR_NO = @PrNo";
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@PrNo", prNo);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

    }
}