using StoreSteels.Helpers;
using StoreSteels.Models;
using StoreSteels.Services;
using StoreSteels.ViewModels;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace StoreSteels.Views
{
    public partial class PRView : Page
    {
        private readonly PRViewModel _viewModel;
        private readonly PRService _prService = new PRService();
        private CancellationTokenSource _searchCts;
        private int _currentViewingId = -1;

        #region === [ PRView ] ===

        public PRView(UserSession session)
        {
            InitializeComponent();
            RunEntryAnimation();
            _viewModel = new PRViewModel { CurrentUser = session };
            this.DataContext = _viewModel;

            ApplyPermission(session.UserLevel);
            _ = _viewModel.LoadAllPR(); // โหลดข้อมูลเริ่มต้น
        }

        #endregion

        #region === [ Permission Control ] ===

        private void ApplyPermission(int level)
        {
            // Level 1-3 สร้าง PR ได้
            btnSubmitPR.Visibility = (level <= 3) ? Visibility.Visible : Visibility.Collapsed;
            // Level 1-2 Export ได้
            btnExport.Visibility = (level <= 2) ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region === [ Event Submit PR ] ===

        private async void SubmitPR_Click(object sender, RoutedEventArgs e)
        {
            string partName = txtProductName.Text.Trim();
            if (string.IsNullOrEmpty(partName) || !int.TryParse(txtQTY.Text, out int q))
            {
                DialogHelper.ShowWarning("ข้อมูลไม่ครบถ้วน");
                return;
            }

            // ตรวจสอบสินค้าใน MST_PART
            bool exists = await Task.Run(() => _prService.IsProductExists(partName));
            if (!exists)
            {
                DialogHelper.ShowWarning("ไม่พบสินค้าลำดับนี้ในระบบ");
                return;
            }

            var newItem = new PRModel { PartName = partName, QTY = q };
            if (await _viewModel.SavePRToDb(newItem))
            {
                DialogHelper.ShowSuccess("บันทึกสำเร็จ");
                ClearInputs();
            }
        }

        #endregion

        #region === [ Event Export ] ===

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            var selected = _viewModel.PRHistory.Where(x => x.IsSelected).ToList();
            if (!selected.Any())
            {
                DialogHelper.ShowWarning("เลือกรายการก่อนครับ");
                return;
            }

            if (await _viewModel.ProcessExportAsync(selected))
            {
                DialogHelper.ShowSuccess("ส่งออกเรียบร้อย");
            }
        }

        #endregion

        #region === [ Event Approve ] ===
        private async void Approve_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PRModel selected)
            {
                if (DialogHelper.ShowConfirm($"อนุมัติรายการ {selected.PR_NO}?", "ยืนยัน"))
                {
                    await _viewModel.ApprovePR(selected);
                }
            }
        }
        #endregion
        
        #region === [ Event Reject ] ===
        private async void Reject_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PRModel selected)
            {
                if (DialogHelper.ShowConfirm($"ปฏิเสธรายการ {selected.PR_NO}?", "ยืนยัน"))
                {
                    await _viewModel.RejectPR(selected);
                }
            }
        }
        #endregion

        #region === [ Event View ] ===
        private void ViewAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PRModel selected)
            {
                if (_currentViewingId == selected.ID) { ClearInputs(); }
                else
                {
                    _currentViewingId = selected.ID;
                    txtProductName.Text = selected.PartName;
                    txtQTY.Text = selected.QTY.ToString();
                    btnSubmitPR.Visibility = Visibility.Collapsed;
                    dgPR.SelectedItem = selected;
                }
            }
        }
        #endregion

        #region === [ Event ClearInputs ] ===
        private void ClearInputs()
        {
            _currentViewingId = -1;
            txtProductName.Text = "";
            txtQTY.Clear();
            // คืนค่า Binding ให้ชื่อคนทำรายการและวันที่กลับมาเป็นปัจจุบัน (ของเก่าที่หายไป)
            txtRequester.SetBinding(TextBox.TextProperty, new Binding("LoginUserName") { Mode = BindingMode.OneWay });
            txtPRDate.SetBinding(TextBox.TextProperty, new Binding("CurrentDateDisplay") { Mode = BindingMode.OneWay });
            txtDept.Text = _viewModel.FixedDept;
            // เช็คสิทธิ์ปุ่มบันทึกอีกครั้ง
            if (_viewModel.CurrentUser.UserLevel <= 3)
                btnSubmitPR.Visibility = Visibility.Visible;
            dgPR.SelectedItem = null;
            txtProductName.IsReadOnly = false;
            txtQTY.IsReadOnly = false;
        }
        #endregion

        #region === [ Event txtProductName KeyUp ] ===
        private async void txtProductName_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Down || e.Key == System.Windows.Input.Key.Up) return;
            string text = txtProductName.Text;
            if (text.Length >= 2)
            {
                await _viewModel.UpdateSuggestions(text);
                // แก้จาก ProductSuggestions.Any() เป็น _viewModel.ProductSuggestions.Any()
                txtProductName.IsDropDownOpen = _viewModel.ProductSuggestions.Any();
            }
        }
        #endregion

        #region === [ Event SelectAllPR ] ===
        private void SelectAllPR_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk) _viewModel.ToggleSelectAll(chk.IsChecked ?? false);
        }
        #endregion

        #region ===[ Event txtSearchPR TextChanged ] ===
        private async void txtSearchPR_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            try
            {
                await Task.Delay(400, _searchCts.Token);
                await _viewModel.LoadAllPR(txtSearchPR.Text);
            }
            catch (TaskCanceledException) { }
        }
        #endregion

        #region ===[ Event SearchPR ] ===
        private void SearchPR_Click(object sender, RoutedEventArgs e)
        {
            _ = _viewModel.LoadAllPR(txtSearchPR.Text.Trim());
        }
        #endregion

        #region ===[ Event txtSearchPR KeyDown ] ===

        // สำหรับการกด Enter ในช่องค้นหา (ถ้าใน XAML เขียน KeyDown="txtSearchPR_KeyDown")
        private void txtSearchPR_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                _ = _viewModel.LoadAllPR(txtSearchPR.Text.Trim());
            }
        }

        #endregion

        #region === [ Event Animation ] ===

        private void RunEntryAnimation()
        {
            TimeSpan duration = TimeSpan.FromSeconds(0.6);
            IEasingFunction ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            // Fade In
            DoubleAnimation fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = duration
            };

            // Slide Up (PageTransform ต้องมีชื่อตรงกับใน XAML)
            DoubleAnimation slideUp = new DoubleAnimation
            {
                From = 30,
                To = 0,
                Duration = duration,
                EasingFunction = ease
            };

            this.BeginAnimation(Page.OpacityProperty, fadeIn);

            // ตรวจสอบความปลอดภัยก่อนรัน Animation
            if (PageTransform != null)
            {
                PageTransform.BeginAnimation(TranslateTransform.YProperty, slideUp);
            }
        }

        #endregion

    }
}