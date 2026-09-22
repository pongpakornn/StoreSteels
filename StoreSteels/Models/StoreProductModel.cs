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

        // ตัวระบุหลักของสินค้า (เดิมคือ PartACode/PT_ACODE - schema ใหม่ตัดออก เหลือ PT_CODE ตัวเดียว)
        private string _partCode;
        public string PartCode { get => _partCode; set { _partCode = value; OnPropertyChanged(); } }

        private string _partName;
        public string PartName { get => _partName; set { _partName = value; OnPropertyChanged(); } }

        private string _packSize;
        public string PackSize { get => _packSize; set { _packSize = value; OnPropertyChanged(); } }

        // แทนที่ CustomerCode เดิม - ใช้จัดกลุ่มตาราง Store (Max-Min) แทน
        private string _category;
        public string Category { get => _category; set { _category = value; OnPropertyChanged(); } }

        private string _supplier;
        public string Supplier { get => _supplier; set { _supplier = value; OnPropertyChanged(); } }

        private string _bin;
        public string Bin { get => _bin; set { _bin = value; OnPropertyChanged(); } }

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

        // ยอดคงคลัง (QTY_STKB) - schema ใหม่เหลือค่าเดียว ไม่มี QTY_STK/PackSize auto-calc pcs↔box
        // อีกต่อไป เพราะค่านี้แทนน้ำหนัก (กก.) ไม่ใช่จำนวนกล่อง/ชิ้น
        private string _qty;
        public string Qty { get => _qty; set { _qty = value; OnPropertyChanged(); } }

        private string _remark;
        public string Remark { get => _remark; set { _remark = value; OnPropertyChanged(); } }

        private bool _isRemarkEditing;
        public bool IsRemarkEditing
        {
            get => _isRemarkEditing;
            set { _isRemarkEditing = value; OnPropertyChanged(); }
        }

        public int Priority { get; set; }
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

        public string FullImagePath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ImageFileName))
                    return null;

                return Path.Combine(
                    @"\\192.168.10.56\ProgramCHR\2. Store Only\StoreSteels\Image",
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
