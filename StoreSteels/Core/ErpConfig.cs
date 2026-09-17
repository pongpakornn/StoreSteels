using System.Configuration;

namespace StoreSteels.Core
{
    // Connection string ของ ERP (CHR) แยกต่างหากจาก GlobalConfig.ConnStr (Stock DB)
    // อ่านจาก App.config คนละ key ตามที่ร้องขอ ใช้แบบ Read-only สำหรับหน้า Packing Card เท่านั้น
    public static class ErpConfig
    {
        private const string FallbackConnStr =
            @"Server=192.168.10.10;Database=CHR;User ID=sa;Password=;TrustServerCertificate=True;Encrypt=False;ApplicationIntent=ReadOnly;";

        public static string ConnStr =>
            ConfigurationManager.ConnectionStrings["ErpConnectionString"]?.ConnectionString ?? FallbackConnStr;
    }
}
