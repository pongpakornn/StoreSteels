//using Microsoft.Extensions.Logging;
//using StoreSteels.Helpers;
//using StoreSteels.Models;
//using StoreSteels.Services;
//using System;
//using System.Collections.ObjectModel;
//using System.ComponentModel;
//using System.Linq;
//using System.Runtime.CompilerServices;
//using System.Threading.Tasks;
//using System.Windows;

//namespace StoreSteels.ViewModels
//{
//    public class ScanInViewModel : INotifyPropertyChanged
//    {
//        private readonly ScanService _scanService = new ScanService();

//        public UserSession CurrentUser { get; set; }
//        public ObservableCollection<ScanItemModel> ScannedItems { get; set; } = new ObservableCollection<ScanItemModel>();
//        public ObservableCollection<ScanItemModel> HistoryItems { get; set; } = new ObservableCollection<ScanItemModel>();

//        #region Properties สำหรับผูกกับ UI
//        private System.Windows.Media.ImageSource _showProductImage;
//        public System.Windows.Media.ImageSource ShowProductImage
//        {
//            get => _showProductImage;
//            set { _showProductImage = value; OnPropertyChanged(); }
//        }

//        private string _showName;
//        public string ShowName { get => _showName; set { _showName = value; OnPropertyChanged(); } }

//        private string _showCode;
//        public string ShowCode { get => _showCode; set { _showCode = value; OnPropertyChanged(); } }

//        private int _showQtyValue;
//        public string ShowQty
//        {
//            get => _showQtyValue.ToString();
//            set { if (int.TryParse(value, out int res)) _showQtyValue = res; OnPropertyChanged(); }
//        }

//        private string _barcodeInput;
//        public string BarcodeInput
//        {
//            get => _barcodeInput;
//            set { _barcodeInput = value; OnPropertyChanged(); }
//        }
//        #endregion

//        public ScanInViewModel()
//        {
//            LoadTodayData();
//        }

//        private async void LoadTodayData()
//        {
//            try
//            {
//                var data = await Task.Run(() => _scanService.GetTodayTransactions());

//                Application.Current.Dispatcher.Invoke(() =>
//                {
//                    ScannedItems.Clear();
//                    HistoryItems.Clear();
//                    foreach (var item in data)
//                    {
//                        if (item.Status == "IN")
//                        {
//                            ScannedItems.Insert(0, item);
//                        }
//                        UpdateSummary(item);
//                    }
//                });
//            }
//            catch (Exception ex)
//            {
//                System.Diagnostics.Debug.WriteLine($"Load Error: {ex.Message}");
//            }
//        }


//        public async Task ProcessScan(string inputCode)
//        {
//            if (string.IsNullOrWhiteSpace(inputCode) || CurrentUser == null) return;

//            string finalSearchCode = inputCode.Trim();

//            // ✅ เก็บบาร์โค้ดดิบ "ทั้งชุด" ไว้ก่อนตัดส่วนหัวออก เพื่อบันทึกลง REF_NO
//            string rawBarcodeFull = finalSearchCode;

//            string uid = CurrentUser.UserId;

//            // =========================================================================
//            // ⚙️ [ระบบคัดแยก] สกัดเอา PartACode คลีนๆ (เช่น "A122-00059") เพื่อให้เสถียร ไม่หลุด Error
//            // =========================================================================
//            var parts = finalSearchCode.Split(new[] { '|', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

//            if (parts.Length > 0)
//            {
//                string firstChunk = parts[0].Trim();

//                if (firstChunk.Length > 2 && char.IsDigit(firstChunk[0]) && char.IsLetter(firstChunk[1]))
//                {
//                    finalSearchCode = firstChunk.Substring(1);
//                }
//                else
//                {
//                    finalSearchCode = firstChunk;
//                }
//            }
//            // =========================================================================

//            try
//            {
//                var result = await Task.Run(() =>
//                {
//                    var part = _scanService.GetPartByScan(finalSearchCode);
//                    if (part == null) return null;

