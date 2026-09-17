using Dapper;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Data.SqlClient;
using StoreSteels.Core;
using StoreSteels.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace StoreSteels.Services
{
    public class ProductControlService
    {
        private readonly string _connectionString = GlobalConfig.ConnStr;

        // ดึงข้อมูลผ่าน Stored Procedure
        public List<ProductControlModel> GetInventoryForQR(string searchText)
        {
            var items = new List<ProductControlModel>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_GetPartForQR", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@SearchText", searchText ?? "");
                    conn.Open();
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            items.Add(new ProductControlModel
                            {
                                PartCode = rdr["PT_CODE"].ToString(),
                                PartName = rdr["PT_DESC"].ToString(),
                                PackSize = rdr["PT_PSZ"].ToString(),
                                Category = rdr["PT_CAT"].ToString(),
                                Location = rdr["PT_LOC"].ToString(),
                                QRCodeData = rdr["PT_QR_DISPLAY"].ToString(),
                                Max = rdr["QTY_MAX"].ToString(),
                                Min = rdr["QTY_MIN"].ToString(),
                                Stock = rdr["QTY_STK"].ToString(),

                                CustomerCode = rdr["PT_CUST"].ToString(),
                                ModelCode = rdr["PT_MODEL"].ToString(),
                                PartACode = rdr["PT_ACODE"].ToString(), // 🎯 ตัวระบุหลักไม่ซ้ำ
                                PartNo = rdr["PT_NO"].ToString(),

                                ImageFileName = rdr["PT_IMG"] != DBNull.Value ? rdr["PT_IMG"].ToString() : null,
                                IsShow = Convert.ToBoolean(rdr["IS_SHOW_MST"]),
                                IsActive = Convert.ToBoolean(rdr["IS_ACTIVE"])
                            });
                        }
                    }
                }
            }
            return items;
        }

        // 🎯 ✅ แก้ไขระบบ UPDATE: เพิ่มคอลัมน์ CustomerCode, ModelCode, PartNo ให้แก้ไขค่าลงฐานข้อมูลได้ครบถ้วน
        //public async Task<bool> UpdateExistingPartAsync(
        //    string oldACode,
        //    string newACode,
        //    string code,
        //    string name,
        //    int psz,
        //    string qrData,
        //    int max,
        //    int min,
        //    string category,
        //    string uid,
        //    string imageFileName,
        //    string customerCode, // 👈 เพิ่มพารามิเตอร์รองรับ 1
        //    string modelCode,    // 👈 เพิ่มพารามิเตอร์รองรับ 2
        //    string partNo        // 👈 เพิ่มพารามิเตอร์รองรับ 3
        //)
        //{
        //    using (SqlConnection conn = new SqlConnection(_connectionString))
        //    {
        //        await conn.OpenAsync();
        //        using (SqlTransaction trans = conn.BeginTransaction())
        //        {
        //            try
        //            {
        //                // 🎯 เพิ่มการ SET ค่าฟิลด์ PT_CUST, PT_MODEL, PT_NO ลงใน SQL Script
        //                string updateSql = @"UPDATE MST_PART SET 
        //                                     PT_ACODE = @NewACode,
        //                                     PT_CODE = @Code, 
        //                                     PT_DESC = @Name, 
        //                                     PT_PSZ = @Psz, 
        //                                     PT_QR = @QR,
        //                                     QTY_MAX = @Max, 
        //                                     QTY_MIN = @Min, 
        //                                     PT_CAT = @Cat,
        //                                     PT_IMG = @ImageFileName,
        //                                     PT_CUST = @CustomerCode,
        //                                     PT_MODEL = @ModelCode,
        //                                     PT_NO = @PartNo
        //                                     WHERE PT_ACODE = @OldACode";

        //                using (SqlCommand cmd = new SqlCommand(updateSql, conn, trans))
        //                {
        //                    cmd.Parameters.AddWithValue("@NewACode", newACode);
        //                    cmd.Parameters.AddWithValue("@Code", code ?? "");
        //                    cmd.Parameters.AddWithValue("@Name", name ?? "");
        //                    cmd.Parameters.AddWithValue("@Psz", psz);
        //                    cmd.Parameters.AddWithValue("@QR", qrData ?? "");
        //                    cmd.Parameters.AddWithValue("@Max", max);
        //                    cmd.Parameters.AddWithValue("@Min", min);
        //                    cmd.Parameters.AddWithValue("@Cat", category ?? "GENERAL");
        //                    cmd.Parameters.AddWithValue("@OldACode", oldACode);
        //                    cmd.Parameters.AddWithValue("@ImageFileName", (object)imageFileName ?? DBNull.Value);

        //                    // 👈 ผูก Parameter ข้อมูลกลุ่มใหม่เพิ่มเติม
        //                    cmd.Parameters.AddWithValue("@CustomerCode", (object)customerCode ?? DBNull.Value);
        //                    cmd.Parameters.AddWithValue("@ModelCode", (object)modelCode ?? DBNull.Value);
        //                    cmd.Parameters.AddWithValue("@PartNo", (object)partNo ?? DBNull.Value);

        //                    await cmd.ExecuteNonQueryAsync();
        //                }

        //                trans.Commit();
        //                return true;
        //            }
        //            catch (Exception ex)
        //            {
        //                trans.Rollback();
        //                System.Diagnostics.Debug.WriteLine($"Update Error: {ex.Message}");
        //                throw;
        //            }
        //        }
        //    }
        //}
        public async Task<bool> UpdateExistingPartAsync(
    string oldACode,
    string newACode,
    string code,
    string name,
    int psz,
    string qrData,
    string category,        // 👈 แก้ลำดับให้ถูกถ้าจำเป็น
    string uid,
    string imageFileName,
    string customerCode,
    string modelCode,
    string partNo
)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 🎯 SQL UPDATE ที่ตัด QTY_MAX และ QTY_MIN ออกไปแล้ว
                        string updateSql = @"UPDATE MST_PART SET 
                                     PT_ACODE = @NewACode,
                                     PT_CODE = @Code, 
                                     PT_DESC = @Name, 
                                     PT_PSZ = @Psz, 
                                     PT_QR = @QR,
                                     PT_CAT = @Cat,
                                     PT_IMG = @ImageFileName,
                                     PT_CUST = @CustomerCode,
                                     PT_MODEL = @ModelCode,
                                     PT_NO = @PartNo
                                     WHERE PT_ACODE = @OldACode";

                        using (SqlCommand cmd = new SqlCommand(updateSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@NewACode", newACode);
                            cmd.Parameters.AddWithValue("@Code", code ?? "");
                            cmd.Parameters.AddWithValue("@Name", name ?? "");
                            cmd.Parameters.AddWithValue("@Psz", psz);
                            cmd.Parameters.AddWithValue("@QR", qrData ?? "");
                            cmd.Parameters.AddWithValue("@Cat", category ?? "GENERAL");
                            cmd.Parameters.AddWithValue("@OldACode", oldACode);
                            cmd.Parameters.AddWithValue("@ImageFileName", (object)imageFileName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@CustomerCode", (object)customerCode ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ModelCode", (object)modelCode ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@PartNo", (object)partNo ?? DBNull.Value);

                            await cmd.ExecuteNonQueryAsync();
                        }

                        trans.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        trans.Rollback();
                        System.Diagnostics.Debug.WriteLine($"Update Error: {ex.Message}");
                        throw;
                    }
                }
            }
        }

        // เพิ่มข้อมูลใหม่ (INSERT)
        public async Task<bool> InsertNewPartAsync(string code, string name, int psz, string qrContent, int max, int min, string category, string imageFileName, string customerCode, string modelCode, string partACode, string partNo)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        string insertSql = @"INSERT INTO MST_PART (
                                        PT_CODE, PT_DESC, PT_PSZ, PT_QR, QTY_MAX, QTY_MIN, PT_CAT, 
                                        PT_LOC, QTY_STK, IS_ACTIVE, IS_SHOW_MST, PT_IMG,
                                        PT_CUST, PT_MODEL, PT_ACODE, PT_NO
                                     )
                                     VALUES (
                                        @Code, @Name, @Psz, @QR, @Max, @Min, @Cat, 
                                        'N/A', 0, 1, 1, @ImageFileName,
                                        @CustomerCode, @ModelCode, @PartACode, @PartNo
                                     )";

                        using (SqlCommand cmd = new SqlCommand(insertSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@Code", code ?? "");
                            cmd.Parameters.AddWithValue("@Name", name ?? "");
                            cmd.Parameters.AddWithValue("@Psz", psz);
                            cmd.Parameters.AddWithValue("@QR", qrContent ?? "");
                            cmd.Parameters.AddWithValue("@Max", max);
                            cmd.Parameters.AddWithValue("@Min", min);
                            cmd.Parameters.AddWithValue("@Cat", category ?? "GENERAL");
                            cmd.Parameters.AddWithValue("@ImageFileName", (object)imageFileName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@CustomerCode", (object)customerCode ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ModelCode", (object)modelCode ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@PartACode", partACode); // 🎯 ฟิลด์หลักห้ามเป็น Null
                            cmd.Parameters.AddWithValue("@PartNo", (object)partNo ?? DBNull.Value);

                            await cmd.ExecuteNonQueryAsync();
                        }

                        trans.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        trans.Rollback();
                        System.Diagnostics.Debug.WriteLine($"Insert Error: {ex.Message}");
                        return false;
                    }
                }
            }
        }

        // 🎯 อัปเดตการทำ QR Code โดยค้นหาผ่าน PartACode
        public bool UpdateQRCodeStatus(string partACode)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "UPDATE MST_PART SET LAST_GEN_QR = GETDATE() WHERE PT_ACODE = @PartACode";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@PartACode", partACode);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // 🎯 [แก้ไขจุดบั๊กหลัก] อัปเดต Show/Hide เฉพาะแถวโดยระบุเงื่อนไขด้วย PT_ACODE
        public bool UpdateShowStatus(string partACode, bool isShow, string uid)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 🎯 เปลี่ยนเงื่อนไขจาก PT_CODE เป็น PT_ACODE เพื่อให้อัปเดตตรงรายการเดียว ไม่เหมาหมดตาราง
                        string sql = "UPDATE MST_PART SET IS_SHOW_MST = @IsShow WHERE PT_ACODE = @PartACode";
                        using (SqlCommand cmd = new SqlCommand(sql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@IsShow", isShow ? 1 : 0);
                            cmd.Parameters.AddWithValue("@PartACode", partACode);
                            cmd.ExecuteNonQuery();
                        }
                        trans.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"UpdateShowStatus Error: {ex.Message}");
                        trans.Rollback();
                        return false;
                    }
                }
            }
        }

        // 🎯 ลบข้อมูลโดยอ้างอิงผ่าน PartACode
        public bool DeletePart(string partACode, string uid)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        string sql = "DELETE FROM MST_PART WHERE PT_ACODE = @PartACode";
                        using (SqlCommand cmd = new SqlCommand(sql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@PartACode", partACode);
                            cmd.ExecuteNonQuery();
                        }
                        trans.Commit();
                        return true;
                    }
                    catch { trans.Rollback(); return false; }
                }
            }
        }

        // 🎯 ตรวจสอบค่าซ้ำในระบบ เปลี่ยนมาเช็คที่ PT_ACODE
        public async Task<bool> CheckDuplicateCodeAsync(string partACode)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                string sql = "SELECT COUNT(1) FROM MST_PART WHERE PT_ACODE = @PartACode";
                int count = await db.ExecuteScalarAsync<int>(sql, new { PartACode = partACode ?? "" });
                return count > 0;
            }
        }
    }
}