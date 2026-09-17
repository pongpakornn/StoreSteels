using System;

namespace StoreSteels.Models
{
    // โครงสร้างดึงข้อมูล Master มาจากตาราง MST_PART ร่วมกับ MST_CALC_CONFIG
    public class PartMasterModel
    {
        public int PT_ID { get; set; }
        public string CustomerName { get; set; } // แทน CUST_CODE
        public string PartACode { get; set; }    // PT_ACODE
        public string PartNo { get; set; }       // PT_NO
        public string PartName { get; set; }     // PT_DESC
        public string PartCode { get; set; }     // PT_CODE (พาร์ทเดี่ยว)
        public int PackSize { get; set; }        // PT_PSZ
        public int DayMin { get; set; }         // คอนฟิกเริ่มต้นจากตาราง MST_CALC_CONFIG
        public int DayMax { get; set; }         // คอนฟิกเริ่มต้นจากตาราง MST_CALC_CONFIG
        public int QtyMax { get; set; }
        public int QtyMin { get; set; }
    }

    public class MaxMinPreviewData
    {
        public int PT_ID { get; set; }
        public string Customer { get; set; }
        public string PartACode { get; set; }
        public string PartCode { get; set; }
        public string PartNo { get; set; }
        public string PartName { get; set; }
        public double OrderQty { get; set; }
        public int WorkDays { get; set; }
        public int PackSize { get; set; }
        public int MinDays { get; set; }
        public int MaxDays { get; set; }
        public double CalculatedMinBox { get; set; }
        public double CalculatedMaxBox { get; set; }
    }
}