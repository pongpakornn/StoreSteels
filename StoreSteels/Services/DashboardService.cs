using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using StoreSteels.Core;
using StoreSteels.Models;

namespace StoreSteels.Services
{
    public class DashboardService
    {
        public List<DashboardSummaryCard> GetSummaryCards()
        {
            int totalCustomers = 0;
            int totalProducts = 0;
            int overMaxCount = 0;
            int underMinCount = 0;

            using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
            {
                conn.Open();

                // Card 1: แสดงจำนวนลูกค้าทั้งหมดที่มี IS_SHOW_MST = 1 (นับแบบ DISTINCT)
                string sqlCust = @"SELECT COUNT(DISTINCT PT_CUST) 
                                   FROM MST_PART 
                                   WHERE IS_ACTIVE = 1 
                                     AND IS_SHOW_MST = 1 
                                     AND PT_CUST IS NOT NULL 
                                     AND PT_CUST <> ''";
                using (SqlCommand cmd = new SqlCommand(sqlCust, conn))
                {
                    totalCustomers = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                }

                // Card 2: แสดงจำนวน PartACode ทั้งหมดที่มี IS_SHOW_MST = 1
                string sqlProd = @"SELECT COUNT(DISTINCT PT_ACODE) 
                                   FROM MST_PART 
                                   WHERE IS_ACTIVE = 1 
                                     AND IS_SHOW_MST = 1";
                using (SqlCommand cmd = new SqlCommand(sqlProd, conn))
                {
                    totalProducts = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                }

                // Card 3: แสดงจำนวนสินค้าที่ OVER_MAX และ NORMAL_GOOD (กลุ่มสีเขียวแก่ และ เขียวอ่อน)
                // ไม่นับรายการที่ Max และ Min เป็น 0
                string sqlMax = @"
SELECT COUNT(*)
FROM VW_StoreMonitoring
WHERE StockStatus IN ('OVER_MAX', 'NORMAL_GOOD')
  AND ISNULL([Max], 0) > 0"; // ป้องกันกรณีเป็น 0 ทั้งคู่

                using (SqlCommand cmd = new SqlCommand(sqlMax, conn))
                {
                    overMaxCount = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                }

                // Card 4: แสดงจำนวนสินค้าที่ UNDER_MIN หรือ OUT_OF_STOCK (กลุ่มสีแดง)
                // ย้ำว่าเฉพาะที่น้อยกว่า Min และไม่นับกรณี Max Min เป็น 0
                string sqlMin = @"
SELECT COUNT(*)
FROM VW_StoreMonitoring
WHERE StockStatus IN ('UNDER_MIN', 'OUT_OF_STOCK')
  AND ISNULL([Min], 0) > 0"; // ป้องกันกรณีไม่มีคอนฟิก Min

                using (SqlCommand cmd = new SqlCommand(sqlMin, conn))
                {
                    underMinCount = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                }
            }

            // ส่งผลลัพธ์ที่นับได้จาก DB จริงกลับไปผูกกับ UI โดยใช้รูปแบบ Pack URI ดึง GIF ตามเดิมครับ
            return new List<DashboardSummaryCard>
            {
                new DashboardSummaryCard
                {
                    Title = "Customer",
                    Value = totalCustomers.ToString("N0"), // ใส่ฟอร์แมตคอมม่าเพื่อความสวยงาม
                    GifPath = "pack://application:,,,/Assets/Gifs/GroupCastomer.gif"
                },
                new DashboardSummaryCard
                {
                    Title = "Product All",
                    Value = totalProducts.ToString("N0"),
                    GifPath = "pack://application:,,,/Assets/Gifs/Product.gif"
                },
                new DashboardSummaryCard
                {
                    Title = "Max Product",
                    Value = overMaxCount.ToString("N0"),
                    GifPath = "pack://application:,,,/Assets/Gifs/Forklift.gif"
                },
                new DashboardSummaryCard
                {
                    Title = "Min Product",
                    Value = underMinCount.ToString("N0"),
                    GifPath = "pack://application:,,,/Assets/Gifs/Alert.gif"
                }
            };
        }