//                    int originalQty = (int)part.Qty;

//                    // ✅ ส่ง rawBarcodeFull เข้าไปบันทึกลง REF_NO
//                    bool isSaved = _scanService.UpdateStock(part.PartId, part.PartCode, part.PartACode, originalQty, uid, rawBarcodeFull);
//                    if (isSaved)
//                    {
//                        LogService.WriteScanLog(uid, "SCAN_IN", part.PartCode, part.PartACode, originalQty);
//                        part.Qty = originalQty;
//                        return part;
//                    }
//                    return null;
//                });

//                if (result != null)
//                {
//                    ShowCode = result.PartACode;
//                    ShowName = result.PartName;
//                    ShowQty = result.Qty.ToString(); // หน้าจอแสดงผลยอดตามระบบเก่าของคุณนนท์

//                    // เส้นทางไฟล์รูปเครือข่าย IP เครื่องหลัก
//                    string baseFolder = @"\\192.168.10.56\ProgramCHR\2. Store Only\StoreSteels\ImageStore";
//                    //string baseFolder = @"C:\Users\pongp\Desktop\WorkMe\3. Project WPF\2. Program StoreSteels\1. ImageStore";
//                    string fileName = result.PartACode;
//                    string imgPath = System.IO.Path.Combine(baseFolder, $"{fileName}.png");

//                    if (!System.IO.File.Exists(imgPath))
//                    {
//                        imgPath = System.IO.Path.Combine(baseFolder, $"{fileName}.jpg");
//                    }

//                    if (!System.IO.File.Exists(imgPath))
//                    {
//                        imgPath = System.IO.Path.Combine(baseFolder, "no-image.png");
//                    }

//                    Application.Current.Dispatcher.Invoke(() =>
//                    {
//                        try
//                        {
//                            if (System.IO.File.Exists(imgPath))
//                            {
//                                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
//                                bitmap.BeginInit();
//                                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
//                                bitmap.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
//                                bitmap.UriSource = new Uri(imgPath, UriKind.Absolute);
//                                bitmap.EndInit();
//                                bitmap.Freeze();

//                                ShowProductImage = bitmap;
//                            }
//                            else
//                            {
//                                ShowProductImage = null;
//                            }
//                        }
//                        catch (Exception ex)
//                        {
//                            System.Diagnostics.Debug.WriteLine($"Load Image Error: {ex.Message}");
//                            ShowProductImage = null;
//                        }
//                    });

//                    var newItem = new ScanItemModel
//                    {
//                        PartId = result.PartId,
//                        PartCode = result.PartCode,
//                        PartName = result.PartName,
//                        PartNo = result.PartNo,
//                        PartACode = result.PartACode,
//                        Qty = (int)result.Qty, // ปรับ Type ให้เป็น int ป้องกันตัวแดงเตือนบน Model เดิม
//                        Status = "IN",
//                        UpdateTime = DateTime.Now,
//                        ProductImagePath = ShowProductImage
//                    };

//                    ScannedItems.Insert(0, newItem);

//                    if (ScannedItems.Count > 12)
//                    {
//                        ScannedItems.RemoveAt(ScannedItems.Count - 1);
//                    }
//                    UpdateSummary(newItem);
//                }
//                else
//                {
//                    DialogHelper.ShowError($"[รายการไม่สำเร็จ] ไม่พบข้อมูลสินค้าในระบบ หรือบาร์โค้ดยังไม่ได้ลงทะเบียน\nCode: {finalSearchCode}");
//                }
//            }
//            catch (Exception ex)
//            {
//                DialogHelper.ShowError("เกิดข้อผิดพลาด: " + ex.Message);
//            }
//            finally
//            {
//                BarcodeInput = string.Empty;
//            }
//        }


//        private void UpdateSummary(ScanItemModel item)
//        {
//            if (string.IsNullOrWhiteSpace(item.PartACode)) return;

