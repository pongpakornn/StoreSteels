//// Store ( Max - Min )
//using StoreSteels.Helpers;
//using System;
//using System.IO;
//using StoreSteels.Converters;
//using System.ComponentModel;
//using System.Windows.Media.Imaging;
//using System.Runtime.CompilerServices;


//namespace StoreSteels.Models
//{
//    public class StoreProductModel : INotifyPropertyChanged
//    {
//        // 1. ID ใช้ตัวเดิมให้ระบบ (ViewModel) รันเลขลำดับหน้าจอให้ตอนโหลดข้อมูล
//        private string _id;
//        public string ID { get => _id; set { _id = value; OnPropertyChanged(); } }

//        private string _customerCode;
//        public string CustomerCode { get => _customerCode; set { _customerCode = value; OnPropertyChanged(); } }

//        private string _modelCode;
//        public string ModelCode { get => _modelCode; set { _modelCode = value; OnPropertyChanged(); } }


//        #region === [ จุดที่อัพเดทเเละกำลังทดสอบการทำงานของระบบ 07-06-2026 ] ===

//        private string _imageFileName;

//        public string ImageFileName
//        {
//            get => _imageFileName;
//            set
//            {
//                _imageFileName = value;

//                OnPropertyChanged();

//                OnPropertyChanged(nameof(FullImagePath));

//                // ✅ สำคัญ
//                OnPropertyChanged(nameof(ProductImage));
//            }
//        }

//        #endregion

//        private string _partCode;
//        public string PartCode { get => _partCode; set { _partCode = value; OnPropertyChanged(); } }

//        private string _partACode;
//        public string PartACode { get => _partACode; set { _partACode = value; OnPropertyChanged(); } }

//        private string _partNo;
//        public string PartNo { get => _partNo; set { _partNo = value; OnPropertyChanged(); } }

//        private string _partName;
//        public string PartName { get => _partName; set { _partName = value; OnPropertyChanged(); } }

//        private string _packSize;
//        public string PackSize { get => _packSize; set { _packSize = value; OnPropertyChanged(); } }

//        #region === [ กำหนดการแสดงผลของค่า Max Min ถ้าเป็น 0 ให้แสดง - ] ===

//        private string _max;
//        public string Max
//        {
//            // ดึงค่าไปโชว์บนจอ: ถ้าหลังบ้านเป็น "0" หรือว่าง ให้โชว์ "-"
//            get => (_max == "0" || string.IsNullOrWhiteSpace(_max)) ? "-" : _max;
//            set
//            {
//                // จังหวะเก็บค่า: ปรับอินพุตเข้าสู่มาตรฐาน เพื่อไม่ให้เทียบค่า Real-time พลาด
//                string val = (value == "-" || string.IsNullOrWhiteSpace(value)) ? "0" : value;

//                if (_max == val) return;
//                _max = val;

//                OnPropertyChanged();
//            }
//        }

//        private string _min;
//        public string Min
//        {
//            get => (_min == "0" || string.IsNullOrWhiteSpace(_min)) ? "-" : _min;
//            set
//            {
//                string val = (value == "-" || string.IsNullOrWhiteSpace(value)) ? "0" : value;

//                if (_min == val) return;
//                _min = val;

//                OnPropertyChanged();
//            }
//        }

//        #endregion

//        #region === [ กำหนดการคำนวณ Auto : ถ้าปรับ StockBox ระบบจะคำนวณ StockPcs ตาม ] ===

//        // 12. ยอดคงเหลือกล่องปัจจุบัน (Current Box)
//        // --- 👑 [ ส่วนที่นนท์ให้เพิ่มเติม: Auto Calculation Logic ] ---
//        private bool _isCalculating = false; // Flag ป้องกัน Infinite Loop ตอนคำนวณสลับฝั่ง

//        private string _qtyStkb; // StockBox
//        public string QtyStkb
//        {
//            get => _qtyStkb;
//            set
//            {
//                if (_qtyStkb == value) return;
//                _qtyStkb = value;
//                OnPropertyChanged();

//                // เงื่อนไข: ถ้า User เปลี่ยน StockBox -> ให้คำนวณ StockPCS (Stock) อัตโนมัติ
//                if (!_isCalculating)
//                {
//                    _isCalculating = true;
//                    try
//                    {
//                        if (double.TryParse(value, out double box) && double.TryParse(PackSize, out double pack))
//                        {
//                            Stock = (box * pack).ToString();
//                        }
//                    }
//                    finally { _isCalculating = false; }
//                }
//            }
//        }

//        //ตัวคำนวณเเบบปัดขึ้นให้เป็นจำนวนเต็ม
//        private string _stock; // StockPCS
//        public string Stock
//        {
//            get => _stock;
//            set
//            {
//                if (_stock == value) return;
//                _stock = value;
//                OnPropertyChanged();

