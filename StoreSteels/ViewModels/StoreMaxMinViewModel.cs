//using Microsoft.Data.SqlClient;
//using StoreSteels.Converters;
//using StoreSteels.Core;
//using StoreSteels.Helpers;
//using StoreSteels.Models;
//using StoreSteels.Services;
//using System;
//using System.Collections.ObjectModel;
//using System.ComponentModel;
//using System.Linq;
//using System.Runtime.CompilerServices;
//using System.Threading.Tasks;
//using System.Windows.Data;

//namespace StoreSteels.ViewModels
//{
//    public class StoreMaxMinViewModel : INotifyPropertyChanged
//    {
//        #region === [ INotifyPropertyChanged & OnPropertyChanged ] ===

//        // 👑 INotifyPropertyChanged Pattern or Property Notification System แจ้ง UI ว่าค่ามีการเปลี่ยน เพื่อให้ UI Update อัตโนมัติ
//        public event PropertyChangedEventHandler PropertyChanged;


//        // 👑 Observable State Collection or Bindable Collection เพิ่ม/ลบข้อมูลแล้ว UI รู้ทันที WPF จะ Refresh Auto
//        protected void OnPropertyChanged([CallerMemberName] string name = null)
//        {
//            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
//        }

//        public ObservableCollection<StoreProductModel> Products { get; set; }
//        public ObservableCollection<string> Customers { get; set; }


//        // 👑 Service Layer or Data Access Service : ดึวข้อมูล จัดการ API Database หรือ Business Logic ต่างๆ แยกความรับผิดชอบให้ชัดเจน
//        private readonly StoreProductService _service = new StoreProductService();


//        // 👑 Session State or User Context : ข้อมูลผู้ใช้ปัจจุบัน ใช้ในการตรวจสอบสิทธิ์การแก้ไขข้อมูล และบันทึก Log การเปลี่ยนแปลงต่างๆ
//        public UserSession CurrentUser { get; set; }


//        // 👑 ICollectionView ( WPF ระดับ Advanced ) : View Projection & Collection View Layer ใช้สำหรับ Group, Filter, Sort, Search โดยไม่แก้ข้อมูลต้นฉบับ
//        public ICollectionView GroupedProducts { get; set; }


//        // Pagination State & Loading State
//        private int _currentOffset = 0;  // 👑 Concurrency Guard
//        private bool _isLoading = false; // 👑 เพิ่มตัวแปรสำหรับล็อก ป้องกันการเรียกซ้ำซ้อน
//        private bool _hasMoreData = true; // 👑 เช็กว่าข้อมูลหมดคลังหรือยัง


//        // 👑 รายการที่แสดงเฉพาะรายการ ปุ่ม Max ปุ่ม Min เเละ เลือกเฉพาะรายลูกค้า
//        private string _selectedFilterType = ""; // "", "OVER_MAX", "UNDER_MIN"
//        public string SelectedFilterType
//        {
//            get => _selectedFilterType;
//            set { _selectedFilterType = value; OnPropertyChanged(); }
//        }


//        // 👑 รายการที่แสดงรายการลูกค้าทั้งหมด
//        private string _selectedCustomer = "ALL CUSTOMERS";
//        public string SelectedCustomer
//        {
//            get => _selectedCustomer;
//            set { _selectedCustomer = value; OnPropertyChanged(); }
//        }

//        #endregion

//        public StoreMaxMinViewModel()
//        {
//            Products = new ObservableCollection<StoreProductModel>();
//            Customers = new ObservableCollection<string>(_service.GetCustomers()); // โหลดข้อมูลลูกค้า

//            // 👑 เพิ่มเติมอัพเดทข้อมูลเพียงเเค่ครั้งเดียวเท่านั้น
//            //GroupedProducts = CollectionViewSource.GetDefaultView(Products);
//            //GroupedProducts.GroupDescriptions.Add(
//            //    new PropertyGroupDescription("CustomerCode"));
//            // 👑 เพิ่มเติมอัพเดทข้อมูลเพียงเเค่ครั้งเดียวเท่านั้น
//            GroupedProducts = CollectionViewSource.GetDefaultView(Products);
//            // 1. แบ่งกลุ่มตามลูกค้า
//            GroupedProducts.GroupDescriptions.Add(new PropertyGroupDescription("CustomerCode"));

//            // 2. 🔥 เพิ่มตรงนี้: เรียงลำดับตาม PartACode จากน้อยไปมากภายในกลุ่ม
//            GroupedProducts.SortDescriptions.Add(
//                new System.ComponentModel.SortDescription("PartACode", System.ComponentModel.ListSortDirection.Ascending));
//        }

//        #region === [ Function : ProcessUpdate ]

//        public async Task<bool> ProcessUpdate(StoreProductModel product, StoreProductModel originalProduct)
//        {
//            if (product == null || CurrentUser == null) return false;

