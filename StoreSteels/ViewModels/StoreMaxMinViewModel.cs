// Store ( Max - Min )
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
        public ObservableCollection<string> Categories { get; set; }

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

        // เดิมชื่อ SelectedCustomer - schema ใหม่กรอง/จัดกลุ่มด้วย Category (ประเภท) แทน Customer
        private string _selectedCategory = "ALL CATEGORIES";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set { _selectedCategory = value; OnPropertyChanged(); }
        }

        #endregion

        public StoreMaxMinViewModel()
        {
            Products = new ObservableCollection<StoreProductModel>();
            Categories = new ObservableCollection<string>(_service.GetCategories());

            GroupedProducts = CollectionViewSource.GetDefaultView(Products);
            GroupedProducts.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
            GroupedProducts.SortDescriptions.Add(
                new SortDescription("PartCode", ListSortDirection.Ascending));
        }

        #region === [ Function : ProcessUpdate ] ===

        public async Task<bool> ProcessUpdate(StoreProductModel product, StoreProductModel originalProduct)
        {
            if (product == null || CurrentUser == null) return false;

            if (product.Remark != originalProduct.Remark)
            {
                LogService.WriteLog(CurrentUser.UserId, "UPDATE_REMARK",
                    $"| Remark: {originalProduct.Remark ?? ""} -> {product.Remark ?? ""}", product.PartCode);
            }

            var stkPermission = CurrentUser?.Permissions?.FirstOrDefault(p => p.SystemId == "STK");
            if (stkPermission != null && stkPermission.CanEdit)
            {
                var changes = new List<string>();
                if (product.Max != originalProduct.Max) changes.Add($"MAX: {originalProduct.Max}->{product.Max}");
                if (product.Min != originalProduct.Min) changes.Add($"MIN: {originalProduct.Min}->{product.Min}");
                if (product.Qty != originalProduct.Qty) changes.Add($"QTY(KG): {originalProduct.Qty}->{product.Qty}");

                if (changes.Count > 0)
                {
                    LogService.WriteLog(CurrentUser.UserId, "UPDATE_PRODUCT_MASTER",
                        $"| Changes: {string.Join(", ", changes)}", product.PartCode);
                }

                return await Task.Run(() =>
                {
                    int maxVal = int.TryParse(product.Max, out int ma) ? ma : 0;
                    int minVal = int.TryParse(product.Min, out int mi) ? mi : 0;
                    double? qtyVal = double.TryParse(product.Qty, out double q) ? q : (double?)null;
                    return _service.UpdateProductMaster(product.PartCode, product.Remark, maxVal, minVal, qtyVal);
                });
            }

            return _service.UpdateRemark(product.PartCode, product.Remark);
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

                string catFilter = SelectedCategory == "ALL CATEGORIES" ? "" : SelectedCategory;

                var newData = _service.GetProducts(
                    searchKeyword,
                    catFilter,
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
                    .Where(x => !string.IsNullOrWhiteSpace(x.PartCode))
                    .Select(x => x.PartCode)
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
                    // ToLookup รองรับ key ซ้ำได้ - วน loop อัปเดตทุก row ที่ PartCode ตรงกันพร้อมกันทุกครั้ง
                    var lookup = Products
                        .Where(x => !string.IsNullOrWhiteSpace(x.PartCode))
                        .ToLookup(x => x.PartCode);

                    foreach (var newItem in freshData)
                    {
                        foreach (var existingItem in lookup[newItem.PartCode])
                        {
                            if (existingItem.Qty != newItem.Qty)
                                existingItem.Qty = newItem.Qty;

                            if (existingItem.Max != newItem.Max)
                                existingItem.Max = newItem.Max;

                            if (existingItem.Min != newItem.Min)
                                existingItem.Min = newItem.Min;

                            if (!existingItem.IsRemarkEditing && existingItem.Remark != newItem.Remark)
                                existingItem.Remark = newItem.Remark;

                            if (existingItem.StockStatus != newItem.StockStatus)
                                existingItem.StockStatus = newItem.StockStatus;
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