//                // เงื่อนไข: ถ้า User เปลี่ยน StockPCS -> ให้คำนวณ StockBox (QtyStkb) เผื่อเศษปัดขึ้น
//                if (!_isCalculating)
//                {
//                    _isCalculating = true;
//                    try
//                    {
//                        if (double.TryParse(value, out double pcs) && double.TryParse(PackSize, out double pack) && pack > 0)
//                        {
//                            // 👑 ตัวสำรองแบบที่ 1: หารแล้วปัดเศษขึ้นเป็นจำนวนเต็มทันทีด้วย Math.Ceiling
//                            double calculatedBoxes = Math.Ceiling(pcs / pack);
//                            QtyStkb = calculatedBoxes.ToString();
//                        }
//                    }
//                    finally { _isCalculating = false; }
//                }
//            }
//        }

//        #endregion

//        // --- 👑 [ ส่วนที่นนท์ให้เพิ่มเติม: Auto Calculation Logic ผ่าน SQL Server ] ---
//        //private string _qtyStkb;

//        //public string QtyStkb
//        //{
//        //    get => _qtyStkb;
//        //    set
//        //    {
//        //        if (_qtyStkb == value)
//        //            return;

//        //        _qtyStkb = value;

//        //        OnPropertyChanged();
//        //    }
//        //}

//        // 14. หมายเหตุเพิ่มเติม
//        //private string _stock;

//        //public string Stock
//        //{
//        //    get => _stock;
//        //    set
//        //    {
//        //        if (_stock == value)
//        //            return;

//        //        _stock = value;

//        //        OnPropertyChanged();
//        //    }
//        //}

//        private string _remark;
//        public string Remark { get => _remark; set { _remark = value; OnPropertyChanged(); } }

//        private bool _isRemarkEditing;
//        public bool IsRemarkEditing
//        {
//            get => _isRemarkEditing;
//            set
//            {
//                _isRemarkEditing = value;
//                OnPropertyChanged();
//            }
//        }

//        // --- ฟิลด์จัดการระบบภายในและการจัดกลุ่ม ---
//        public string Category { get; set; }
//        public int Priority { get; set; }
//        public string QRCodeData { get; set; }
//        public bool IsActive { get; set; }
//        public bool IsShow { get; set; }

//        //public string StockStatus { get; set; }
//        private string _stockStatus;

//        // อัพเดทเพิ่มเติม 07--06-2026
//        public string StockStatus
//        {
//            get => _stockStatus;
//            set
//            {
//                if (_stockStatus == value)
//                    return;

//                _stockStatus = value;

//                OnPropertyChanged();
//            }
//        }


//        #region === [ เพิ่มประสิทธิ์ที่ภาพให้กับระบบจากดึงรูปภาพตรงๆโหลดรูปภาพใหม่ทุกครั้งเปลี่ยนเป็นโหลดครั้งเดียวเเละบันทึกไว้ใช้ได้เลย ] ===

//        // ดึงรูปผ่าน Network Path ไปแสดงบนหน้าจอ
//        public string FullImagePath
//        {
//            get
//            {
//                if (string.IsNullOrWhiteSpace(ImageFileName))
//                    return null;

//                return Path.Combine(
//                    @"\\192.168.10.56\ProgramCHR\2. Store Only\StoreSteels\ImageStore",
//                    //@"C:\Users\pongp\Desktop\WorkMe\3. Project WPF\2. Program StoreSteels\1. ImageStore",
//                    ImageFileName
//                );
//            }
//        }

//        // ดึงรูปผ่าน Network Path ไปแสดงบนหน้าจอครั้งเดียวเเละไม่ต้องโหลดใหม่ทุกๆครั้ง
//        public BitmapImage ProductImage
//        {
//            get
//            {
//                return ImageCacheHelper.LoadImage(FullImagePath);
//            }
//        }

//        #endregion

//        public event PropertyChangedEventHandler PropertyChanged;
//        protected void OnPropertyChanged([CallerMemberName] string name = null)
//        {
//            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
//        }
//    }
//}
// Store ( Max - Min )
using StoreSteels.Helpers;
using System;
using System.IO;
using StoreSteels.Converters;
using System.ComponentModel;
using System.Windows.Media.Imaging;
using System.Runtime.CompilerServices;


namespace StoreSteels.Models
{
    public class StoreProductModel : INotifyPropertyChanged
    {
        // 1. ID
        private string _id;
        public string ID { get => _id; set { _id = value; OnPropertyChanged(); } }

        private string _customerCode;
        public string CustomerCode { get => _customerCode; set { _customerCode = value; OnPropertyChanged(); } }

        private string _modelCode;
        public string ModelCode { get => _modelCode; set { _modelCode = value; OnPropertyChanged(); } }