//            // 1. เก็บ Log Remark (ทำได้ทุกคน)
//            if (product.Remark != originalProduct.Remark)
//            {
//                LogService.WriteLog(CurrentUser.UserId, "UPDATE_REMARK",
//                    $"| Remark: {originalProduct.Remark ?? ""} -> {product.Remark ?? ""}", product.PartACode);
//            }

//            // 2. เก็บ Log ข้อมูล Master (ถ้ามีสิทธิ์)
//            var stkPermission = CurrentUser?.Permissions?.FirstOrDefault(p => p.SystemId == "STK");
//            if (stkPermission != null && stkPermission.CanEdit)
//            {
//                List<string> changes = new List<string>();
//                if (product.Max != originalProduct.Max) changes.Add($"MAX: {originalProduct.Max}->{product.Max}");
//                if (product.Min != originalProduct.Min) changes.Add($"MIN: {originalProduct.Min}->{product.Min}");
//                if (product.QtyStkb != originalProduct.QtyStkb) changes.Add($"STOCKBOX: {originalProduct.QtyStkb}->{product.QtyStkb}");
//                if (product.Stock != originalProduct.Stock) changes.Add($"STOCK: {originalProduct.Stock}->{product.Stock}");

//                if (changes.Count > 0)
//                {
//                    LogService.WriteLog(CurrentUser.UserId, "UPDATE_PRODUCT_MASTER",
//                        $"| Changes: {string.Join(", ", changes)}", product.PartACode);
//                }

//                return await Task.Run(() => {
//                    // ใช้ค่า string ที่ได้จาก Model ตรงๆ แล้วแปลงแบบไม่สน null
//                    int maxVal = int.TryParse(product.Max, out int ma) ? ma : 0;
//                    int minVal = int.TryParse(product.Min, out int mi) ? mi : 0;
//                    double? qtyStkbVal = double.TryParse(product.QtyStkb, out double qb) ? Math.Ceiling(qb) : (double?)null;
//                    int? stockVal = int.TryParse(product.Stock, out int st) ? st : (int?)null;
//                    return _service.UpdateProductMaster(product.PartACode, product.Remark, maxVal, minVal, qtyStkbVal, stockVal);
//                });
//            }

//            return _service.UpdateRemark(product.PartACode, product.Remark);
//        }

//        #endregion

//        #region === [ Function : LoadData ]

//        public void LoadData(string searchKeyword = "", bool isLoadMore = false)
//        {
//            if (_isLoading || (isLoadMore && !_hasMoreData))
//                return;

//            try
//            {
//                _isLoading = true;

//                int pageSize;

//                if (!isLoadMore)
//                {
//                    _currentOffset = 0;
//                    _hasMoreData = true;

//                    Products.Clear();

//                    pageSize = 50;
//                }
//                else
//                {
//                    pageSize = 30;
//                }

//                string custFilter =
//                    SelectedCustomer == "ALL CUSTOMERS"
//                    ? ""
//                    : SelectedCustomer;

//                var newData = _service.GetProducts(
//                    searchKeyword,
//                    custFilter,
//                    SelectedFilterType,
//                    _currentOffset,
//                    pageSize);

//                if (newData == null || newData.Count == 0)
//                {
//                    _hasMoreData = false;
//                    return;
//                }

//                foreach (var item in newData)
//                {
//                    Products.Add(item);
//                }

//                _currentOffset += newData.Count;

//                if (newData.Count < pageSize)
//                {
//                    _hasMoreData = false;
//                }

//            }
//            finally
//            {
//                _isLoading = false;
//            }
//        }

//        #endregion

//        #region === [ Function : Real-Timer Updates ]
//        public async Task UpdateStockFromDbAsync()
//        {
//            if (_isLoading || Products == null || Products.Count == 0)
//                return;

//            try
//            {
//                var currentPartCodes = Products
//                    .Where(x => !string.IsNullOrWhiteSpace(x.PartACode))
//                    .Select(x => x.PartACode)
//                    .Distinct()
//                    .ToList();

//                if (currentPartCodes.Count == 0)
//                    return;

//                var freshData = await Task.Run(() =>
//                    _service.GetMinimalStockUpdates(currentPartCodes));

//                if (freshData == null || freshData.Count == 0)
//                    return;

//                await App.Current.Dispatcher.InvokeAsync(() =>
//                {
//                    var lookup = Products
//                        .Where(x => !string.IsNullOrWhiteSpace(x.PartACode))
//                        .ToDictionary(x => x.PartACode);

//                    foreach (var newItem in freshData)
//                    {
//                        if (lookup.TryGetValue(newItem.PartACode, out var existingItem))
//                        {
//                            // =====================================================
//                            // STOCK BOX
//                            // =====================================================
//                            if (existingItem.QtyStkb != newItem.QtyStkb)
//                            {
//                                existingItem.QtyStkb = newItem.QtyStkb;
//                            }

