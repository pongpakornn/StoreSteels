using Microsoft.Data.SqlClient;
using StoreSteels.Core;
using StoreSteels.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StoreSteels.Services
{
    public class StoreProductService
    {
        public List<StoreProductModel> GetProducts(
            string searchKeyword = "",
            string customer = "",
            string filterType = "",
            int skip = 0,
            int take = 300)
        {
            var list = new List<StoreProductModel>();

            using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
            {
                StringBuilder sql = new StringBuilder(@"
                SELECT *
                FROM VW_StoreMonitoring
                WHERE 1 = 1
                ");

                if (!string.IsNullOrEmpty(searchKeyword))
                    sql.Append("AND (PartACode LIKE @key OR PartName LIKE @key OR PartNo LIKE @key) ");

                if (!string.IsNullOrEmpty(customer))
                    sql.Append(" AND CustomerCode = @customer ");

                if (filterType == "OVER_MAX")
                {
                    sql.Append(@"
                    AND StockStatus IN ('OVER_MAX', 'NORMAL_GOOD')
                    AND ISNULL([Max], 0) > 0
                ");
                }
                else if (filterType == "UNDER_MIN")
                {
                    sql.Append(@"
                    AND StockStatus IN ('UNDER_MIN', 'OUT_OF_STOCK')
                    AND ISNULL([Min], 0) > 0
                ");
                }

                sql.Append("ORDER BY PartACode OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY");

                SqlCommand cmd = new SqlCommand(sql.ToString(), conn);

                if (!string.IsNullOrEmpty(searchKeyword))
                    cmd.Parameters.AddWithValue("@key", "%" + searchKeyword + "%");

                if (!string.IsNullOrEmpty(customer))
                    cmd.Parameters.AddWithValue("@customer", customer);

                cmd.Parameters.AddWithValue("@Skip", skip);
                cmd.Parameters.AddWithValue("@Take", take);

                conn.Open();

                using (SqlDataReader rdr = cmd.ExecuteReader())
                {
                    int rowNumber = skip + 1;

                    while (rdr.Read())
                    {
                        list.Add(new StoreProductModel
                        {
                            ID = (rowNumber++).ToString(),
                            CustomerCode = rdr["CustomerCode"]?.ToString() ?? "",
                            ModelCode = rdr["ModelCode"]?.ToString() ?? "",
                            ImageFileName = rdr["ImageFileName"]?.ToString() ?? "",
                            PartCode = rdr["PartCode"]?.ToString() ?? "",
                            PartACode = rdr["PartACode"]?.ToString() ?? "",
                            PartNo = rdr["PartNo"]?.ToString() ?? "",
                            PartName = rdr["PartName"]?.ToString() ?? "",
                            PackSize = rdr["PackSize"]?.ToString() ?? "",
                            Max = rdr["Max"]?.ToString() ?? "",
                            Min = rdr["Min"]?.ToString() ?? "",
                            QtyStkb = rdr["QtyStkb"]?.ToString() ?? "",
                            Stock = rdr["Stock"]?.ToString() ?? "",
                            Remark = rdr["Remark"]?.ToString() ?? "",
                            Category = rdr["Category"]?.ToString() ?? "",

                            // 👑 ใช้ TryParse แทน Convert.ToInt32 กัน FormatException
                            //    กรณี LIT_STAT เป็น string หรือ null ใน DB
                            Priority = int.TryParse(rdr["Priority"]?.ToString(), out int pri) ? pri : 0,

                            // 👑 StockStatus เป็น string เสมอ ไม่ Parse เป็น int
                            StockStatus = rdr["StockStatus"]?.ToString() ?? "NORMAL"
                        });
                    }
                }
            }

            return list;
        }

        #region === [ Update Product Master ] ===

        public bool UpdateProductMaster(string partACode, string remark, int? max, int? min, double? qtyStkb, int? stock)
        {
            using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
            {
                StringBuilder sql = new StringBuilder("UPDATE MST_PART SET PT_REMARK = @remark");

                if (max.HasValue) sql.Append(", QTY_MAX = @max");
                if (min.HasValue) sql.Append(", QTY_MIN = @min");
                if (qtyStkb.HasValue) sql.Append(", QTY_STKB = @qtyStkb");
                if (stock.HasValue) sql.Append(", QTY_STK = @stock");

                sql.Append(" WHERE PT_ACODE = @code");

                SqlCommand cmd = new SqlCommand(sql.ToString(), conn);
                cmd.Parameters.AddWithValue("@remark", (object)remark ?? "");
                cmd.Parameters.AddWithValue("@code", partACode);

                if (max.HasValue) cmd.Parameters.AddWithValue("@max", max.Value);
                if (min.HasValue) cmd.Parameters.AddWithValue("@min", min.Value);
                if (qtyStkb.HasValue) cmd.Parameters.AddWithValue("@qtyStkb", qtyStkb.Value);
                if (stock.HasValue) cmd.Parameters.AddWithValue("@stock", stock.Value);

                conn.Open();
                cmd.ExecuteNonQuery();
                return true;
            }
        }

        #endregion

        #region === [ Update Remark ] ===

        public bool UpdateRemark(string partACode, string remark)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
                {
                    string sql = "UPDATE MST_PART SET PT_REMARK = @remark WHERE PT_ACODE = @code";
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@remark", (object)remark ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@code", partACode);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB Error in UpdateRemark: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region === [ Get Customers ] ===

        public List<string> GetCustomers()
        {
            var customers = new List<string> { "ALL CUSTOMERS" };
            using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
            {
                string sql = @"SELECT DISTINCT PT_CUST 
                       FROM MST_PART 
                       WHERE IS_ACTIVE = 1 
                         AND IS_SHOW_MST = 1 
                         AND PT_CUST IS NOT NULL 
                         AND PT_CUST <> '' 
                       ORDER BY PT_CUST";

                SqlCommand cmd = new SqlCommand(sql, conn);
                conn.Open();
                using (SqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                        customers.Add(rdr["PT_CUST"].ToString());
                }
            }
            return customers;
        }

        #endregion

        #region === [ Real-Time Stock Updates ] ===

        public List<StoreProductModel> GetMinimalStockUpdates(List<string> partCodes)
        {
            var list = new List<StoreProductModel>();
            if (partCodes == null || partCodes.Count == 0) return list;

            using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
            {
                StringBuilder sql = new StringBuilder(@"
                SELECT 
                    PartACode,
                    ISNULL([Max], 0)    AS [Max],
                    ISNULL([Min], 0)    AS [Min],
                    ISNULL(QtyStkb, 0)  AS QtyStkb,
                    ISNULL(Stock, 0)    AS Stock,
                    Remark,
                    StockStatus
                FROM VW_StoreMonitoring
                WHERE PartACode IN (");

                var paramNames = partCodes.Select((s, i) => $"@p{i}").ToList();
                sql.Append(string.Join(",", paramNames));
                sql.Append(")");

                SqlCommand cmd = new SqlCommand(sql.ToString(), conn);

                for (int i = 0; i < partCodes.Count; i++)
                    cmd.Parameters.AddWithValue($"@p{i}", partCodes[i]);

                conn.Open();
                using (SqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        list.Add(new StoreProductModel
                        {
                            PartACode = rdr["PartACode"]?.ToString() ?? "",
                            Max = rdr["Max"]?.ToString() ?? "0",
                            Min = rdr["Min"]?.ToString() ?? "0",
                            QtyStkb = rdr["QtyStkb"]?.ToString() ?? "0",
                            Stock = rdr["Stock"]?.ToString() ?? "0",
                            Remark = rdr["Remark"]?.ToString() ?? "",
                            StockStatus = rdr["StockStatus"]?.ToString() ?? "NORMAL"
                        });
                    }
                }
            }
            return list;
        }

        #endregion
    }
}