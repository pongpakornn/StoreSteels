
// LogService.cs : บันทึกการทำงานของผู้ใช้งานในระบบ เก็บรายละเอียดทุกการเคลื่อนไหว

using Microsoft.Data.SqlClient;
using StoreSteels.Core;
using System;
using System.Threading.Tasks;

namespace StoreSteels.Services
{
    public static class LogService
    {
        // --- [ แก้ไขเฉพาะจุดใน LogService.cs ] ---

        public static void WriteLog(string userId, string action, string detail, string refCode)
        {
            Task.Run(() =>
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
                    {
                        string sql = @"INSERT INTO SYS_LOGS (USR_ID, ACT_TYPE, LOG_DESC, LOG_REF, LOG_DATE) 
                               VALUES (@user, @action, @detail, @ref, GETDATE())";

                        // ใช้ using สำหรับ SqlCommand เพื่อคืนทรัพยากรให้ระบบ
                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@user", (object)userId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@action", (object)action ?? "");
                            cmd.Parameters.AddWithValue("@detail", (object)detail ?? "");
                            cmd.Parameters.AddWithValue("@ref", (object)refCode ?? "");

                            conn.Open();
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    // เขียนลง Debug เพื่อให้เห็นตอนพัฒนา แต่ไม่ให้โปรแกรมหลักค้าง
                    System.Diagnostics.Debug.WriteLine("Log Error: " + ex.Message);
                }
            });
        }

        // เพิ่มตัวนี้เข้าไปครับ แยก Parameter ให้ชัดเจน
        public static void WriteUpdateStkLog(string userId, string part, string max, string min, string remark)
        {
            // ครอบด้วย Task.Run เพื่อให้มันทำงานข้างหลัง ไม่ไปขวางหน้าจอ
            Task.Run(() =>
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
                    {
                        string sql = @"INSERT INTO STK_UPDATE_LOG (USR_ID, LOG_PART, LOG_MAX, LOG_MIN, LOG_REMARK) 
                             VALUES (@uid, @part, @max, @min, @remark)";

                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@uid", userId);
                            cmd.Parameters.AddWithValue("@part", part);

                            int.TryParse(max, out int maxVal);
                            int.TryParse(min, out int minVal);

                            cmd.Parameters.AddWithValue("@max", maxVal);
                            cmd.Parameters.AddWithValue("@min", minVal);
                            cmd.Parameters.AddWithValue("@remark", (object)remark ?? DBNull.Value);

                            conn.Open();
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Log Error: {ex.Message}");
                }
            });
        }

        // เพิ่มใน LogService.cs
        public static void WriteQRLog(string userId, string partCode, string actionType)
        {
            Task.Run(() =>
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
                    {
                        // สมมติว่ามีตาราง QR_LOG หรือใช้ SYS_LOGS เดิมก็ได้
                        string sql = @"INSERT INTO SYS_LOGS (USR_ID, ACT_TYPE, LOG_DESC, LOG_REF, LOG_DATE) 
                               VALUES (@uid, @act, @desc, @ref, GETDATE())";

                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@uid", userId);
                            cmd.Parameters.AddWithValue("@act", "QR_SYSTEM");
                            cmd.Parameters.AddWithValue("@desc", $"{actionType} for Part: {partCode}");
                            cmd.Parameters.AddWithValue("@ref", partCode);

                            conn.Open();
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex) { /* Handle error */ }
            });
        }

        public static void WritePRLog(string userId, string actionType, string description, string reference)
        {
            Task.Run(() =>
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
                    {
                        string sql = @"INSERT INTO SYS_LOGS (USR_ID, ACT_TYPE, LOG_DESC, LOG_REF, LOG_DATE) 
                               VALUES (@uid, @act, @desc, @ref, GETDATE())";

                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@uid", userId ?? (object)DBNull.Value);
                            // คุณนนท์อาจจะฟิกซ์ค่า @act เป็น "PR_SYSTEM" ไปเลยคล้ายๆ QR ก็ได้ครับ
                            cmd.Parameters.AddWithValue("@act", "PR_SYSTEM");
                            cmd.Parameters.AddWithValue("@desc", description);
                            cmd.Parameters.AddWithValue("@ref", reference ?? (object)DBNull.Value);

                            conn.Open();
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch { /* ปล่อยเงียบเพื่อไม่ให้กระทบ UI Main Thread */ }
            });
        }

        // ✅ เพิ่มสำหรับงานแสกนโดยเฉพาะ (Scan In / Scan Out)
        public static void WriteScanLog(string userId, string actionType, string partCode, string partName, int qty)
        {
            Task.Run(() =>
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
                    {
                        // ใช้ตาราง SYS_LOGS เป็นหลักเพื่อให้ตรวจสอบง่ายที่เดียว
                        string sql = @"INSERT INTO SYS_LOGS (USR_ID, ACT_TYPE, LOG_DESC, LOG_REF, LOG_DATE) 
                                       VALUES (@uid, @act, @desc, @ref, GETDATE())";

                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            // actionType รับค่าเป็น "SCAN_IN" หรือ "SCAN_OUT"
                            string description = $"[{actionType}] PD: {partName} | QTY: {qty}";

                            cmd.Parameters.AddWithValue("@uid", (object)userId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@act", actionType);
                            cmd.Parameters.AddWithValue("@desc", description);
                            cmd.Parameters.AddWithValue("@ref", partCode ?? "");

                            conn.Open();
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Scan Log Error: {ex.Message}");
                }
            });
        }

    }
}