//                            // =====================================================
//                            // STOCK PCS
//                            // =====================================================
//                            if (existingItem.Stock != newItem.Stock)
//                            {
//                                existingItem.Stock = newItem.Stock;
//                            }

//                            // =====================================================
//                            // MAX / MIN
//                            // =====================================================
//                            if (existingItem.Max != newItem.Max)
//                            {
//                                existingItem.Max = newItem.Max;
//                            }

//                            if (existingItem.Min != newItem.Min)
//                            {
//                                existingItem.Min = newItem.Min;
//                            }

//                            // =====================================================
//                            // REMARK (กันไม่ให้ทับตอนพิมพ์)
//                            // =====================================================
//                            if (!existingItem.IsRemarkEditing)
//                            {
//                                if (existingItem.Remark != newItem.Remark)
//                                {
//                                    existingItem.Remark = newItem.Remark;
//                                }
//                            }

//                            // =====================================================
//                            // 👑 STOCK STATUS (อัปเดตเพื่อให้สีบนหน้าจอเปลี่ยน Real-time)
//                            // =====================================================
//                            if (existingItem.StockStatus != newItem.StockStatus)
//                            {
//                                existingItem.StockStatus = newItem.StockStatus;
//                            }
//                        }
//                    }
//                });
//            }
//            catch (Exception ex)
//            {
//                System.Diagnostics.Debug.WriteLine($"Error in UpdateStockFromDbAsync: {ex.Message}");
//            }
//        }

//        #endregion

