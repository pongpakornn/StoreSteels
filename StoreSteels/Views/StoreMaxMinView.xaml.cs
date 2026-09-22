using StoreSteels.Models;
using StoreSteels.Services;
using StoreSteels.Helpers;
using StoreSteels.ViewModels;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace StoreSteels.Views
{
    public partial class StoreMaxMinView : Page
    {
        private StoreMaxMinViewModel _viewModel;
        private bool _isEditing = false;

        // Auto-Scroll Fields
        private DispatcherTimer _autoScrollTimer; // 🟢 ปรับมาใช้ Timer ควบคุมความเร็วคงที่แทน Rendering
        private double _currentScrollOffset = 0;
        private bool _isUserInteracting = false;
        private DispatcherTimer _interactionResetTimer;
        private DispatcherTimer _searchDebounceTimer;
        private DispatcherTimer _refreshTimer;

        // ตัวแปรเก็บค่าเดิมก่อนแก้ไข
        private StoreProductModel _originalData;

        // ตัวแปรระดับคลาสสำหรับเก็บค่าที่ส่งมาจาก Dashboard
        private string _selectedCategory;
        private string _filterType;

        public StoreMaxMinView(UserSession session)
        {
            InitializeComponent();
            _viewModel = new StoreMaxMinViewModel();
            _viewModel.CurrentUser = session;
            this.DataContext = _viewModel;

            this.Loaded += (s, e) => {
                if (string.IsNullOrEmpty(_filterType) && string.IsNullOrEmpty(_selectedCategory))
                {
                    _viewModel.LoadData();
                }
                InitializeAutoScroll();
                InitializeRealTimeRefresh();
            };

            // 🧼 เมื่อ User ย้ายหน้า ย้ายแท็บ หรือปิดหน้าจอ ให้เคลียร์ทุก Timer ทันที ป้องกันการทำงานรั่วไหลเบื้องหลัง
            this.Unloaded += (s, e) => {
                StopRealTimeRefresh();
                StopAutoScroll();
            };

            RunEntryAnimation();
        }

        public StoreMaxMinView(UserSession session, string categoryCode, string filterType) : this(session)
        {
            _selectedCategory = categoryCode;
            _filterType = filterType;

            if (_viewModel != null)
            {
                _viewModel.SelectedCategory = !string.IsNullOrEmpty(_selectedCategory) ? _selectedCategory : "ALL CATEGORIES";
                _viewModel.SelectedFilterType = _filterType;
            }
        }

        #region --- RealTime Refresh Logic ---

        private void InitializeRealTimeRefresh()
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer.Tick -= RefreshTimer_Tick;
                _refreshTimer = null;
            }

            _refreshTimer = new DispatcherTimer();
            // ⏱️ แก้ไข: ล็อกเวลารีเฟรชเบื้องหลังให้เป็น 3 วินาที (ไม่ใช้ 0 วินาทีแล้ว เพื่อไม่ให้เบียดบังฟังก์ชันอื่น)
            _refreshTimer.Interval = TimeSpan.FromSeconds(3);
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();
        }

        private void StopRealTimeRefresh()
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer.Tick -= RefreshTimer_Tick;
                _refreshTimer = null;
            }
        }

        private async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            if (_viewModel == null) return;

            // 🛡️ เช็กสถานะการใช้งานหน้าจอ
            if (chkAutoScroll.IsChecked == true && _isUserInteracting) return;

            // ตรวจจับว่าผู้ใช้กำลังแก้ไขข้อมูลคาไว้ที่เซลล์อยู่หรือไม่ ชัวร์ที่สุดครับ
            var cellEditMode = dgStore.CurrentCell != null && dgStore.IsReadOnly == false && _isEditing;
            if (cellEditMode) return;

            try
            {
                await _viewModel.UpdateStockFromDbAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in View RefreshTimer_Tick: {ex.Message}");
            }
        }

        #endregion

        #region --- Filter Button Events ---

        private void cbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel == null) return;
            string searchKey = txtSearch != null ? txtSearch.Text : "";
            _viewModel.LoadData(searchKey);
        }

        private void btnOverMax_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is StoreMaxMinViewModel vm)
            {
                vm.SelectedFilterType = "OVER_MAX";
                vm.LoadData(txtSearch.Text);
            }
        }

        private void btnUnderMin_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is StoreMaxMinViewModel vm)
            {
                vm.SelectedFilterType = "UNDER_MIN";
                vm.LoadData(txtSearch.Text);
            }
        }

        private void btnShowAll_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SelectedFilterType = "";
            _viewModel.SelectedCategory = "ALL CATEGORIES";
            if (txtSearch != null) txtSearch.Text = "";

            _viewModel.LoadData("");
        }

        #endregion

        #region --- Auto-Scroll Logic ---

        // ⏱️ ปรับค่าความเร็วในการเลื่อนทีละนิด (0.5 คือนุ่มนวล ค่อย ๆ ลงช้า ๆ ครับ)
        private double _scrollIncrement = 0.7;

        // 🔁 หมุนวนทิศทางเดียว (เลื่อนขึ้นต่อเนื่องเหมือนป้ายโฆษณา) แทนการเลื่อนขึ้น-ลงสลับไปมาแบบเดิม
        // พอสุดล่างแล้วให้ตัดกลับขึ้นบนสุดทันที ไม่สลับทิศ
        private int _loopPauseFrames = 0;
        private const int LoopPauseFrameCount = 25; // หยุดพักสั้นๆ ที่จุดเริ่มต้นก่อนวนใหม่ (~1.5 วินาทีที่ 60ms/frame)

        private void InitializeAutoScroll()
        {
            _interactionResetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _interactionResetTimer.Tick += (s, e) =>
            {
                _interactionResetTimer.Stop();
                _isUserInteracting = false;
            };

            // ล้างบอร์ดแคสต์เก่าออกเพื่อความชัวร์
            if (_autoScrollTimer != null)
            {
                _autoScrollTimer.Stop();
                _autoScrollTimer.Tick -= AutoScrollTimer_Tick;
                _autoScrollTimer = null;
            }

            // 🆕 ใช้ Timer ตัวใหม่ช่วยล็อก Frame Rate ของสกรอลล์ให้วิ่งช้าและสมูทคงที่
            _autoScrollTimer = new DispatcherTimer();
            _autoScrollTimer.Interval = TimeSpan.FromMilliseconds(60); // ทำงานทุกๆ 15ms (ได้ประมาณ 60 FPS ค่อย ๆ สไลด์)
            _autoScrollTimer.Tick += AutoScrollTimer_Tick;
            _autoScrollTimer.Start();
        }

        private void AutoScrollTimer_Tick(object sender, EventArgs e)
        {
            if (_isEditing || _isUserInteracting || chkAutoScroll.IsChecked != true) return;

            var scrollViewer = GetVisualChild<ScrollViewer>(dgStore);
            if (scrollViewer != null && scrollViewer.ScrollableHeight > 0)
            {
                if (_loopPauseFrames > 0)
                {
                    _loopPauseFrames--;
                    return;
                }

                // เลื่อนขึ้นทิศทางเดียวต่อเนื่อง (ไม่สลับขึ้น-ลง) เหมือนป้ายโฆษณาหมุนวน
                _currentScrollOffset += _scrollIncrement;

                if (_currentScrollOffset >= scrollViewer.ScrollableHeight)
                {
                    // ถึงล่างสุดแล้ว วนกลับขึ้นบนสุดทันที แล้วหยุดพักสั้นๆ ก่อนเริ่มเลื่อนรอบใหม่
                    _currentScrollOffset = 0;
                    _loopPauseFrames = LoopPauseFrameCount;
                }

                scrollViewer.ScrollToVerticalOffset(_currentScrollOffset);
            }
        }

        private void StopAutoScroll()
        {
            if (_autoScrollTimer != null)
            {
                _autoScrollTimer.Stop();
                _autoScrollTimer.Tick -= AutoScrollTimer_Tick;
                _autoScrollTimer = null;
            }
            if (_interactionResetTimer != null)
            {
                _interactionResetTimer.Stop();
            }
        }

        private void dgStore_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            UserActiveTrigger();
        }

        private void dgStore_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            UserActiveTrigger();
        }

        private void UserActiveTrigger()
        {
            if (chkAutoScroll.IsChecked == true)
            {
                _isUserInteracting = true;
                _interactionResetTimer?.Stop();
                _interactionResetTimer?.Start();
            }
        }

        private void chkAutoScroll_Checked(object sender, RoutedEventArgs e)
        {
            var scrollViewer = GetVisualChild<ScrollViewer>(dgStore);
            if (scrollViewer != null)
            {
                _currentScrollOffset = scrollViewer.VerticalOffset;
            }
            _isUserInteracting = false;
            _interactionResetTimer?.Stop();
        }

        private void chkAutoScroll_Unchecked(object sender, RoutedEventArgs e)
        {
            _interactionResetTimer?.Stop();
            _isUserInteracting = false;
        }

        #endregion

        #region --- DataGrid Events ---
        private void dgStore_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange > 0 && e.ExtentHeight > 0)
            {
                if (e.VerticalOffset >= (e.ExtentHeight - e.ViewportHeight) - 20)
                {
                    _viewModel.LoadData(txtSearch.Text, isLoadMore: true);
                }
            }

            if (_isUserInteracting || chkAutoScroll.IsChecked != true)
            {
                _currentScrollOffset = e.VerticalOffset;
            }
        }

        private void dgStore_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            var p = e.Row.Item as StoreProductModel;
            if (p != null)
            {
                _originalData = new StoreProductModel
                {
                    PartCode = p.PartCode,
                    Remark = p.Remark,
                    Max = p.Max,
                    Min = p.Min,
                    Qty = p.Qty
                };
            }

            string header = e.Column.Header.ToString().ToUpper();
            if (header.Contains("REMARK"))
            {
                if (p != null) p.IsRemarkEditing = true;
                return;
            }

            var stkPermission = _viewModel.CurrentUser?.Permissions.FirstOrDefault(p => p.SystemId == "STK");
            if (stkPermission == null || !stkPermission.CanEdit)
            {
                DialogHelper.ShowWarning("คุณไม่มีสิทธิ์แก้ไขข้อมูลหลัก (Stock-Max-Min)", "ACCESS DENIED");
                e.Cancel = true;
            }
            else
            {
                _isEditing = true;
            }
        }

        private async void dgStore_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
{
    if (e.EditAction == DataGridEditAction.Commit)
    {
        var product = e.Row.Item as StoreProductModel;
        if (product != null && _originalData != null)
        {
            try
            {
                // 🔒 ล็อกสถานะไว้ต่อ ป้องกันระบบ Realtime ดึงข้อมูลมาทับระหว่างยิงเน็ตเวิร์กอัปเดตฐานข้อมูล
                _isEditing = true; 

                bool success = await _viewModel.ProcessUpdate(product, _originalData);
                product.IsRemarkEditing = false;

                if (!success)
                {
                    DialogHelper.ShowError("ไม่สามารถบันทึกข้อมูลได้");
                    _viewModel.LoadData(txtSearch.Text);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CellEditEnding Error: {ex.Message}");
            }
            finally
            {
                // 🔓 ปลดล็อกให้ระบบ Realtime ทำงานได้ตามปกติเมื่อบันทึกเสร็จชัวร์ๆ แล้ว
                _originalData = null;
                _isEditing = false;
            }
        }
    }
    else
    {
        // กรณีผู้ใช้กด Cancel (Esc) ให้ปลดล็อกได้เลย
        _originalData = null;
        _isEditing = false;
    }
}

        #endregion

        #region --- General Logic ---

        private void RunEntryAnimation()
        {
            TimeSpan duration = TimeSpan.FromSeconds(0.6);
            IEasingFunction ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            DoubleAnimation fadeIn = new DoubleAnimation { From = 0, To = 1, Duration = duration };
            DoubleAnimation slideUp = new DoubleAnimation { From = 30, To = 0, Duration = duration, EasingFunction = ease };

            this.BeginAnimation(Page.OpacityProperty, fadeIn);
            if (PageTransform != null) PageTransform.BeginAnimation(TranslateTransform.YProperty, slideUp);
        }

        private T GetVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T) return (T)child;
                var result = GetVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        #endregion

        #region --- Search Events ---

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            if (txtSearch != null)
            {
                _viewModel.LoadData(txtSearch.Text);
            }
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_searchDebounceTimer != null) _searchDebounceTimer.Stop();

            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _searchDebounceTimer.Tick += (s, ev) =>
            {
                _searchDebounceTimer.Stop();
                if (!_isEditing)
                {
                    _viewModel.LoadData(txtSearch.Text);
                }
            };
            _searchDebounceTimer.Start();
        }

        private void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Keyboard.ClearFocus();
                _viewModel.LoadData(txtSearch.Text);
                e.Handled = true;
            }
        }

        #endregion
    }
}