//            // ดึงยอดคงคลังปัจจุบันจาก Database ผ่านตัวแปรไอดีสินค้า (PartId)
//            int actualCurrentStock = _scanService.GetInventoryBalance(item.PartId);

//            var existing = HistoryItems.FirstOrDefault(x => x.PartACode == item.PartACode);

//            if (existing != null)
//            {
//                if (string.IsNullOrEmpty(existing.PartCode) && !string.IsNullOrEmpty(item.PartCode))
//                    existing.PartCode = item.PartCode;
//                if (string.IsNullOrEmpty(existing.PartNo) && !string.IsNullOrEmpty(item.PartNo))
//                    existing.PartNo = item.PartNo;
//                if (string.IsNullOrEmpty(existing.PartName) && !string.IsNullOrEmpty(item.PartName))
//                    existing.PartName = item.PartName;

//                if (item.Status == "IN")
//                {
//                    existing.InCount += 1;
//                    existing.TotalInQty += item.Qty;
//                }
//                else if (item.Status == "OUT")
//                {
//                    existing.OutCount += 1;
//                    existing.TotalOutQty += item.Qty;
//                }

//                // เอายอดจริงจาก SQL มาเขียนทับเลย ไม่ต้องบวกลบอินเมมโมรี่ให้เสี่ยงติดลบอีกต่อไป
//                existing.FinalStock = actualCurrentStock;
//            }
//            else
//            {
//                HistoryItems.Add(new ScanItemModel
//                {
//                    PartId = item.PartId, // อย่าลืมแมปค่าตัวนี้เผื่อสแกนซ้ำตัวเดิมในแถวถัดไป
//                    PartCode = item.PartCode,
//                    PartName = item.PartName,
//                    PartACode = item.PartACode,
//                    PartNo = item.PartNo,
//                    InCount = item.Status == "IN" ? 1 : 0,
//                    TotalInQty = item.Status == "IN" ? item.Qty : 0,
//                    OutCount = item.Status == "OUT" ? 1 : 0,
//                    TotalOutQty = item.Status == "OUT" ? item.Qty : 0,

//                    // ใช้ยอดจริงจาก SQL สำหรับการสแกนเจอรายการใหม่ครั้งแรกของวัน
//                    FinalStock = actualCurrentStock,
//                    ProductImagePath = item.ProductImagePath
//                });
//            }
//        }

//        public event PropertyChangedEventHandler PropertyChanged;
//        protected void OnPropertyChanged([CallerMemberName] string name = null)
//            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
//    }
//}

