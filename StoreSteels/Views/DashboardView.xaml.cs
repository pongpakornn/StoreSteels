using StoreSteels.Models;
using StoreSteels.Services;
using StoreSteels.ViewModels;
using System;
using System.Collections.Generic; // อย่าลืมเปิดใช้งาน Namespace นี้สำหรับ List<string> ครับ
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace StoreSteels.Views
{
    public partial class DashboardView : Page
    {
        private DashboardViewModel _viewModel;
        private DashboardService _dashboardService;
        public UserSession CurrentUser { get; private set; }

        public DashboardView()
        {
            InitializeComponent();
            _dashboardService = new DashboardService();
            _viewModel = new DashboardViewModel();
            this.DataContext = _viewModel;

            RunEntryAnimation();
        }

        public DashboardView(UserSession session) : this()
        {
            this.CurrentUser = session;
        }

        private void RunEntryAnimation()
        {
            TimeSpan duration = TimeSpan.FromSeconds(0.6);
            DoubleAnimation fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = duration
            };
            this.BeginAnimation(Page.OpacityProperty, fadeIn);
        }

        /// <summary>
        /// 1. ระบบจัดการดับเบิ้ลคลิกบน CardBox (การ์ด 2, 3, 4)
        /// </summary>
        private void Card_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is DashboardSummaryCard card)
            {
                // ตรวจสอบว่าหน้าจอนี้เปิดอยู่ภายใต้ MainView หรือไม่
                if (Window.GetWindow(this) is MainView mainWindow)
                {
                    if (card.Title == "Product All")
                    {
                        // เรียกฟังก์ชันกลางของ MainView เพื่อเปลี่ยนหน้าและเปลี่ยนหัวข้อแถบด้านบนพร้อมกัน
                        mainWindow.NavigateToPage(new StoreMaxMinView(this.CurrentUser, "", "ALL"), "STORE ( MAX-MIN )");
                    }
                    else if (card.Title == "Max Product")
                    {
                        mainWindow.NavigateToPage(new StoreMaxMinView(this.CurrentUser, "", "OVER_MAX"), "STORE ( MAX-MIN )");
                    }
                    else if (card.Title == "Min Product")
                    {
                        mainWindow.NavigateToPage(new StoreMaxMinView(this.CurrentUser, "", "UNDER_MIN"), "STORE ( MAX-MIN )");
                    }
                }
                else
                {
                    // กรณีเปิดแบบ Standalone (Fallback สำรองไว้)
                    FallbackNavigation(card.Title);
                }
            }
        }

        /// <summary>
        /// 2. ระบบดึงรายชื่อลูกค้าลง ContextMenu อัตโนมัติเมื่อกดคลิกขวาที่การ์ด Customer
        /// </summary>
        private async void CustomerMenu_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is Border border && border.ContextMenu is ContextMenu menu)
            {
                menu.Items.Clear();
                menu.Items.Add(new MenuItem { Header = "⏳ กำลังโหลดรายชื่อลูกค้า...", IsEnabled = false });

                try
                {
                    List<string> customerList = await _dashboardService.GetActiveCustomersAsync();

                    this.Dispatcher.Invoke(() =>
                    {
                        menu.Items.Clear();

                        if (customerList == null || customerList.Count == 0)
                        {
                            menu.Items.Add(new MenuItem { Header = "❌ ไม่พบข้อมูลลูกค้า", IsEnabled = false });
                            return;
                        }

                        foreach (var custCode in customerList)
                        {
                            MenuItem item = new MenuItem { Header = $"🏢 {custCode}" };
                            item.Tag = custCode;
                            item.Click += CustomerMenuItem_Click;
                            menu.Items.Add(item);
                        }
                    });
                }
                catch (Exception ex)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        menu.Items.Clear();
                        menu.Items.Add(new MenuItem { Header = $"⚠️ ข้อผิดพลาด: {ex.Message}", IsEnabled = false });
                    });
                }
            }
        }

        /// <summary>
        /// 3. เมื่อเลือกชื่อลูกค้าในรายการคลิกขวา -> เปิดหน้า StoreMaxMinView คัดกรองเฉพาะลูกค้ารายนั้น
        /// </summary>
        private void CustomerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag != null)
            {
                string selectedCustomer = item.Tag.ToString();

                // ตรวจสอบและวิ่งไปใช้ฟังก์ชันกลางของ MainView เพื่ออัปเดตหัวข้อตัวหนังสือด้านบน
                if (Window.GetWindow(this) is MainView mainWindow)
                {
                    mainWindow.NavigateToPage(new StoreMaxMinView(this.CurrentUser, selectedCustomer, "ALL"), "STORE ( MAX-MIN )");
                }
                else
                {
                    // Fallback หากไม่ได้รันผ่าน MainView
                    var targetPage = new StoreMaxMinView(this.CurrentUser, selectedCustomer, "ALL");
                    this.NavigationService?.Navigate(targetPage);
                }
            }
        }

        /// <summary>
        /// 4. ระบบจัดการดับเบิ้ลคลิกบน CardBox (การ์ด 2, 3, 4) โดยใช้ MouseDown
        /// </summary>
        private void Card_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (sender is Border border && border.DataContext is DashboardSummaryCard card)
                {
                    if (Window.GetWindow(this) is MainView mainWindow)
                    {
                        if (card.Title == "Product All")
                        {
                            mainWindow.NavigateToPage(new StoreMaxMinView(this.CurrentUser, "", "ALL"), "STORE ( MAX-MIN )");
                        }
                        else if (card.Title == "Max Product")
                        {
                            mainWindow.NavigateToPage(new StoreMaxMinView(this.CurrentUser, "", "OVER_MAX"), "STORE ( MAX-MIN )");
                        }
                        else if (card.Title == "Min Product")
                        {
                            mainWindow.NavigateToPage(new StoreMaxMinView(this.CurrentUser, "", "UNDER_MIN"), "STORE ( MAX-MIN )");
                        }
                    }
                    else
                    {
                        FallbackNavigation(card.Title);
                    }
                }
            }
        }

        /// <summary>
        /// ฟังก์ชันสำรองกรณีหน้าจอไม่ได้ถูกเรียกผ่าน MainView (ช่วยให้โค้ดทำงานได้ไม่เด้ง Error)
        /// </summary>
        private void FallbackNavigation(string cardTitle)
        {
            StoreMaxMinView targetPage = null; // เปลี่ยนจาก Page เป็น StoreMaxMinView
            if (cardTitle == "Product All") targetPage = new StoreMaxMinView(this.CurrentUser, "", "ALL");
            else if (cardTitle == "Max Product") targetPage = new StoreMaxMinView(this.CurrentUser, "", "OVER_MAX");
            else if (cardTitle == "Min Product") targetPage = new StoreMaxMinView(this.CurrentUser, "", "UNDER_MIN");

            if (targetPage != null)
            {
                this.NavigationService?.Navigate(targetPage);
            }
        }
    }
}