        // เพิ่มฟังก์ชันนี้ไว้ในคลาส DashboardService
        public async Task<List<string>> GetActiveCustomersAsync()
        {
            var customers = new List<string>();
            using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
            {
                conn.Open();
                string sql = @"SELECT DISTINCT PT_CUST 
                       FROM MST_PART 
                       WHERE IS_ACTIVE = 1 
                         AND IS_SHOW_MST = 1 
                         AND PT_CUST IS NOT NULL 
                         AND PT_CUST <> ''
                       ORDER BY PT_CUST";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        customers.Add(reader["PT_CUST"].ToString());
                    }
                }
            }
            return customers;
        }

        //public List<CustomerOrderShare> GetMonthlyCustomerOrders()
        //{
        //    var list = new List<CustomerOrderShare>();
        //    using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
        //    {
        //        conn.Open();
        //        string sql = "SELECT * FROM VW_Dashboard_PieChart";
        //        using (SqlCommand cmd = new SqlCommand(sql, conn))
        //        using (SqlDataReader reader = cmd.ExecuteReader())
        //        {
        //            while (reader.Read())
        //            {
        //                list.Add(new CustomerOrderShare
        //                {
        //                    CustomerName = reader["CustomerName"].ToString(),
        //                    OrderAmount = Convert.ToDouble(reader["OrderAmount"]),
        //                    Percentage = Convert.ToDouble(reader["Percentage"]),
        //                    HexColor = GenerateStaticColor(reader["CustomerName"].ToString())
        //                });
        //            }
        //        }
        //    }
        //    return list;
        //}
        public List<CustomerOrderShare> GetMonthlyCustomerOrders()
        {
            var list = new List<CustomerOrderShare>();
            using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
            {
                conn.Open();
                string sql = "SELECT * FROM VW_Dashboard_PieChart"; // วิ่งไปเรียกคำสั่งใหม่ที่เรา ALTER ไว้
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new CustomerOrderShare
                        {
                            CustomerName = reader["CustomerName"].ToString(),
                            OrderAmount = Convert.ToDouble(reader["OrderAmount"]),
                            Percentage = Convert.ToDouble(reader["Percentage"]),
                            HexColor = GenerateStaticColor(reader["CustomerName"].ToString())
                        });
                    }
                }
            }
            return list;
        }

        //public List<MonthlyForecastOrderCompare> GetYearlyForecastOrder()
        //{
        //    var list = new List<MonthlyForecastOrderCompare>();
        //    using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
        //    {
        //        conn.Open();
        //        string sql = "SELECT * FROM VW_Dashboard_BarChart ORDER BY MonthOrder";
        //        using (SqlCommand cmd = new SqlCommand(sql, conn))
        //        using (SqlDataReader reader = cmd.ExecuteReader())
        //        {
        //            while (reader.Read())
        //            {
        //                list.Add(new MonthlyForecastOrderCompare
        //                {
        //                    MonthName = reader["MonthName"].ToString(),
        //                    ForecastValue = Convert.ToDouble(reader["ForecastValue"]),
        //                    OrderValue = Convert.ToDouble(reader["OrderValue"])
        //                });
        //            }
        //        }
        //    }
        //    return list;
        //}
        public List<MonthlyForecastOrderCompare> GetYearlyForecastOrder()
        {
            var list = new List<MonthlyForecastOrderCompare>();
            using (SqlConnection conn = new SqlConnection(GlobalConfig.ConnStr))
            {
                conn.Open();
                string sql = "SELECT * FROM VW_Dashboard_BarChart ORDER BY MonthOrder";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new MonthlyForecastOrderCompare
                        {
                            MonthName = reader["MonthName"].ToString(),
                            ForecastValue = Convert.ToDouble(reader["ForecastValue"]),
                            OrderValue = Convert.ToDouble(reader["OrderValue"]),
                            DeliveryValue = Convert.ToDouble(reader["DeliveryValue"]) // 🚀 ผูกค่าเพิ่มตรงนี้
                        });
                    }
                }
            }
            return list;
        }

        private string GenerateStaticColor(string text)
        {
            int hash = 0;
            foreach (char c in text)
            {
                hash = c + ((hash << 5) - hash);
            }

            int r = (hash & 0xFF0000) >> 16;
            int g = (hash & 0x00FF00) >> 8;
            int b = hash & 0x0000FF;

            r = (r % 150) + 80;
            g = (g % 150) + 80;
            b = (b % 150) + 80;

            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}