using Microsoft.Extensions.Logging;
using StoreSteels.Helpers;
using StoreSteels.Models;
using StoreSteels.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace StoreSteels.ViewModels
{
    public class ScanInViewModel : INotifyPropertyChanged
    {
        private readonly ScanService _scanService = new ScanService();

        public UserSession CurrentUser { get; set; }
        public ObservableCollection<ScanItemModel> ScannedItems { get; set; } = new ObservableCollection<ScanItemModel>();

        // 🚀 เพิ่ม Collection แยกสำหรับเก็บข้อมูลสแกนสภาวะ Test (ไม่ลง DB)
        public ObservableCollection<ScanItemModel> TestScannedItems { get; set; } = new ObservableCollection<ScanItemModel>();

        public ObservableCollection<ScanItemModel> HistoryItems { get; set; } = new ObservableCollection<ScanItemModel>();

        #region Mode Test Properties
        private bool _isTestMode = false;
        public bool IsTestMode
        {
            get => _isTestMode;
            set
            {
                _isTestMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TestButtonText));
                OnPropertyChanged(nameof(TestButtonBackground));
                OnPropertyChanged(nameof(TestBannerVisibility));
            }
        }

        public string TestButtonText => IsTestMode ? "ยกเลิกเทส (Exit Test)" : "เปิดโหมด Test";
        public System.Windows.Media.Brush TestButtonBackground => IsTestMode
            ? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#D32F2F") // สีแดงเมื่อกดถอนโหมด
            : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#ED6C02"); // สีส้มโหมด Test

        public Visibility TestBannerVisibility => IsTestMode ? Visibility.Visible : Visibility.Collapsed;

        // Command สำหรับกดปุ่มสลับโหมด Test
        public ICommand ToggleTestModeCommand { get; }
        #endregion

        #region Mode Return (คืนเหล็ก) Properties
        // 🔄 โหมดคืนเหล็กที่เหลือจากการผลิตกลับเข้าคลัง: สแกน QR ที่ Export มาจากหน้า ProductControl
        // (รูปแบบ ProductCode|ProductName) แล้วเด้ง Popup ให้กรอกจำนวนรับคืนเอง แทนที่จะรับเข้าตาม
        // Packsize มาตรฐานแบบการสแกนปกติ กดปุ่มซ้ำเพื่อยกเลิกโหมดกลับไปสแกนแบบปกติ
        private bool _isReturnMode = false;
        public bool IsReturnMode
        {
            get => _isReturnMode;
            set
            {
                _isReturnMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReturnButtonText));
                OnPropertyChanged(nameof(ReturnButtonBackground));
                OnPropertyChanged(nameof(ReturnBannerVisibility));
            }
        }

        public string ReturnButtonText => IsReturnMode ? "ยกเลิกการคืนเหล็ก" : "คืนเหล็ก";
        public System.Windows.Media.Brush ReturnButtonBackground => IsReturnMode
            ? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#D32F2F") // สีแดงเมื่อกำลังอยู่ในโหมด (กดซ้ำ = ยกเลิก)
            : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#0288D1"); // สีฟ้าโหมดปกติ

        public Visibility ReturnBannerVisibility => IsReturnMode ? Visibility.Visible : Visibility.Collapsed;

        public ICommand ToggleReturnModeCommand { get; }
        #endregion

        #region Properties สำหรับผูกกับ UI
        private System.Windows.Media.ImageSource _showProductImage;
        public System.Windows.Media.ImageSource ShowProductImage
        {
            get => _showProductImage;
            set { _showProductImage = value; OnPropertyChanged(); }
        }

        private string _showName;
        public string ShowName { get => _showName; set { _showName = value; OnPropertyChanged(); } }

        private string _showCode;
        public string ShowCode { get => _showCode; set { _showCode = value; OnPropertyChanged(); } }

        private int _showQtyValue;
        public string ShowQty
        {
            get => _showQtyValue.ToString();
            set { if (int.TryParse(value, out int res)) _showQtyValue = res; OnPropertyChanged(); }
        }

        private string _barcodeInput;
        public string BarcodeInput
        {
            get => _barcodeInput;
            set { _barcodeInput = value; OnPropertyChanged(); }
        }
        #endregion

        public ScanInViewModel()
        {
            ToggleTestModeCommand = new RelayCommand(p => ExecuteToggleTestMode());
            ToggleReturnModeCommand = new RelayCommand(p => ExecuteToggleReturnMode());
            LoadTodayData();
        }

        private void ExecuteToggleTestMode()
        {
            IsTestMode = !IsTestMode;
        }

        private void ExecuteToggleReturnMode()
        {
            IsReturnMode = !IsReturnMode;
        }

        private async void LoadTodayData()
        {
            try
            {
                var data = await Task.Run(() => _scanService.GetTodayTransactions());

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ScannedItems.Clear();
                    HistoryItems.Clear();
                    foreach (var item in data)
                    {
                        if (item.Status == "IN" || item.Status == "RETURN")
                        {
                            ScannedItems.Insert(0, item);
                        }
                        UpdateSummary(item);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load Error: {ex.Message}");
            }
        }

        public async Task ProcessScan(string inputCode)
        {
            if (string.IsNullOrWhiteSpace(inputCode) || CurrentUser == null) return;

            // 🔄 โหมดคืนเหล็ก: ตัดออกจาก flow ปกติทั้งหมด ใช้ QR รูปแบบ Export จากหน้า ProductControl
            // (ProductCode|ProductName) แทน แล้วเด้ง Popup ให้กรอกจำนวนรับคืนเอง
            if (IsReturnMode)
            {
                await ProcessReturnScan(inputCode);
                return;
            }

            string finalSearchCode = inputCode.Trim();
            string rawBarcodeFull = finalSearchCode;
            string uid = CurrentUser.UserId;

            // ✅ รองรับ QR ที่พิมพ์จากหน้า Packing Card (TicketNo | MaterialCode | WorkOrder | LotNo | JobName | Qty)
            // ให้ค้นหาด้วย MaterialCode และใช้ Qty ตามใบเบิกแทนค่า Pack Size เริ่มต้นของสินค้า
            bool isPackingCardScan = PackingCardBarcodeParser.TryParse(finalSearchCode, out string packingMaterialCode, out decimal packingQty);

            if (isPackingCardScan)
            {
                finalSearchCode = packingMaterialCode;
            }
            else
            {
                var parts = finalSearchCode.Split(new[] { '|', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length > 0)
                {
                    string firstChunk = parts[0].Trim();

                    if (firstChunk.Length > 2 && char.IsDigit(firstChunk[0]) && char.IsLetter(firstChunk[1]))
                    {
                        finalSearchCode = firstChunk.Substring(1);
                    }
                    else
                    {
                        finalSearchCode = firstChunk;
                    }
                }
            }

            try
            {
                var result = await Task.Run(() =>
                {
                    var part = _scanService.GetPartByScan(finalSearchCode);
                    if (part == null) return null;

                    int originalQty = isPackingCardScan ? (int)packingQty : (int)part.Qty;

                    // ⚙️ [เพิ่มเงื่อนไข Test Mode]: ถ้าอยู่ในโหมด Test จะไม่ยิง UpdateStock เข้า DB
                    if (IsTestMode)
                    {
                        part.Qty = originalQty;
                        return part; // ส่งข้อมูลคืนทันที โดยไม่สั่งบันทึก DB และไม่เขียน Log
                    }

                    // บันทึกจริงกรณีโหมดปกติ
                    bool isSaved = _scanService.UpdateStock(part.PartId, part.PartCode, part.PartACode, originalQty, uid, rawBarcodeFull);
                    if (isSaved)
                    {
                        LogService.WriteScanLog(uid, "SCAN_IN", part.PartCode, part.PartACode, originalQty);
                        part.Qty = originalQty;
                        return part;
                    }
                    return null;
                });

                if (result != null)
                {
                    ShowCode = result.PartACode;
                    ShowName = result.PartName;
                    ShowQty = result.Qty.ToString();

                    LoadProductImage(result.PartACode);

                    var newItem = new ScanItemModel
                    {
                        PartId = result.PartId,
                        PartCode = result.PartCode,
                        PartName = result.PartName,
                        PartNo = result.PartNo,
                        PartACode = result.PartACode,
                        Qty = (int)result.Qty,
                        Status = IsTestMode ? "TEST" : "IN", // กำหนดสถานะให้เห็นชัดๆ
                        UpdateTime = DateTime.Now,
                        ProductImagePath = ShowProductImage
                    };

                    // ⚙️ [สลับตารางแสดงผล]: ถ้าเป็น Test Mode ให้โยนลง TestScannedItems (ตารางที่ 2)
                    if (IsTestMode)
                    {
                        TestScannedItems.Insert(0, newItem);
                        if (TestScannedItems.Count > 12)
                        {
                            TestScannedItems.RemoveAt(TestScannedItems.Count - 1);
                        }
                    }
                    else
                    {
                        ScannedItems.Insert(0, newItem);
                        if (ScannedItems.Count > 12)
                        {
                            ScannedItems.RemoveAt(ScannedItems.Count - 1);
                        }
                        UpdateSummary(newItem);
                    }
                }
                else
                {
                    DialogHelper.ShowError($"[รายการไม่สำเร็จ] ไม่พบข้อมูลสินค้าในระบบ หรือบาร์โค้ดยังไม่ได้ลงทะเบียน\nCode: {finalSearchCode}");
                }
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError("เกิดข้อผิดพลาด: " + ex.Message);
            }
            finally
            {
                BarcodeInput = string.Empty;
            }
        }

        // ดึงรูปสินค้าจาก Shared Folder มาแสดงที่ ShowProductImage (ใช้ร่วมกันทั้งสแกนปกติและคืนเหล็ก)
        private void LoadProductImage(string partACode)
        {
            string baseFolder = @"\\192.168.10.56\ProgramCHR\2. Store Only\StoreSteels\Image";
            string fileName = partACode;
            string imgPath = System.IO.Path.Combine(baseFolder, $"{fileName}.png");

            if (!System.IO.File.Exists(imgPath))
                imgPath = System.IO.Path.Combine(baseFolder, $"{fileName}.jpg");

            if (!System.IO.File.Exists(imgPath))
                imgPath = System.IO.Path.Combine(baseFolder, "no-image.png");

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (System.IO.File.Exists(imgPath))
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
                        bitmap.UriSource = new Uri(imgPath, UriKind.Absolute);
                        bitmap.EndInit();
                        bitmap.Freeze();

                        ShowProductImage = bitmap;
                    }
                    else
                    {
                        ShowProductImage = null;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Load Image Error: {ex.Message}");
                    ShowProductImage = null;
                }
            });
        }

        // ตัด Product Code ออกจาก QR ที่ Export มาจากหน้า ProductControl (รูปแบบ "ProductCode|ProductName")
        // เผื่อกรณีไม่มี "|" (เช่น พิมพ์รหัสตรงๆ) ให้ใช้ค่าที่ trim แล้วทั้งก้อนแทน
        private static string ExtractReturnScanCode(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            string trimmed = raw.Trim();
            int pipeIndex = trimmed.IndexOf('|');
            return pipeIndex >= 0 ? trimmed.Substring(0, pipeIndex).Trim() : trimmed;
        }

        // 🔄 [โหมดคืนเหล็ก] สแกน QR Export จากหน้า ProductControl -> ค้นหาสินค้า -> เด้ง Popup กรอกจำนวนรับคืน
        // -> ยืนยันแล้วอัปเดต QTY_STKB ตามจำนวนที่กรอกเอง (ไม่ใช่ Packsize มาตรฐาน) พร้อม tag สถานะ "RETURN"
        private async Task ProcessReturnScan(string inputCode)
        {
            string rawBarcodeFull = inputCode.Trim();
            string uid = CurrentUser.UserId;
            string code = ExtractReturnScanCode(rawBarcodeFull);

            try
            {
                var part = await Task.Run(() => _scanService.GetPartByScan(code));

                if (part == null)
                {
                    DialogHelper.ShowError($"[รายการไม่สำเร็จ] ไม่พบข้อมูลสินค้าในระบบสำหรับคืนเหล็ก\nCode: {code}");
                    return;
                }

                int? enteredQty = DialogHelper.ShowQuantityInput(
                    $"{part.PartName}\nProduct Code: {part.PartCode}\n\nกรุณากรอกจำนวนที่รับคืนเข้าคลัง",
                    "คืนเหล็กเข้าคลัง");

                if (enteredQty == null || enteredQty.Value <= 0)
                    return; // ผู้ใช้กด Cancel หรือปิดหน้าต่าง - ไม่ทำอะไรต่อ

                int qty = enteredQty.Value;
                bool isSaved = true;

                if (!IsTestMode)
                {
                    isSaved = await Task.Run(() =>
                        _scanService.UpdateStock(part.PartId, part.PartCode, part.PartACode, qty, uid, rawBarcodeFull, "RETURN"));

                    if (isSaved)
                    {
                        LogService.WriteScanLog(uid, "SCAN_RETURN", part.PartCode, part.PartACode, qty);
                    }
                    else
                    {
                        DialogHelper.ShowError("บันทึกการคืนเหล็กไม่สำเร็จ กรุณาลองใหม่อีกครั้ง");
                        return;
                    }
                }

                ShowCode = part.PartACode;
                ShowName = part.PartName;
                ShowQty = qty.ToString();

                LoadProductImage(part.PartACode);

                var newItem = new ScanItemModel
                {
                    PartId = part.PartId,
                    PartCode = part.PartCode,
                    PartName = part.PartName,
                    PartNo = part.PartNo,
                    PartACode = part.PartACode,
                    Qty = qty,
                    Status = IsTestMode ? "TEST" : "RETURN",
                    UpdateTime = DateTime.Now,
                    ProductImagePath = ShowProductImage
                };

                if (IsTestMode)
                {
                    TestScannedItems.Insert(0, newItem);
                    if (TestScannedItems.Count > 12)
                    {
                        TestScannedItems.RemoveAt(TestScannedItems.Count - 1);
                    }
                }
                else
                {
                    ScannedItems.Insert(0, newItem);
                    if (ScannedItems.Count > 12)
                    {
                        ScannedItems.RemoveAt(ScannedItems.Count - 1);
                    }
                    UpdateSummary(newItem);
                }
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError("เกิดข้อผิดพลาด: " + ex.Message);
            }
            finally
            {
                BarcodeInput = string.Empty;
            }
        }

        private void UpdateSummary(ScanItemModel item)
        {
            if (string.IsNullOrWhiteSpace(item.PartACode)) return;

            int actualCurrentStock = _scanService.GetInventoryBalance(item.PartId);
            var existing = HistoryItems.FirstOrDefault(x => x.PartACode == item.PartACode);

            if (existing != null)
            {
                if (string.IsNullOrEmpty(existing.PartCode) && !string.IsNullOrEmpty(item.PartCode))
                    existing.PartCode = item.PartCode;
                if (string.IsNullOrEmpty(existing.PartNo) && !string.IsNullOrEmpty(item.PartNo))
                    existing.PartNo = item.PartNo;
                if (string.IsNullOrEmpty(existing.PartName) && !string.IsNullOrEmpty(item.PartName))
                    existing.PartName = item.PartName;

                // การคืนเหล็ก (RETURN) นับรวมเป็นยอดรับเข้าเหมือน IN ในตารางสรุปนี้ เพราะเป็นการเพิ่มสต็อกเข้าคลังเช่นกัน
                if (item.Status == "IN" || item.Status == "RETURN")
                {
                    existing.InCount += 1;
                    existing.TotalInQty += item.Qty;
                }
                else if (item.Status == "OUT")
                {
                    existing.OutCount += 1;
                    existing.TotalOutQty += item.Qty;
                }

                existing.FinalStock = actualCurrentStock;
            }
            else
            {
                HistoryItems.Add(new ScanItemModel
                {
                    PartId = item.PartId,
                    PartCode = item.PartCode,
                    PartName = item.PartName,
                    PartACode = item.PartACode,
                    PartNo = item.PartNo,
                    InCount = (item.Status == "IN" || item.Status == "RETURN") ? 1 : 0,
                    TotalInQty = (item.Status == "IN" || item.Status == "RETURN") ? item.Qty : 0,
                    OutCount = item.Status == "OUT" ? 1 : 0,
                    TotalOutQty = item.Status == "OUT" ? item.Qty : 0,
                    FinalStock = actualCurrentStock,
                    ProductImagePath = item.ProductImagePath
                });
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // Helper Class สำหรับ Bind ปุ่ม Toggle
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}