using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient; // 🎯 แก้ไข 1: เปลี่ยนมาใช้ Microsoft.Data.SqlClient ตามตัวอื่นๆ ในโปรเจกต์
using StoreSteels.Core;            // 🎯 แก้ไข 2: เรียกใช้ Core เพื่อดึง GlobalConfig.ConnStr มาใช้
using StoreSteels.Models;

namespace StoreSteels.Services
{
    public class MaxMinCalService
    {
        /// <summary>
        /// ดึงข้อมูลพาร์ทมาสเตอร์และคอนฟิกจำนวนวันปลอดภัยจาก View จริงบน SQL Server
        /// </summary>
        public List<PartMasterModel> GetPartMasterList(string keyword = "")
        {
            var list = new List<PartMasterModel>();
            string query = @"
            SELECT PT_ID, CustomerName, PartACode, PartCode, PartNo, PartName, PackSize, DayMin, DayMax, QtyMax, QtyMin
            FROM dbo.VW_PART_MAXMIN_MASTER
            WHERE (@Keyword = '' 
                   OR PartACode LIKE '%' + @Keyword + '%' 
                   OR PartCode LIKE '%' + @Keyword + '%' 
                   OR PartNo LIKE '%' + @Keyword + '%' 
                   OR PartName LIKE '%' + @Keyword + '%' 
                   OR CustomerName LIKE '%' + @Keyword + '%')
            ORDER BY CustomerName, PartACode;";

            using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Keyword", string.IsNullOrWhiteSpace(keyword) ? "" : keyword);
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new PartMasterModel
                        {
                            PT_ID = Convert.ToInt32(reader["PT_ID"]),
                            CustomerName = reader["CustomerName"]?.ToString() ?? "",
                            PartACode = reader["PartACode"]?.ToString() ?? "",
                            PartCode = reader["PartCode"]?.ToString() ?? "",
                            PartNo = reader["PartNo"]?.ToString() ?? "",
                            PartName = reader["PartName"]?.ToString() ?? "",
                            PackSize = reader["PackSize"] != DBNull.Value ? Convert.ToInt32(reader["PackSize"]) : 1,
                            DayMin = reader["DayMin"] != DBNull.Value ? Convert.ToInt32(reader["DayMin"]) : 1,
                            DayMax = reader["DayMax"] != DBNull.Value ? Convert.ToInt32(reader["DayMax"]) : 3,
                            QtyMax = reader["QtyMax"] != DBNull.Value ? Convert.ToInt32(reader["QtyMax"]) : 0,
                            QtyMin = reader["QtyMin"] != DBNull.Value ? Convert.ToInt32(reader["QtyMin"]) : 0,


                        });
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// ใช้ MERGE (Upsert) ในการตรวจสอบคีย์ซ้ำ (CUST_CODE, PT_ID) 
        /// ถ้ามีอยู่แล้วจะ UPDATE ถ้าไม่มีจะทำการ INSERT ลงตาราง MST_CALC_CONFIG พร้อมบันทึก PT_ACODE
        /// </summary>
        public bool SaveOrUpdateCalcConfig(string customerCode, int ptId, string partACode, int dayMin, int dayMax, string userEmpId)
        {
            // 🎯 อัปเดต Query: เพิ่มคอลัมน์ PT_ACODE เข้าไปในกระบวนการ Insert ของตารางจริง
            string query = @"
            MERGE INTO MST_CALC_CONFIG AS Target
            USING (SELECT @CustCode AS CUST_CODE, @PtId AS PT_ID) AS Source
            ON (Target.CUST_CODE = Source.CUST_CODE AND Target.PT_ID = Source.PT_ID)
            WHEN MATCHED THEN
                UPDATE SET Target.DAY_MIN = @DayMin, 
                           Target.DAY_MAX = @DayMax, 
                           Target.UPDATE_BY = @UpdateBy, 
                           Target.UPDATE_DATE = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (CUST_CODE, PT_ID, PT_ACODE, DAY_MIN, DAY_MAX, UPDATE_BY, UPDATE_DATE)
                VALUES (@CustCode, @PtId, @PartACode, @DayMin, @DayMax, @UpdateBy, GETDATE());";

            using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.Add("@CustCode", SqlDbType.NVarChar, 100).Value = customerCode ?? (object)DBNull.Value;
                cmd.Parameters.Add("@PtId", SqlDbType.Int).Value = ptId;
                cmd.Parameters.Add("@PartACode", SqlDbType.NVarChar, 100).Value = partACode ?? (object)DBNull.Value; // 🎯 พารามิเตอร์ใหม่ที่เพิ่มเข้ามา
                cmd.Parameters.Add("@DayMin", SqlDbType.Int).Value = dayMin;
                cmd.Parameters.Add("@DayMax", SqlDbType.Int).Value = dayMax;
                cmd.Parameters.Add("@UpdateBy", SqlDbType.VarChar, 20).Value = userEmpId ?? "SYSTEM";

                try
                {
                    conn.Open();
                    int rowsAffected = cmd.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
                catch (Exception ex)
                {
                    throw new Exception($"[Database Error] ไม่สามารถบันทึกเงื่อนไขลงตาราง MST_CALC_CONFIG ได้: {ex.Message}");
                }
            }
        }