//    }
//}
    using Microsoft.Data.SqlClient;
    using StoreSteels.Converters;
    using StoreSteels.Core;
    using StoreSteels.Helpers;
    using StoreSteels.Models;
    using StoreSteels.Services;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using System.Windows.Data;

    namespace StoreSteels.ViewModels
    {
        public class StoreMaxMinViewModel : INotifyPropertyChanged
        {
            #region === [ INotifyPropertyChanged ] ===

            public event PropertyChangedEventHandler PropertyChanged;

            protected void OnPropertyChanged([CallerMemberName] string name = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }

            public ObservableCollection<StoreProductModel> Products { get; set; }
            public ObservableCollection<string> Customers { get; set; }

            private readonly StoreProductService _service = new StoreProductService();

            public UserSession CurrentUser { get; set; }

            public ICollectionView GroupedProducts { get; set; }

            private int _currentOffset = 0;
            private bool _isLoading = false;
            private bool _isRealTimeUpdating = false;
            private bool _hasMoreData = true;

            private string _selectedFilterType = "";
            public string SelectedFilterType
            {
                get => _selectedFilterType;
                set { _selectedFilterType = value; OnPropertyChanged(); }
            }

            private string _selectedCustomer = "ALL CUSTOMERS";
            public string SelectedCustomer
            {
                get => _selectedCustomer;
                set { _selectedCustomer = value; OnPropertyChanged(); }
            }

            #endregion

            public StoreMaxMinViewModel()
            {
                Products = new ObservableCollection<StoreProductModel>();
                Customers = new ObservableCollection<string>(_service.GetCustomers());

                GroupedProducts = CollectionViewSource.GetDefaultView(Products);
                GroupedProducts.GroupDescriptions.Add(new PropertyGroupDescription("CustomerCode"));
                GroupedProducts.SortDescriptions.Add(
                    new SortDescription("PartACode", ListSortDirection.Ascending));
            }

            #region === [ Function : ProcessUpdate ] ===

            public async Task<bool> ProcessUpdate(StoreProductModel product, StoreProductModel originalProduct)
            {
                if (product == null || CurrentUser == null) return false;

                if (product.Remark != originalProduct.Remark)
                {
                    LogService.WriteLog(CurrentUser.UserId, "UPDATE_REMARK",
                        $"| Remark: {originalProduct.Remark ?? ""} -> {product.Remark ?? ""}", product.PartACode);
                }

                var stkPermission = CurrentUser?.Permissions?.FirstOrDefault(p => p.SystemId == "STK");
                if (stkPermission != null && stkPermission.CanEdit)
                {
                    var changes = new List<string>();
                    if (product.Max != originalProduct.Max) changes.Add($"MAX: {originalProduct.Max}->{product.Max}");
                    if (product.Min != originalProduct.Min) changes.Add($"MIN: {originalProduct.Min}->{product.Min}");
                    if (product.QtyStkb != originalProduct.QtyStkb) changes.Add($"STOCKBOX: {originalProduct.QtyStkb}->{product.QtyStkb}");
                    if (product.Stock != originalProduct.Stock) changes.Add($"STOCK: {originalProduct.Stock}->{product.Stock}");

                    if (changes.Count > 0)
                    {
                        LogService.WriteLog(CurrentUser.UserId, "UPDATE_PRODUCT_MASTER",
                            $"| Changes: {string.Join(", ", changes)}", product.PartACode);
                    }

                    return await Task.Run(() =>
                    {
                        int maxVal = int.TryParse(product.Max, out int ma) ? ma : 0;
                        int minVal = int.TryParse(product.Min, out int mi) ? mi : 0;
                        double? qtyStkbVal = double.TryParse(product.QtyStkb, out double qb) ? Math.Ceiling(qb) : (double?)null;
                        int? stockVal = int.TryParse(product.Stock, out int st) ? st : (int?)null;
                        return _service.UpdateProductMaster(product.PartACode, product.Remark, maxVal, minVal, qtyStkbVal, stockVal);
                    });
                }

                return _service.UpdateRemark(product.PartACode, product.Remark);
            }

            #endregion

            #region === [ Function : LoadData ] ===

            public void LoadData(string searchKeyword = "", bool isLoadMore = false)
            {
                if (_isLoading || (isLoadMore && !_hasMoreData))
                    return;

                try
                {
                    _isLoading = true;

                    int pageSize;

                    if (!isLoadMore)
                    {
                        _currentOffset = 0;
                        _hasMoreData = true;
                        Products.Clear();
                        pageSize = 50;
                    }
                    else
                    {
                        pageSize = 30;
                    }

                    string custFilter = SelectedCustomer == "ALL CUSTOMERS" ? "" : SelectedCustomer;

                    var newData = _service.GetProducts(
                        searchKeyword,
                        custFilter,
                        SelectedFilterType,
                        _currentOffset,
                        pageSize);

                    if (newData == null || newData.Count == 0)
                    {
                        _hasMoreData = false;
                        return;
                    }

                    foreach (var item in newData)
                        Products.Add(item);

                    _currentOffset += newData.Count;

                    if (newData.Count < pageSize)
                        _hasMoreData = false;
                }
                finally
                {
                    _isLoading = false;
                }
            }

            #endregion

            #region === [ Function : Real-Time Updates ] ===

            public async Task UpdateStockFromDbAsync()
            {
                if (_isRealTimeUpdating || Products == null || Products.Count == 0)
                    return;

                try
                {
                    _isRealTimeUpdating = true;

                    var currentPartCodes = Products
                        .Where(x => !string.IsNullOrWhiteSpace(x.PartACode))
                        .Select(x => x.PartACode)
                        .Distinct()
                        .ToList();

                    if (currentPartCodes.Count == 0)
                        return;

                    var freshData = await Task.Run(() =>
                        _service.GetMinimalStockUpdates(currentPartCodes));

                    if (freshData == null || freshData.Count == 0)
                        return;

                    await App.Current.Dispatcher.InvokeAsync(() =>
                    {
                        // ============================================================
                        // 👑 KEY FIX: ToLookup แทน ToDictionary
                        //
                        // สาเหตุที่ Real-time ไม่ทำงานมา 2 วัน:
                        // ToDictionary crash ทันทีที่เจอ PartACode ซ้ำ (เช่น A116-00001
                        // มีหลาย PT_CODE) → Exception ถูก swallow ใน try/catch →
                        // existingItem ไม่ถูก update เลยสักครั้ง
                        //
                        // ToLookup รองรับ key ซ้ำได้ → วน loop อัปเดตทุก row ที่
                        // PartACode ตรงกันพร้อมกันทุกครั้ง
                        // ============================================================
                        var lookup = Products
                            .Where(x => !string.IsNullOrWhiteSpace(x.PartACode))
                            .ToLookup(x => x.PartACode);

                        foreach (var newItem in freshData)
                        {
                            foreach (var existingItem in lookup[newItem.PartACode])
                            {
                                existingItem.IsSyncingFromDb = true;
                                try
                                {
                                    if (existingItem.QtyStkb != newItem.QtyStkb)
                                        existingItem.QtyStkb = newItem.QtyStkb;

                                    if (existingItem.Stock != newItem.Stock)
                                        existingItem.Stock = newItem.Stock;

                                    if (existingItem.Max != newItem.Max)
                                        existingItem.Max = newItem.Max;

                                    if (existingItem.Min != newItem.Min)
                                        existingItem.Min = newItem.Min;

                                    if (!existingItem.IsRemarkEditing && existingItem.Remark != newItem.Remark)
                                        existingItem.Remark = newItem.Remark;

                                    if (existingItem.StockStatus != newItem.StockStatus)
                                        existingItem.StockStatus = newItem.StockStatus;
                                }
                                finally
                                {
                                    existingItem.IsSyncingFromDb = false;
                                }
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in UpdateStockFromDbAsync: {ex.Message}");
                }
                finally
                {
                    _isRealTimeUpdating = false;
                }
            }

            #endregion
        }
    }