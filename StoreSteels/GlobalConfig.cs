using StoreSteels.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StoreSteels.Core
{
    //    public static class GlobalConfig
    //    {
    //        public static readonly string ConnStr = //@"Server=DESKTOP-TJ7525D\SQLEXPRESS; Database=StoreSteels; User ID=sa; Password=1234; TrustServerCertificate=True;";
    //        //@"Server=PAPYRUS-DB; Database=StoreSteels; User ID=Papyrus; Password=Software23!; TrustServerCertificate=True;";
    //        @"Server=DESKTOP-TJ7525D\SQLEXPRESS; Database=StoreSteels; User ID=sa; Password=123; TrustServerCertificate=True;";

    //        public static UserSession CurrentUser { get; set; }
    //    }
    //}
    public static class GlobalConfig
    {
        // 🏢 ใช้ที่บริษัท
        public static readonly string OfficeConnStr =
            @"Server=PAPYRUS-DB; Database=StoreSteels; User ID=Papyrus; Password=Software23!; TrustServerCertificate=True;";

        // 🏠 ใช้ที่บ้านผ่าน VPN
        public static readonly string HomeConnStr =
            @"Server=192.168.10.56; Database=StoreSteels; User ID=Papyrus; Password=Software23!; TrustServerCertificate=True;";

        // ตัวที่ระบบใช้งานจริง
        public static string ConnStr => HomeConnStr;

        public static UserSession CurrentUser { get; set; }
    }
}


























