        public void ProcessImportForecastOrder(
        List<ExcelForecastOrderModel> excelData,
        string userEmpId)
        {
            using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
            {
                conn.Open();

                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (var item in excelData)
                        {
                            //--------------------------------------------------
                            // STEP 1 INSERT STAGE
                            //--------------------------------------------------

                            string insertSql = @" INSERT INTO TRN_IMPORT_STAGE( CUST_CODE, PT_ACODE, FORECAST_QTY, ORDER_QTY, DELIVERY_QTY, WORK_DAYS, TARGET_DATE, GUID_RUN )
                             VALUES ( @CustCode, @PartACode, @Forecast, @Order, @Delivery, @WorkDay, GETDATE(), NEWID())";

                            using (SqlCommand cmd = new SqlCommand(insertSql, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@CustCode", item.Customer);
                                cmd.Parameters.AddWithValue("@PartACode", item.PartA ?? "");
                                cmd.Parameters.AddWithValue("@Forecast", item.Forecast);
                                cmd.Parameters.AddWithValue("@Order", item.Order);
                                cmd.Parameters.AddWithValue("@Delivery", item.Delivery);
                                cmd.Parameters.AddWithValue("@WorkDay", item.Workday);
                                cmd.ExecuteNonQuery();
                            }

                            //--------------------------------------------------
                            // STEP 2 ถ้ามี PartA ให้คำนวณ Max Min
                            //--------------------------------------------------

                            if (!string.IsNullOrWhiteSpace(item.PartA)
                                && item.Order > 0
                                && item.Workday > 0)
                            {
                                string calcSql = @"

                                DECLARE @PT_ID INT;
                                DECLARE @PACKSIZE INT;
                                DECLARE @DAY_MIN INT = 1;
                                DECLARE @DAY_MAX INT = 3;

                                SELECT
                                    @PT_ID = PT_ID,
                                    @PACKSIZE = ISNULL(PT_PSZ,1)
                                FROM MST_PART
                                WHERE PT_ACODE = @PartACode;

                                SELECT
                                    @DAY_MIN = ISNULL(DAY_MIN,1),
                                    @DAY_MAX = ISNULL(DAY_MAX,3)
                                FROM MST_CALC_CONFIG
                                WHERE PT_ID = @PT_ID
                                  AND CUST_CODE = @CustCode;

                                IF @PT_ID IS NOT NULL
                                BEGIN

                                DECLARE @MINBOX INT;
                                DECLARE @MAXBOX INT;
                                SET @MINBOX =
                                CEILING((@OrderQty / CAST(@WorkDay AS DECIMAL(18,4)))/CAST(@PACKSIZE AS DECIMAL(18,4)));
                                SET @MAXBOX =
                                @MINBOX * @DAY_MAX;
                                UPDATE MST_PART
                                SET
                                    QTY_MIN = @MINBOX,
                                    QTY_MAX = @MAXBOX
                                WHERE PT_ID = @PT_ID;
                                END";

                                using (SqlCommand cmd = new SqlCommand(calcSql, conn, trans))
                                {
                                    cmd.Parameters.AddWithValue("@CustCode", item.Customer);
                                    cmd.Parameters.AddWithValue("@PartACode", item.PartA);
                                    cmd.Parameters.AddWithValue("@OrderQty", item.Order);
                                    cmd.Parameters.AddWithValue("@WorkDay", item.Workday);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }

                        trans.Commit();
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }


        public void ProcessImportMaxMin(List<ExcelMaxMinModel> excelData, string userEmpId)
        {
            string sqlQuery = @"
                DECLARE @RealPtId INT;
                DECLARE @PartACode NVARCHAR(100);

                SELECT TOP 1 @RealPtId = PT_ID, @PartACode = PartACode 
                FROM dbo.VW_PART_MAXMIN_MASTER 
                WHERE PartNo = @PartNo AND CustomerName = @CustCode;

                IF @RealPtId IS NOT NULL
                BEGIN
                    MERGE INTO MST_CALC_CONFIG AS Target
                    USING (SELECT @CustCode AS CUST_CODE, @RealPtId AS PT_ID) AS Source
                    ON (Target.CUST_CODE = Source.CUST_CODE AND Target.PT_ID = Source.PT_ID)
                    WHEN MATCHED THEN
                        UPDATE SET 
                            Target.DAY_MIN = @DayMin, 
                            Target.DAY_MAX = @DayMax, 
                            Target.UPDATE_BY = @UpdateBy, 
                            Target.UPDATE_DATE = GETDATE()  
                    WHEN NOT MATCHED THEN
                        INSERT (CUST_CODE, PT_ID, PT_ACODE, DAY_MIN, DAY_MAX, UPDATE_BY, UPDATE_DATE)
                        VALUES (@CustCode, @RealPtId, @PartACode, @DayMin, @DayMax, @UpdateBy, GETDATE());
                END;";

            using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (var item in excelData)
                        {
                            using (SqlCommand cmd = new SqlCommand(sqlQuery, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@CustCode", item.Customer);
                                cmd.Parameters.AddWithValue("@PartNo", item.PartNo);
                                cmd.Parameters.AddWithValue("@DayMax", item.DayMax);
                                cmd.Parameters.AddWithValue("@DayMin", item.DayMin);
                                cmd.Parameters.AddWithValue("@UpdateBy", userEmpId ?? "SYSTEM_EXCEL");
                                cmd.ExecuteNonQuery();
                            }
                        }
                        trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        trans.Rollback();
                        throw new Exception($"เกิดข้อผิดพลาดระหว่างบันทึกค่าเทมเพลต Max Min: {ex.Message}");
                    }
                }
            }
        }

    }
}