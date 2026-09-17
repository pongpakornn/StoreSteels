using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StoreSteels.Models
{
    public class UserSession
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public int UserLevel { get; set; }    // USR_LVL (1, 2, 3)
        public bool IsMasterAdmin { get; set; } // สำหรับสิทธิ์พิเศษของคุณเอง
        public string Department { get; set; }

        #region === [ เพิ่มส่วนนี้เพื่อเช็คสิทธิ์แบบละเอียด (Permission) ] ===
        public List<UserPermission> Permissions { get; set; } = new List<UserPermission>();
        #endregion

        // =========================================================================
        // [ปรับปรุงใหม่] Level 1 และ 2 เข้าได้ทุกระบบ / Level 3 เช็คตามสิทธิ์จริงใน List
        // =========================================================================

        // สิทธิ์การเข้าหน้า Scanner (รวม Scan In และ Scan Out) เมนู Sidebar *** เพิ่มก็ไำด้เเละไม่เพิ่มก็ได้ ***
        public bool CanViewScannerMenu
        {
            get
            {
                if (UserLevel == 1) return true;
                return CanViewScanIn || CanViewScanOut;
            }
        }

        // สิทธิ์การเข้าหน้า Scan In
        // ในไฟล์ UserSession.cs
        public bool CanViewScanIn
        {
            get
            {
                // ถ้าเป็น Admin (Level 1,2) ให้เห็นปุ่มนี้เสมอ
                if (UserLevel == 1) return true;
                // ถ้าเป็นพนักงาน ให้เช็คสิทธิ์ในฐานข้อมูล
                return Permissions.Any(p => p.SystemId == "SCAN_IN" && p.CanView);
            }
        }

        public bool CanViewScanOut
        {
            get
            {
                // ถ้าเป็น Admin (Level 1,2) ให้เห็นปุ่มนี้เสมอ
                if (UserLevel == 1) return true;
                // ถ้าเป็นพนักงาน ให้เช็คสิทธิ์ในฐานข้อมูล
                return Permissions.Any(p => p.SystemId == "SCAN_OUT" && p.CanView);
            }
        }

        // =========================================================================
        // 🆕 [เพิ่มใหม่] Dashboard / ProductControl / MaxMinCalculator
        // ใช้ Pattern เดียวกับ CanViewScanIn/CanViewScanOut เป๊ะๆ:
        // Level 1 bypass อัตโนมัติเท่านั้น ส่วน Level 2, 3 ต้องมีแถว Permission ในตารางจริง
        // =========================================================================
        public bool CanViewDashboard
        {
            get
            {
                if (UserLevel == 1) return true;
                return Permissions.Any(p => p.SystemId == "DASHBOARD" && p.CanView);
            }
        }

        public bool CanViewProductControl
        {
            get
            {
                if (UserLevel == 1) return true;
                return Permissions.Any(p => p.SystemId == "PDControl" && p.CanView);
            }
        }

        public bool CanViewMaxMinCalculator
        {
            get
            {
                if (UserLevel == 1) return true;
                return Permissions.Any(p => p.SystemId == "MaxMinCalc" && p.CanView);
            }
        }

        // กำหนดไม่ให้ User3 เห็นเมนูอื่นๆ เห็นเพียงเเค่ Scanner
        public bool CanViewAdminMenu => (UserLevel == 1 || UserLevel == 2);
    }

}