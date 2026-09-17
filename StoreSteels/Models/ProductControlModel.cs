using System;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging; // 💡 เพิ่ม Namespace สำหรับจัดการ BitmapImage

namespace StoreSteels.Models
{
    public class ProductControlModel : INotifyPropertyChanged
    {
        private string _partCode;
        private string _partName;
        private string _packSize;
        private string _category;
        private string _max;
        private string _min;
        private string _qrCodeData;
        private string _stock;
        private string _location;
        private bool _isActive;
        private bool _isShow;
        private bool _isSelected;
        private string _imageFileName;

        // 👥 ฟิลด์ภายในสำหรับ 4 ฟิลด์ใหม่ เพื่อทำ Property Notification
        private string _customerCode;
        private string _modelCode;
        private string _partACode;
        private string _partNo;

        // ฟิลด์พื้นฐานเดิม
        public string PartCode { get => _partCode; set { _partCode = value; OnPropertyChanged(); } }
        public string PartName { get => _partName; set { _partName = value; OnPropertyChanged(); } }
        public string PackSize { get => _packSize; set { _packSize = value; OnPropertyChanged(); } }
        public string Category { get => _category; set { _category = value; OnPropertyChanged(); } }
        public string Max { get => _max; set { _max = value; OnPropertyChanged(); } }
        public string Min { get => _min; set { _min = value; OnPropertyChanged(); } }
        public string QRCodeData { get => _qrCodeData; set { _qrCodeData = value; OnPropertyChanged(); } }
        public string Stock { get => _stock; set { _stock = value; OnPropertyChanged(); } }
        public string Location { get => _location; set { _location = value; OnPropertyChanged(); } }
        public bool IsActive { get => _isActive; set { _isActive = value; OnPropertyChanged(); } }
        public bool IsShow { get => _isShow; set { _isShow = value; OnPropertyChanged(); } }
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

        // 🛠️ แก้ไข: 4 ฟิลด์ใหม่ให้รองรับ OnPropertyChanged() เพื่อให้ Grid และหน้าจอเปลี่ยนค่าตามทันที
        public string CustomerCode { get => _customerCode; set { _customerCode = value; OnPropertyChanged(); } }
        public string ModelCode { get => _modelCode; set { _modelCode = value; OnPropertyChanged(); } }
        public string PartACode { get => _partACode; set { _partACode = value; OnPropertyChanged(); } }
        public string PartNo { get => _partNo; set { _partNo = value; OnPropertyChanged(); } }

        // ชื่อไฟล์รูปภาพที่เก็บใน DB
        public string ImageFileName
        {
            get => _imageFileName;
            set
            {
                _imageFileName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FullImagePath));
                //OnPropertyChanged(nameof(ProductImage)); // 💡 แจ้งให้ XAML วาดรูปใน DataGrid ใหม่ทันทีเมื่อชื่อไฟล์เปลี่ยน
            }
        }

        // Property สำหรับคำนวณ Path เต็ม (ยังเก็บไว้เช็กหรือดึงไปใช้งานส่วนอื่นได้)
        public string FullImagePath
        {
            get
            {
                if (string.IsNullOrEmpty(ImageFileName)) return null;

                if (Path.IsPathRooted(ImageFileName) && File.Exists(ImageFileName))
                    return ImageFileName;

                //string baseFolder = @"\\192.168.10.56\ProgramCHR\2. Store Only\StoreSteels\ImageStore";
                string baseFolder = @"C:\Users\pongp\Desktop\WorkMe\3. Project WPF\2. Program StoreSteels\1. ImageStore";
                string fullPath = Path.Combine(baseFolder, ImageFileName);

                return File.Exists(fullPath) ? fullPath : null;
            }
        }

        // ✅ 2. เอาคอมเมนต์ออกจากบล็อก ProductImage เพื่อให้ส่งภาพชนิด BitmapImage ออกไปใช้งาน
        public BitmapImage ProductImage
        {
            get
            {
                string path = FullImagePath;
                if (string.IsNullOrEmpty(path)) return null;

                try
                {
                    // เทคนิคโหลดรูปเข้าหน่วยความจำชั่วคราวและปลดล็อกไฟล์ทันที ป้องกันปัญหาเอนจิน WPF ค้าง
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad; // สำคัญที่สุด: โหลดเสร็จคลายล็อกไฟล์ต้นฉบับ
                    bitmap.UriSource = new Uri(path, UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze(); // ทำให้ส่งรูปข้ามไปวาดบนคอนโทรลต่างๆ ได้เร็วขึ้นและไม่แครช
                    return bitmap;
                }
                catch
                {
                    return null; // ถ้ารูปภาพบนเครื่องเสียหายหรือเปิดไม่ได้ จะคืนค่าเป็น null เพื่อไม่ให้โปรแกรมค้าง
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}