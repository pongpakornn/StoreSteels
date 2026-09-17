using Microsoft.Win32; // อย่าลืมเพิ่ม Namespace นี้ที่ด้านบน
using StoreSteels.Helpers;
using StoreSteels.Models;
using StoreSteels.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace StoreSteels.ViewModels
{
    public class MaxMinCalViewModel : INotifyPropertyChanged
    {
        private List<PartMasterModel> _allPartsMaster;
        private int _currentPtId;
        private int _currentPackSize = 1;
        private List<MaxMinPreviewData> _masterPreviewList = new List<MaxMinPreviewData>();

        // เก็บสถานะว่ากำลังเลือกดูหรือแก้ไข Row ไหนอยู่ (ใช้สำหรับกดครั้งที่ 2 เพื่อยกเลิก)
        private MaxMinPreviewData _selectedViewRow = null;
        private MaxMinPreviewData _selectedEditRow = null;

        // ประกาศสร้างออบเจ็กต์ Service ไว้ใช้งานส่วนบนของ ViewModel
        private readonly ExcelImportService _excelImportService = new ExcelImportService();
        private readonly MaxMinCalService _maxMinCalService = new MaxMinCalService();

        public MaxMinCalViewModel()
        {
            PreviewList = new ObservableCollection<MaxMinPreviewData>();
            LoadPartMasterFromDatabase();

            ClearCommand = new RelayCommand(ExecuteClear);
            UpdateMasterCommand = new RelayCommand(ExecuteUpdateMaster);
            SearchCommand = new RelayCommand(ExecuteSearch);
            ImportSetMaxMinCommand = new RelayCommand(ExecuteImportSetMaxMin);
            ImportForecastOrderCommand = new RelayCommand(ExecuteImportForecastOrder);

            // 🎯 ผูกคำสั่งสำหรับปุ่มใน DataGrid (รองรับปุ่ม VIEW และ EDIT)
            ViewRowCommand = new RelayCommand<MaxMinPreviewData>(ExecuteViewRow);
            EditRowCommand = new RelayCommand<MaxMinPreviewData>(ExecuteEditRow);
        }

        public ObservableCollection<string> CustomerList { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> PartACodeList { get; set; } = new ObservableCollection<string>();

        #region Form Properties
        private string _selectedCustomer;
        public string SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (_selectedCustomer != value)
                {
                    _selectedCustomer = value;
                    OnPropertyChanged();
                    FilterPartACode();
                }
            }
        }

        private string _selectedPartACode;
        public string SelectedPartACode
        {
            get => _selectedPartACode;
            set
            {
                if (_selectedPartACode != value)
                {
                    _selectedPartACode = value;
                    OnPropertyChanged();
                    AutoFillPartDetails();
                }
            }
        }

        private string _partCode;
        public string PartCode
        {
            get => _partCode;
            set { _partCode = value; OnPropertyChanged(); }
        }

        private string _partNo;
        public string PartNo
        {
            get => _partNo;
            set { _partNo = value; OnPropertyChanged(); }
        }

        private string _partName;
        public string PartName
        {
            get => _partName;
            set { _partName = value; OnPropertyChanged(); }
        }

        private string _orderQty = "0";
        public string OrderQty
        {
            get => _orderQty;
            set { _orderQty = value; OnPropertyChanged(); RunLiveCalculation(); }
        }

        private string _workDays = "21";
        public string WorkDays
        {
            get => _workDays;
            set { _workDays = value; OnPropertyChanged(); RunLiveCalculation(); }
        }

        private string _minDays = "1";
        public string MinDays
        {
            get => _minDays;
            set { _minDays = value; OnPropertyChanged(); RunLiveCalculation(); }
        }

        private string _maxDays = "3";
        public string MaxDays
        {
            get => _maxDays;
            set { _maxDays = value; OnPropertyChanged(); RunLiveCalculation(); }
        }
        #endregion

        #region Search Properties

        private System.Threading.CancellationTokenSource _searchCts;

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    _ = SearchWithDelayAsync(_searchText);
                }
            }
        }

        private async Task SearchWithDelayAsync(string keyword)
        {
            _searchCts?.Cancel();
            _searchCts = new System.Threading.CancellationTokenSource();
            try
            {
                await Task.Delay(300, _searchCts.Token); // หน่วง 300ms
                ExecuteSearch(null);
            }
            catch (TaskCanceledException) { }
        }
        #endregion

        #region Result Properties
        private double _calculatedMinBox;
        public double CalculatedMinBox
        {
            get => _calculatedMinBox;
            set { _calculatedMinBox = value; OnPropertyChanged(); }
        }

        private double _calculatedMaxBox;
        public double CalculatedMaxBox
        {
            get => _calculatedMaxBox;
            set { _calculatedMaxBox = value; OnPropertyChanged(); }
        }

        // เพิ่ม Property นี้ไว้ที่ส่วนบนของ ViewModel
        private bool _isEditingMode;
        public bool IsEditingMode
        {
            get => _isEditingMode;
            set { _isEditingMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsReadOnlyMode)); }
        }

        // สร้าง Property คำนวณ (ใช้ใน XAML เพื่อสลับสถานะ)
        public bool IsReadOnlyMode => !IsEditingMode;
        #endregion

        public ObservableCollection<MaxMinPreviewData> PreviewList { get; set; }

        public ICommand ClearCommand { get; }
        public ICommand UpdateMasterCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ImportSetMaxMinCommand { get; }
        public ICommand ImportForecastOrderCommand { get; }

        // 🎯 เพิ่ม ICommand สำหรับ DataGrid Action
        public ICommand ViewRowCommand { get; }
        public ICommand EditRowCommand { get; }

        private readonly MaxMinCalService _maxMinService = new MaxMinCalService();
        private readonly string _currentUserEmpId = "SYSTEM";
        // 1. ระบบนำเข้า SET MAX MIN
        private void ExecuteImportSetMaxMin(object parameter)
        {
            // 1. เปิดหน้าต่างให้ User เลือกไฟล์
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Excel Files (*.xlsx)|*.xlsx|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName; // ใช้ไฟล์ที่ User เลือก

                try
                {
                    var excelData = _excelImportService.ReadMaxMinExcel(filePath);

                    if (excelData.Count == 0)
                    {
                        MessageBox.Show("ไม่พบข้อมูลที่จะนำเข้าในไฟล์ Excel กรุณาตรวจสอบข้อมูล", "คำเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    _maxMinCalService.ProcessImportMaxMin(excelData, "admin1");
                    MessageBox.Show($"นำเข้าไฟล์เรียบร้อยแล้ว จำนวน {excelData.Count} รายการ", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"เกิดข้อผิดพลาด: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // 2. ระบบนำเข้า FORECAST & ORDER
        private void ExecuteImportForecastOrder(object parameter)
        {
            // 1. สร้างหน้าต่างสำหรับเลือกไฟล์
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Excel Files (*.xlsx)|*.xlsx|All files (*.*)|*.*";
            openFileDialog.Title = "เลือกไฟล์ FORECAST & ORDER";

            // 2. ถ้า User กด OK หลังจากเลือกไฟล์แล้ว
            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName; // ใช้ Path จากไฟล์ที่ User เลือก

                try
                {
                    var excelData = _excelImportService.ReadForecastOrderExcel(filePath);

                    if (excelData.Count == 0)
                    {
                        MessageBox.Show("ไม่พบข้อมูลที่จะนำเข้าในไฟล์ Excel กรุณาตรวจสอบข้อมูล", "คำเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    _maxMinCalService.ProcessImportForecastOrder(excelData, "admin1");

                    // หากต้องการรีเฟรชหน้าจอ ให้ Un-comment บรรทัดนี้
                    // LoadDataGridFromDatabase(); 

                    MessageBox.Show($"นำเข้าไฟล์ FORECAST & ORDER และคำนวณ Auto เรียบร้อยแล้ว! (จำนวน {excelData.Count} รายการ)", "Excel Import Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"เกิดข้อผิดพลาดในการนำเข้าข้อมูล: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadPartMasterFromDatabase()
        {
            try
            {
                _allPartsMaster = _maxMinService.GetPartMasterList();

                var customers = _allPartsMaster
                    .Select(p => p.CustomerName)
                    .Where(c => !string.IsNullOrEmpty(c))
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList();

                CustomerList.Clear();
                foreach (var cust in customers) CustomerList.Add(cust);

                _masterPreviewList = _allPartsMaster.Select(p => new MaxMinPreviewData
                {
                    PT_ID = p.PT_ID,
                    Customer = p.CustomerName,
                    PartACode = p.PartACode,
                    PartCode = p.PartCode,
                    PartNo = p.PartNo,
                    PartName = p.PartName,
                    PackSize = p.PackSize,
                    MinDays = p.DayMin,
                    MaxDays = p.DayMax,
                    OrderQty = 0,
                    WorkDays = 21,
                    CalculatedMinBox = 0,
                    CalculatedMaxBox = 0
                }).ToList();

                ExecuteSearch(null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ข้อผิดพลาดระบบฐานข้อมูล", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterPartACode()
        {
            PartACodeList.Clear();
            if (string.IsNullOrEmpty(SelectedCustomer)) return;

            var codes = _allPartsMaster
                .Where(p => p.CustomerName == SelectedCustomer)
                .Select(p => p.PartACode)
                .Distinct()
                .ToList();

            foreach (var code in codes) PartACodeList.Add(code);
        }

        private void AutoFillPartDetails()
        {
            if (string.IsNullOrEmpty(SelectedPartACode) || string.IsNullOrEmpty(SelectedCustomer)) return;

            var part = _allPartsMaster.FirstOrDefault(p => p.PartACode == SelectedPartACode && p.CustomerName == SelectedCustomer);
            if (part != null)
            {
                _currentPtId = part.PT_ID;
                _currentPackSize = part.PackSize > 0 ? part.PackSize : 1;
                PartCode = part.PartCode;
                PartNo = part.PartNo;
                PartName = part.PartName;
                MinDays = part.DayMin.ToString();
                MaxDays = part.DayMax.ToString();
                RunLiveCalculation();
            }
        }

        private void RunLiveCalculation()
        {
            if (!double.TryParse(OrderQty, out double order) || order <= 0 ||
                !double.TryParse(WorkDays, out double days) || days <= 0 ||
                !int.TryParse(MinDays, out int minD) ||
                !int.TryParse(MaxDays, out int maxD))
            {
                CalculatedMinBox = 0;
                CalculatedMaxBox = 0;
                return;
            }

            double dailyProduction = order / days;
            CalculatedMinBox = Math.Ceiling((dailyProduction * minD) / _currentPackSize);
            CalculatedMaxBox = Math.Ceiling((dailyProduction * maxD) / _currentPackSize);
        }

        // 🎯 แก้ไขฟังก์ชันค้นหา: ดึง Pattern จาก sp_GetPartForQR ค้นหาครอบคลุมทุกฟิลด์
        // ปรับปรุง ExecuteSearch ให้เรียกผ่าน Service (Database)
        private void ExecuteSearch(object parameter)
        {
            // ดึงข้อมูลที่กรองแล้วจาก SQL โดยตรง
            var filteredData = _maxMinService.GetPartMasterList(SearchText);

            // อัปเดต PreviewList
            PreviewList.Clear();
            foreach (var item in filteredData)
            {
                PreviewList.Add(new MaxMinPreviewData
                {
                    PT_ID = item.PT_ID,
                    Customer = item.CustomerName,
                    PartACode = item.PartACode,
                    PartCode = item.PartCode,
                    PartNo = item.PartNo,
                    PartName = item.PartName,
                    PackSize = item.PackSize,
                    MinDays = item.DayMin,
                    MaxDays = item.DayMax,
                    // คงค่าเดิมที่ User อาจจะกรอกค้างไว้ไว้ได้
                    OrderQty = 0,
                    WorkDays = 21
                });
            }
        }

        // 🎯 ปุ่ม VIEW กดครั้งที่ 1 โหลดขึ้นฟอร์ม / กดครั้งที่ 2 ยกเลิก (Clear ฟอร์ม)
        private void ExecuteViewRow(MaxMinPreviewData selectedRow)
        {
            if (selectedRow == null) return;

            if (_selectedViewRow == selectedRow)
            {
                ExecuteClear(null);
                _selectedViewRow = null;
                IsEditingMode = false; // กลับเป็น ReadOnly
            }
            else
            {
                MapRowToForm(selectedRow);
                _selectedViewRow = selectedRow;
                _selectedEditRow = null;
                IsEditingMode = false; // โหมดดูอย่างเดียว
            }
        }

        // 🎯 ปุ่ม EDIT กดครั้งที่ 1 เตรียมแก้ไข / กดครั้งที่ 2 ยกเลิก (Clear ฟอร์ม)
        private void ExecuteEditRow(MaxMinPreviewData selectedRow)
        {
            if (selectedRow == null) return;

            if (_selectedEditRow == selectedRow)
            {
                ExecuteClear(null);
                _selectedEditRow = null;
                IsEditingMode = false; // กลับเป็น ReadOnly
            }
            else
            {
                MapRowToForm(selectedRow);
                _selectedEditRow = selectedRow;
                _selectedViewRow = null;
                IsEditingMode = true; // เปิดโหมดแก้ไข
            }
        }

        // ฟังก์ชันช่วยย้ายข้อมูลจาก Row ในตาราง ขึ้นมาที่ฟอร์มด้านบน
        private void MapRowToForm(MaxMinPreviewData row)
        {
            SelectedCustomer = row.Customer;
            SelectedPartACode = row.PartACode;
            PartCode = row.PartCode;
            PartNo = row.PartNo;
            PartName = row.PartName;
            MinDays = row.MinDays.ToString();
            MaxDays = row.MaxDays.ToString();
            OrderQty = row.OrderQty.ToString();
            WorkDays = row.WorkDays.ToString();
            _currentPtId = row.PT_ID;
            _currentPackSize = row.PackSize > 0 ? row.PackSize : 1;
            RunLiveCalculation();
        }

        //private void ExecuteUpdateMaster(object parameter)
        //{
        //    if (string.IsNullOrEmpty(SelectedCustomer) || string.IsNullOrEmpty(SelectedPartACode))
        //    {
        //        MessageBox.Show("กรุณาเลือกข้อมูล Customer และ Part A Code ให้ครบถ้วนก่อนทำการอัปเดตครับ", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        //        return;
        //    }

        //    int.TryParse(MinDays, out int minD);
        //    int.TryParse(MaxDays, out int maxD);
        //    double.TryParse(OrderQty, out double oQty);
        //    int.TryParse(WorkDays, out int wDays);

        //    try
        //    {
        //        bool isSaved = _maxMinService.SaveOrUpdateCalcConfig(SelectedCustomer, _currentPtId, SelectedPartACode, minD, maxD, _currentUserEmpId);

        //        if (isSaved)
        //        {
        //            MessageBox.Show($"บันทึกเงื่อนไขคอนฟิกจำนวนวันปลอดภัยเรียบร้อยแล้วครับ!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

        //            // ล้างสถานะการเลือกและดึงข้อมูลใหม่
        //            _selectedViewRow = null;
        //            _selectedEditRow = null;
        //            LoadPartMasterFromDatabase();
        //            ExecuteClear(null);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //}
        // ในส่วนของ ExecuteUpdateMaster
        private void ExecuteUpdateMaster(object parameter)
        {
            if (string.IsNullOrEmpty(SelectedCustomer) || string.IsNullOrEmpty(SelectedPartACode))
            {
                DialogHelper.ShowWarning("กรุณาเลือกข้อมูลให้ครบถ้วนก่อนบันทึกครับ");
                return;
            }

            // ใช้ DialogHelper สำหรับยืนยัน
            if (!DialogHelper.ShowConfirm($"ต้องการบันทึกการตั้งค่า Min: {MinDays}, Max: {MaxDays} สำหรับ {SelectedPartACode} ใช่หรือไม่?"))
                return;

            int.TryParse(MinDays, out int minD);
            int.TryParse(MaxDays, out int maxD);

            try
            {
                bool isSaved = _maxMinService.SaveOrUpdateCalcConfig(SelectedCustomer, _currentPtId, SelectedPartACode, minD, maxD, _currentUserEmpId);

                if (isSaved)
                {
                    // บันทึก Log ลงฐานข้อมูล
                    string logMsg = $"| Update MaxMin : Min={minD}, Max={maxD}";
                    LogService.WriteLog(_currentUserEmpId, "EDIT_MAXMIN", logMsg, SelectedPartACode);

                    // แจ้งเตือนสำเร็จ
                    DialogHelper.ShowSuccess("บันทึกข้อมูลเรียบร้อยแล้ว");

                    _selectedViewRow = null;
                    _selectedEditRow = null;
                    LoadPartMasterFromDatabase();
                    ExecuteClear(null);
                }
            }
            catch (Exception ex)
            {
                // แจ้งเตือนข้อผิดพลาด
                DialogHelper.ShowError($"เกิดข้อผิดพลาด:\n{ex.Message}");
            }
        }

        private void ExecuteClear(object parameter)
        {
            SelectedCustomer = null;
            SelectedPartACode = null;
            PartCode = string.Empty;
            PartNo = string.Empty;
            PartName = string.Empty;
            OrderQty = "";
            WorkDays = "";
            MinDays = "";
            MaxDays = "";
            CalculatedMinBox = 0;
            CalculatedMaxBox = 0;
            _selectedViewRow = null;
            _selectedEditRow = null;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}