        private string _imageFileName;
        public string ImageFileName
        {
            get => _imageFileName;
            set
            {
                _imageFileName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FullImagePath));
                OnPropertyChanged(nameof(ProductImage));
            }
        }

        private string _partCode;
        public string PartCode { get => _partCode; set { _partCode = value; OnPropertyChanged(); } }

        private string _partACode;
        public string PartACode { get => _partACode; set { _partACode = value; OnPropertyChanged(); } }

        private string _partNo;
        public string PartNo { get => _partNo; set { _partNo = value; OnPropertyChanged(); } }

        private string _partName;
        public string PartName { get => _partName; set { _partName = value; OnPropertyChanged(); } }

        private string _packSize;
        public string PackSize { get => _packSize; set { _packSize = value; OnPropertyChanged(); } }

        #region === [ Max / Min ] ===

        private string _max;
        public string Max
        {
            get => (_max == "0" || string.IsNullOrWhiteSpace(_max)) ? "-" : _max;
            set
            {
                string val = (value == "-" || string.IsNullOrWhiteSpace(value)) ? "0" : value;
                if (_max == val) return;
                _max = val;
                OnPropertyChanged();
            }
        }

        private string _min;
        public string Min
        {
            get => (_min == "0" || string.IsNullOrWhiteSpace(_min)) ? "-" : _min;
            set
            {
                string val = (value == "-" || string.IsNullOrWhiteSpace(value)) ? "0" : value;
                if (_min == val) return;
                _min = val;
                OnPropertyChanged();
            }
        }

        #endregion

        #region === [ QtyStkb / Stock พร้อม Auto-Calc และ Sync Guard ] ===

        // 👑 FLAG สำคัญ: ตอน Real-time sync จาก DB ให้ Set flag นี้ก่อน
        //    เพื่อหยุด Auto-Calculation ไม่ให้คำนวณทับค่าที่ DB ส่งมา
        public bool IsSyncingFromDb { get; set; } = false;

        private bool _isCalculating = false;

        private string _qtyStkb;
        public string QtyStkb
        {
            get => _qtyStkb;
            set
            {
                if (_qtyStkb == value) return;
                _qtyStkb = value;
                OnPropertyChanged();

                if (IsSyncingFromDb || _isCalculating) return;

                _isCalculating = true;
                try
                {
                    if (double.TryParse(value, out double box) && double.TryParse(PackSize, out double pack) && pack > 0)
                    {
                        _stock = (box * pack).ToString();
                        OnPropertyChanged(nameof(Stock));
                    }
                }
                finally { _isCalculating = false; }
            }
        }

        private string _stock;
        public string Stock
        {
            get => _stock;
            set
            {
                if (_stock == value) return;
                _stock = value;
                OnPropertyChanged();

                if (IsSyncingFromDb || _isCalculating) return;

                _isCalculating = true;
                try
                {
                    if (double.TryParse(value, out double pcs) && double.TryParse(PackSize, out double pack) && pack > 0)
                    {
                        double calculatedBoxes = Math.Ceiling(pcs / pack);
                        _qtyStkb = calculatedBoxes.ToString();
                        OnPropertyChanged(nameof(QtyStkb));
                    }
                }
                finally { _isCalculating = false; }
            }
        }

        public void UpdateStockFromDb(double currentStockPcs, double packSize)
        {
            IsSyncingFromDb = true;
            try
            {
                PackSize = packSize.ToString();
                _stock = currentStockPcs.ToString();

                // คำนวณยอดกล่องปัดขึ้นจากยอดสต็อกชิ้นจริงใน DB
                if (packSize > 0)
                {
                    _qtyStkb = Math.Ceiling(currentStockPcs / packSize).ToString();
                }
                else
                {
                    _qtyStkb = "0";
                }

                // ยิงแจ้ง UI ทีเดียวพร้อมกันทั้ง 2 ช่อง
                OnPropertyChanged(nameof(Stock));
                OnPropertyChanged(nameof(QtyStkb));
            }
            finally
            {
                IsSyncingFromDb = false;
            }
        }

        #endregion

        private string _remark;
        public string Remark { get => _remark; set { _remark = value; OnPropertyChanged(); } }

        private bool _isRemarkEditing;
        public bool IsRemarkEditing
        {
            get => _isRemarkEditing;
            set { _isRemarkEditing = value; OnPropertyChanged(); }
        }

        public string Category { get; set; }
        public int Priority { get; set; }
        public string QRCodeData { get; set; }
        public bool IsActive { get; set; }
        public bool IsShow { get; set; }

        private string _stockStatus;
        public string StockStatus
        {
            get => _stockStatus;
            set
            {
                if (_stockStatus == value) return;
                _stockStatus = value;
                OnPropertyChanged();
            }
        }

        #region === [ Image ] ===

        public string FullImagePath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ImageFileName))
                    return null;

                return Path.Combine(
                    @"\\192.168.10.56\ProgramCHR\2. Store Only\StoreSteels\ImageStore",
                    //@"C:\Users\pongp\Desktop\WorkMe\3. Project WPF\2. Program StoreSteels\1. ImageStore",
                    ImageFileName
                ); 
            }
        }

        public BitmapImage ProductImage
        {
            get { return ImageCacheHelper.LoadImage(FullImagePath); }
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}