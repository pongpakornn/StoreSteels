    using QRCoder;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media.Imaging;
    using System.Windows.Media.Animation;
    using System.Windows.Media;
    using System.Threading.Tasks; // ใส่เพิ่มเพื่อให้รองรับ Task.Run สวยๆ ครับ
    using StoreSteels.Helpers;
    using StoreSteels.Services;
    using StoreSteels.Models;
    using StoreSteels.ViewModels;

    namespace StoreSteels.Views
    {
        public partial class ProductControlView : Page
        {
            private readonly ProductControlViewModel _viewModel;
            private string _currentViewingCode = "";
            // ✅ แก้ไขตรงนี้: เปลี่ยนจาก _oldPartCode เป็น _oldPartACode เพื่อให้สามารถเรียกใช้งานในเมธอดด้านล่างได้
            private string _oldPartACode = "";

            public ProductControlView(UserSession session)
            {
                InitializeComponent();

                _viewModel = new ProductControlViewModel();
                _viewModel.CurrentUser = session;

                this.DataContext = _viewModel;

                // --- ปลดล็อกทุกปุ่มให้ทุกคน ---
                //btnGenerate.IsEnabled = true;
                btnRegister.Visibility = Visibility.Visible;

                RunEntryAnimation();
            }

            private void btnSelectImage_Click(object sender, RoutedEventArgs e)
            {
                if (_viewModel != null)
                {
                    _viewModel.SelectProductImage();
                }
            }

            private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
            {
                if (_viewModel != null)
                {
                    _viewModel.SearchText = txtSearch.Text;
                }
            }

            private void Find_Click(object sender, RoutedEventArgs e)
            {
                _viewModel.LoadData(txtSearch.Text);
            }

            private async void RegisterPart_Click(object sender, RoutedEventArgs e)
            {
                if (string.IsNullOrEmpty(txtProductCode.Text))
                {
                    DialogHelper.ShowWarning("กรุณากรอกรหัสสินค้าก่อนทำการลงทะเบียนครับ");
                    return;
                }

                var newModel = GetModelFromInputs();

                // บันทึกข้อมูล
                bool result = await _viewModel.SaveToDb(newModel, isUpdate: false);

                if (result)
                {
                    DialogHelper.ShowSuccess("ลงทะเบียนสินค้าใหม่เรียบร้อยแล้ว!");
                    ClearOnlyInputs();
                }
                else
                {
                    // เพิ่มจุดนี้เข้าไปเพื่อให้รู้ตัวทันทีว่าฟังก์ชันส่ง False กลับมา
                    DialogHelper.ShowWarning("การลงทะเบียนถูกปฏิเสธ (ตรวจสอบข้อผิดพลาดด้านบน)");
                }
            }
            private async void SaveEdit_Click(object sender, RoutedEventArgs e)
            {
                var editModel = GetModelFromInputs();

                // 🎯 ส่งพารามิเตอร์ตัวสุดท้ายเป็น _oldPartACode (เดิมส่ง _oldPartCode)
                bool result = await _viewModel.SaveToDb(editModel, isUpdate: true, oldCode: _oldPartACode);

                if (result)
                {
                    DialogHelper.ShowSuccess("แก้ไขข้อมูลเรียบร้อยแล้ว!");
                    SetEditMode(false);
                    ClearOnlyInputs();
                    _oldPartACode = null; // ล้างค่าหลังทำงานสำเร็จ
                }
            }

            private void ViewAction_Click(object sender, RoutedEventArgs e)
            {
                if (sender is Button btn && btn.DataContext is ProductControlModel selected)
                {
                    if (_currentViewingCode == selected.PartACode)
                    {
                        // กดซ้ำ: ล้างแค่ช่องกรอกข้อมูล ไม่ล้างตาราง
                        ClearOnlyInputs();
                        dgQRHistory.SelectedItem = null;
                    }
                    else
                    {
                        // กดใหม่: เติมข้อมูล
                        _currentViewingCode = selected.PartACode;
                        FillInputsFromModel(selected);
                        GenerateQRPreviewLogic();
                        dgQRHistory.SelectedItem = selected;
                    }
                }
            }

            private void EditPD_Click(object sender, RoutedEventArgs e)
            {
                if (sender is Button btn && btn.DataContext is ProductControlModel selected)
                {
                    if (btnSave.Visibility == Visibility.Visible && _oldPartACode == selected.PartACode)
                    {
                        // กดซ้ำ: ล้างแค่ช่องกรอกข้อมูล + ปิดโหมดแก้ไข
                        ClearOnlyInputs();
                        SetEditMode(false);
                        dgQRHistory.SelectedItem = null;
                    }
                    else
                    {
                        // กดใหม่: เข้าโหมดแก้ไข
                        FillInputsFromModel(selected);
                        _currentViewingCode = selected.PartACode;
                        _oldPartACode = selected.PartACode;
                        SetEditMode(true);
                        txtProductName.Focus();
                        GenerateQRPreviewLogic();
                        dgQRHistory.SelectedItem = selected;
                    }
                }
            }

            private async void DeletePD_Click(object sender, RoutedEventArgs e)
            {
                if (sender is Button btn && btn.DataContext is ProductControlModel selected)
                {
                    if (DialogHelper.ShowConfirm($"ลบสินค้า: {selected.PartName}?", "CONFIRM"))
                    {
                        if (await _viewModel.DeleteProductAsync(selected))
                        {
                            DialogHelper.ShowSuccess("ลบสำเร็จ!");
                        }
                    }
                }
            }

            private void ToggleVisibility_Click(object sender, RoutedEventArgs e)
            {
                if (sender is Button btn && btn.DataContext is ProductControlModel selected)
                {
                    if (_viewModel.ToggleStatus(selected))
                    {
                        DialogHelper.ShowSuccess(selected.IsShow ? "แสดงรายการแล้ว" : "ซ่อนรายการแล้ว");
                    }
                }
            }

            private void SelectAll_Click(object sender, RoutedEventArgs e)
            {
                var checkBox = sender as CheckBox;
                if (checkBox == null || _viewModel?.Products == null) return;

                bool isChecked = checkBox.IsChecked ?? false;

                foreach (var product in _viewModel.Products)
                {
                    product.IsSelected = isChecked;
                }

                dgQRHistory.Items.Refresh();
            }

            // --- UI Helpers ---
            private ProductControlModel GetModelFromInputs()
            {
                // schema ใหม่ตัด PT_ACODE/PT_MODEL/PT_NO ออกจาก MST_PART แล้ว PT_CODE เป็นตัวระบุหลักตัวเดียว
                // PartACode จึงมิเรอร์ค่าจาก PartCode เพื่อให้ logic เดิมที่อ้างอิง PartACode (lookup แถว, edit, delete) ยังทำงานถูกต้อง
                string code = txtProductCode.Text.Trim();
                return new ProductControlModel
                {
                    CustomerCode = txtSupplier.Text.Trim(),
                    Category = txtCategory.Text.Trim(),
                    PartCode = code,
                    PartACode = code,
                    PartName = txtProductName.Text.Trim(),
                    PackSize = txtPcs.Text.Trim(),
                    Location = txtBin.Text.Trim(),
                    QRCodeData = txtQRCode.Text.Trim(),
                    ImageFileName = _viewModel.SelectedProduct?.ImageFileName
                };
            }

            private void RefreshUI()
            {
                ClearOnlyInputs();
                _viewModel.LoadData();
            }

        private void ClearOnlyInputs()
        {
            // เคลียร์ TextBox
            txtSupplier.Clear();
            txtCategory.Clear();
            txtProductCode.Clear();
            txtProductName.Clear();
            txtPcs.Clear();
            txtBin.Clear();
            txtQRCode.Clear();

            // 🎯 หัวใจสำคัญ: สร้างตัวใหม่ไปเลย เพื่อตัดความสัมพันธ์กับ Row เดิมในตาราง
            _viewModel.SelectedProduct = new ProductControlModel();

            _currentViewingCode = string.Empty;
            _oldPartACode = null;

            // เคลียร์ Selection ในตาราง
            dgQRHistory.SelectedItem = null;

            // บังคับให้ตาราง Refresh เพื่อให้แน่ใจว่าแสดงผลค่าล่าสุด
            dgQRHistory.Items.Refresh();
        }

        private void FillInputsFromModel(ProductControlModel model)
                {
            _viewModel.SelectedProduct = new ProductControlModel
            {
                CustomerCode = model.CustomerCode,
                Category = model.Category,
                PartCode = model.PartCode,
                PartACode = model.PartACode,
                PartName = model.PartName,
                PackSize = model.PackSize,
                Location = model.Location,
                QRCodeData = model.QRCodeData,
                ImageFileName = model.ImageFileName
                // ถ้ามี Field อื่นให้เพิ่มตรงนี้
            };
        }

                private void SetEditMode(bool isEdit)
                {
                    btnRegister.Visibility = isEdit ? Visibility.Collapsed : (_viewModel.CurrentUser?.UserLevel <= 2 ? Visibility.Visible : Visibility.Collapsed);
                    btnSave.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;
                    txtProductCode.IsEnabled = true;
                    txtProductCode.Opacity = 1.0;
                }

            // --- QR Logic ---
            private void GenerateQR_Click(object sender, RoutedEventArgs e)
            {
                GenerateQRPreviewLogic();
            }

            private void GenerateQRPreviewLogic()
            {
                try
                {
                    string qrText = txtProductCode.Text.Trim();
                    if (string.IsNullOrEmpty(qrText)) return;

                    using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                    {
                        using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.Q))
                        {
                            using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
                            {
                                byte[] qrCodeAsPngByteArr = qrCode.GetGraphic(20);

                                using (MemoryStream ms = new MemoryStream(qrCodeAsPngByteArr))
                                {
                                    BitmapImage bitmap = new BitmapImage();
                                    bitmap.BeginInit();
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.StreamSource = ms;
                                    bitmap.EndInit();
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    DialogHelper.ShowError($"ไม่สามารถสร้าง QR Code ได้: {ex.Message}");
                }
            }

            private void RunEntryAnimation()
            {
                if (PageTransform != null)
                {
                    DoubleAnimation anim = new DoubleAnimation
                    {
                        To = 0,
                        Duration = TimeSpan.FromSeconds(0.4),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    PageTransform.BeginAnimation(TranslateTransform.YProperty, anim);
                }
            }

            private void dgQRHistory_ScrollChanged(object sender, ScrollChangedEventArgs e)
            {
                // ไว้สำหรับจัดการ Infinite Scroll หรือโหลดหน้าเพิ่มในกรณีที่เปิดใช้งาน Virtualization
            }

            // ✅ นำฟังก์ชันกลับเข้ามาอยู่ก่อนปีกกาปิดคลาสอันแรกแล้วครับ
            private async void Export_Click(object sender, RoutedEventArgs e)
            {
                var selectedItems = _viewModel.Products.Where(x => x.IsSelected).ToList();

                if (selectedItems.Count == 0)
                {
                    DialogHelper.ShowWarning("กรุณาเลือกรายการที่ต้องการ Export อย่างน้อย 1 รายการครับ");
                    return;
                }

                try
                {
                    string currentUserId = _viewModel.CurrentUser?.UserId ?? "Unknown";

                    await Task.Run(() =>
                    {
                        var exportService = new ExportService();
                        exportService.GenerateA4Pdf(selectedItems);
                    });

                    foreach (var item in selectedItems)
                    {
                        LogService.WriteLog(currentUserId, "EXPORT_QR_PDF", $"Exported QR Code to PDF for Part: {item.PartName}", item.PartCode);
                    }

                    DialogHelper.ShowSuccess("ส่งออกไฟล์ QR ไปที่โฟลเดอร์ Desktop\\StoreSteels_Export เรียบร้อยแล้ว!");
                }
                catch (Exception ex)
                {
                    DialogHelper.ShowError($"Export ไม่สำเร็จ: {ex.Message}");
                }
            }
        } // 👈 ปิดตัวคลาส ProductControlView
    } // 👈 ปิดตัวเนมสเปซ StoreSteels.Views