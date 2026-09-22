using Microsoft.Win32;
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
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace StoreSteels.ViewModels
{
    public class ProductControlViewModel : INotifyPropertyChanged
    {
        private readonly ProductControlService _qrService = new ProductControlService();

        private ProductControlModel _selectedProduct;
        public ProductControlModel SelectedProduct
        {
            get => _selectedProduct;
            set { _selectedProduct = value; OnPropertyChanged(); }
        }

        private ProductControlModel _inputModel = new ProductControlModel();
        public ProductControlModel InputModel
        {
            get => _inputModel;
            set { _inputModel = value; OnPropertyChanged(); }
        }

        public ICollectionView GroupedProducts { get; private set; }
        public UserSession CurrentUser { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ObservableCollection<ProductControlModel> Products { get; set; } = new ObservableCollection<ProductControlModel>();

        private System.Threading.CancellationTokenSource _searchCts;

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                _ = SearchWithDelayAsync(_searchText);
            }
        }

        public void SelectProductImage()
        {
            if (SelectedProduct == null) PrepareAddProduct();

            // 🎯 เปลี่ยนมาเช็คความว่างเปล่าที่ PartACode แทนครับ
            if (string.IsNullOrWhiteSpace(SelectedProduct.PartACode))
            {
                DialogHelper.ShowError("กรุณากรอกรหัสสินค้าจริง (Part A Code) ก่อนเลือกรูปภาพครับ");
                return;
            }

            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpeg;*.jpg)|*.png;*.jpeg;*.jpg"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string sourcePath = openFileDialog.FileName;
                    string extension = System.IO.Path.GetExtension(sourcePath);

                    // 🎯 ตั้งชื่อไฟล์ใหม่ด้วย PartACode เพื่อป้องกันชื่อซ้ำ
                    string newFileName = SelectedProduct.PartACode + extension;
                    string targetFolder = @"\\192.168.10.56\ProgramCHR\2. Store Only\StoreSteels\Image";

                    if (!System.IO.Directory.Exists(targetFolder))
                        System.IO.Directory.CreateDirectory(targetFolder);

                    string destPath = System.IO.Path.Combine(targetFolder, newFileName);

                    SelectedProduct.ImageFileName = string.Empty;

                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    if (System.IO.File.Exists(destPath))
                    {
                        System.IO.File.Delete(destPath);
                    }

                    System.IO.File.Copy(sourcePath, destPath, true);
                    SelectedProduct.ImageFileName = newFileName;

                    DialogHelper.ShowSuccess("อัปโหลดรูปภาพสำเร็จ");
                }
                catch (Exception ex)
                {
                    DialogHelper.ShowError("ไม่สามารถอัปโหลดรูปได้\n" + ex.Message);
                }
            }
        }

        private void PrepareAddProduct()
        {
            SelectedProduct = new ProductControlModel
            {
                PartCode = "",
                PartName = "",
                PartACode = "", // 🎯
                ImageFileName = string.Empty
            };
        }

        private void InitializeGrouping()
        {
            if (GroupedProducts == null)
            {
                GroupedProducts = CollectionViewSource.GetDefaultView(Products);
                if (GroupedProducts.GroupDescriptions.Count == 0)
                {
                    GroupedProducts.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
                }
                if (GroupedProducts.SortDescriptions.Count == 0)
                {
                    GroupedProducts.SortDescriptions.Add(new SortDescription("Category", ListSortDirection.Ascending));
                }
            }
            else
            {
                GroupedProducts.Refresh();
            }
        }

        private async Task SearchWithDelayAsync(string keyword)
        {
            _searchCts?.Cancel();
            _searchCts = new System.Threading.CancellationTokenSource();

            try
            {
                await Task.Delay(300, _searchCts.Token);
                LoadData(keyword);
            }
            catch (TaskCanceledException) { }
        }

        public ProductControlViewModel()
        {
            PrepareAddProduct();
            LoadData();
        }

        private int _currentOffset = 0;
        private const int PageSize = 100;

        public async void LoadData(string keyword = "", bool isLoadMore = false)
        {
            if (!isLoadMore) _currentOffset = 0;

            try
            {
                var data = await Task.Run(() => _qrService.GetInventoryForQR(keyword?.Trim()));
                var paged = data.Skip(_currentOffset).Take(PageSize).ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!isLoadMore) Products.Clear();

                    foreach (var item in paged)
                    {
                        Products.Add(item);
                    }

                    if (GroupedProducts == null) InitializeGrouping();
                    else GroupedProducts.Refresh();
                });

                _currentOffset += paged.Count;
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError("โหลดข้อมูลไม่สำเร็จ: " + ex.Message);
            }
        }

        // 🎯 ✅ แก้ไขระบบ UPDATE ใน ViewModel เพื่อดึงและแนบข้อมูล CustomerCode, ModelCode, PartNo ส่งเข้าหลังบ้านครบทุกตัว
        public async Task<bool> UpdateProductAsync(ProductControlModel model, string oldACode)
        {
            if (CurrentUser == null || string.IsNullOrEmpty(oldACode)) return false;

            try
            {
                string uid = CurrentUser.UserId;
                int psz = int.TryParse(model.PackSize, out int rPsz) ? rPsz : 0;

                // เช็คการเปลี่ยนรหัส ACode
                if (oldACode != model.PartACode)
                {
                    bool isDuplicate = await _qrService.CheckDuplicateCodeAsync(model.PartACode);
                    if (isDuplicate)
                    {
                        DialogHelper.ShowError($"ไม่สามารถเปลี่ยนรหัสเป็น {model.PartACode} ได้ เนื่องจากรหัส PART A นี้มีในระบบแล้วครับ");
                        return false;
                    }
                }

                var targetRow = Products.FirstOrDefault(p => p.PartACode == oldACode);
                string changeLog = "";

                if (targetRow != null)
                {
                    // 🎯 ใช้ Helper ที่เราเพิ่งทำกันไว้ (แต่ต้องเอา Max/Min ออกจาก Helper นั้นด้วยนะครับ)
                    changeLog = GetChangeLogSimplified(targetRow, model);
                }

                if (string.IsNullOrEmpty(changeLog)) changeLog = "No changes detected";
                LogService.WriteLog(uid, "EDIT_PART", $" | Edited Details : {changeLog}", model.PartACode);

                // 🎯 ส่งค่าเข้า Service (สังเกตว่าตัด max, min ออกไปแล้ว)
                bool success = await _qrService.UpdateExistingPartAsync(
                    oldACode,
                    model.PartACode,
                    model.PartCode,
                    model.PartName,
                    psz,
                    model.QRCodeData,
                    model.Category,
                    uid,
                    model.ImageFileName,
                    model.CustomerCode,
                    model.Location
                );

                if (success && targetRow != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        int index = Products.IndexOf(targetRow);
                        model.IsShow = targetRow.IsShow;
                        model.Stock = targetRow.Stock;
                        model.IsActive = targetRow.IsActive;

                        Products.RemoveAt(index);
                        Products.Insert(index, model);
                    });
                }
                return success;
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError($"อัปเดตข้อมูลไม่สำเร็จ:\n{ex.Message}");
                return false;
            }
        }

        private string GetChangeLogSimplified(ProductControlModel oldVal, ProductControlModel newVal)
        {
            var diffs = new List<string>();

            if (oldVal.PartCode != newVal.PartCode) diffs.Add($"PDCODE: {oldVal.PartCode}->{newVal.PartCode}");
            if (oldVal.PartName != newVal.PartName) diffs.Add($"PTNAME: {oldVal.PartName}->{newVal.PartName}");
            if (oldVal.PackSize != newVal.PackSize) diffs.Add($"PZ: {oldVal.PackSize}->{newVal.PackSize}");
            if (oldVal.Category != newVal.Category) diffs.Add($"CAT: {oldVal.Category}->{newVal.Category}");
            if (oldVal.CustomerCode != newVal.CustomerCode) diffs.Add($"SUPPLIER: {oldVal.CustomerCode}->{newVal.CustomerCode}");
            if (oldVal.Location != newVal.Location) diffs.Add($"BIN: {oldVal.Location}->{newVal.Location}");
            if (oldVal.QRCodeData != newVal.QRCodeData) diffs.Add($"QR: {oldVal.QRCodeData}->{newVal.QRCodeData}");

            return diffs.Count > 0 ? string.Join("  |  ", diffs) : null;
        }

        // ลงทะเบียนสินค้าใหม่ (INSERT)
        public async Task<bool> RegisterNewProductAsync(ProductControlModel model)
        {
            if (CurrentUser == null)
            {
                DialogHelper.ShowError("ไม่พบข้อมูลผู้ใช้งานระบบ (Session Expired) กรุณาเข้าสู่ระบบใหม่อีกครั้ง");
                return false;
            }

            try
            {
                string uid = CurrentUser.UserId;
                int psz = int.TryParse(model.PackSize, out int rPsz) ? rPsz : 0;
                int max = int.TryParse(model.Max, out int rMax) ? rMax : 0;
                int min = int.TryParse(model.Min, out int rMin) ? rMin : 0;

                // 🎯 เช็คค่าซ้ำด้วยค่า PartACode
                bool existsInDb = await _qrService.CheckDuplicateCodeAsync(model.PartACode);
                if (existsInDb)
                {
                    DialogHelper.ShowError($"รหัสสินค้า Part A Code: {model.PartACode} นี้ถูกใช้ไปแล้วในฐานข้อมูลครับ");
                    return false;
                }

                string fullDetail = $"SUPPLIER: {model.CustomerCode}  | CAT: {model.Category}  | PDCODE : {model.PartCode}  | BIN: {model.Location}  | PDNAME: {model.PartName}  | PZ : {psz}";
                LogService.WriteLog(uid, "REGISTER_PART", $"Registered New Part: {fullDetail}", model.PartACode);

                bool success = await _qrService.InsertNewPartAsync(
                    model.PartCode, model.PartName, psz, model.QRCodeData, max, min, model.Category, model.ImageFileName,
                    model.CustomerCode, model.Location
                );

                if (success)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        model.IsShow = true;
                        model.Stock = "0";
                        model.IsActive = true;

                        Products.Insert(0, model);
                    });
                }
                else
                {
                    DialogHelper.ShowError("ฐานข้อมูลปฏิเสธการบันทึกข้อมูล กรุณาตรวจสอบการกรอกข้อมูลให้ครบถ้วน");
                }

                return success;
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError("ลงทะเบียนสินค้าใหม่ไม่สำเร็จ: " + ex.Message);
                return false;
            }
        }

        // ปรับฟังก์ชัน SaveToDb ให้ใช้ oldACode แทน oldCode
        public async Task<bool> SaveToDb(ProductControlModel model, bool isUpdate, string oldCode = "")
        {
            if (CurrentUser == null)
            {
                DialogHelper.ShowError("ไม่สามารถบันทึกได้เนื่องจาก Session ผู้ใช้งานเป็นว่าง (Null)");
                return false;
            }

            try
            {
                if (isUpdate)
                {
                    // 🎯 ส่ง oldCode เข้าไปทำงานที่ UpdateProductAsync (ซึ่งรับพารามิเตอร์ oldACode)
                    return await UpdateProductAsync(model, oldCode);
                }
                else
                {
                    bool existsInList = Products.Any(p => p.PartACode == model.PartACode);
                    if (existsInList)
                    {
                        DialogHelper.ShowError($"รหัสสินค้า {model.PartACode} มีอยู่ในรายการ");
                        return false;
                    }

                    return await RegisterNewProductAsync(model);
                }
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError("ระบบบันทึกเกิดข้อผิดพลาด: " + ex.Message);
                return false;
            }
        }

        // 🎯 [จุดแก้ไขแก้ไขบั๊ก Show/Hide ทั้งตาราง]
        public bool ToggleStatus(ProductControlModel product)
        {
            if (CurrentUser == null || product == null) return false;
            string uid = CurrentUser.UserId;
            bool newStatus = !product.IsShow;

            // 🎯 เปลี่ยนพารามิเตอร์ส่งตัวระบุหลักไปเป็น product.PartACode 
            if (_qrService.UpdateShowStatus(product.PartACode, newStatus, uid))
            {
                product.IsShow = newStatus;
                string statusLabel = newStatus ? "Enabled" : "Disabled";
                LogService.WriteLog(uid, "TOGGLE_STATUS", $" | Set show status to {statusLabel}  | PDNAME : {product.PartName}", product.PartACode);
                return true;
            }
            return false;
        }

        // 🎯 ลบสินค้าโดยอิงตาม PartACode
        public async Task<bool> DeleteProductAsync(ProductControlModel product)
        {
            if (CurrentUser == null || product == null) return false;
            string uid = CurrentUser.UserId;

            // 🎯 สั่งทำงาน Delete ผ่าน PartACode
            bool success = await Task.Run(() => _qrService.DeletePart(product.PartACode, uid));

            if (success)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Products.Remove(product);
                });

                LogService.WriteLog(uid, "DELETE_PART", $"Deleted PDNAME : {product.PartName}", product.PartACode);
                return true;
            }
            return false;
        }

        // 🎯 รันกระบวนการสร้าง QR Code ด้วยการอัปเดตผ่าน PartACode
        public async Task<bool> ProcessGenerateQR(ProductControlModel product)
        {
            if (product == null || CurrentUser == null) return false;
            if (CurrentUser.UserLevel > 3) return false;

            string uid = CurrentUser.UserId;
            string pName = product.PartName;
            string pACode = product.PartACode; // 🎯 เปลี่ยนมาจับตัวแปร PartACode แทน

            return await Task.Run(() =>
            {
                try
                {
                    bool success = _qrService.UpdateQRCodeStatus(pACode);
                    if (success)
                    {
                        LogService.WriteLog(uid, "GENERATE_QR", $"User generated QR Code for Part: {pName}", pACode);
                    }
                    return success;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"QR Error: {ex.Message}");
                    return false;
                }
            });
        }
    }
}