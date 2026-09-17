using StoreSteels.Helpers;
using StoreSteels.Services;
using StoreSteels.Models;
using StoreSteels.Converters;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace StoreSteels.Views
{
    public partial class MainView : Window
    {
        public UserSession CurrentUser { get; private set; }                                                                // ประกาศเรียกใช้งาน Models
        private AuthService _authService = new AuthService();                                                               // ประกาศเรียกใช้งาน Service
        public static readonly DependencyProperty IsSidebarOpenProperty =                                                   // ใช้ DependencyProperty เพื่อให้รองรับ Binding ใน XAML
            DependencyProperty.Register("IsSidebarOpen", typeof(bool), typeof(MainView), new PropertyMetadata(true));

        public bool IsSidebarOpen
        {
            get { return (bool)GetValue(IsSidebarOpenProperty); }
            set { SetValue(IsSidebarOpenProperty, value); }
        }

        #region === [ MainView ] ===
        public MainView(UserSession session)        // <-- รับ UserSession มาจาก LoginView
        {
            InitializeComponent();
            this.CurrentUser = session;             // เก็บข้อมูลคน Login
            this.DataContext = this;                // ทำให้ Binding {Binding CurrentUser.UserLevel} ทำงานได้
            this.Closing += MainView_Closing;       // [เพิ่ม] ดักจับการปิด Window ทุกกรณีรวมถึงกด X เพื่อให้ UserID อัพเดทสถานะเป็น Offline เสมอ

            #region === [ Initial Navigation Logic : ตัดสินใจนำทางไปยังหน้าแรกที่เหมาะสมตามสิทธิ์การใช้งาน ตัวเก่าแบบไม่มีเงื่อนไข ] ===
            // เริ่มต้นที่หน้าแรก
            //อันเก่าที่ไม่มีการกำหนดสิทธิ์การใช้งาน
            //NavigateToPage(new DashboardView(this.CurrentUser), "DASHBOARD"); 
            #endregion

            #region === [ Initial Navigation Logic : ตัดสินใจนำทางไปยังหน้าแรกที่เหมาะสมตามสิทธิ์การใช้งาน ตัวใหม่แบบมีเงื่อนไข ] ===

            //// 1. ถ้าเป็น Admin (Level 1, 2) ให้ไปหน้า Dashboard ตามปกติ
            //if (CurrentUser.UserLevel == 1 || CurrentUser.UserLevel == 2)
            //{
            //    NavigateToPage(new DashboardView(this.CurrentUser), "DASHBOARD");
            //}
            //// 2. ถ้าเป็นพนักงาน ScanIn (และมีสิทธิ์) ให้ไปหน้า ScanIn เลย
            //else if (CurrentUser.CanViewScanIn && !CurrentUser.CanViewScanOut)
            //{
            //    NavigateToPage(new ScanInView(this.CurrentUser), "MULTI-SCANNER ( IN )");
            //}
            //// 3. ถ้าเป็นพนักงาน ScanOut (และมีสิทธิ์) ให้ไปหน้า ScanOut เลย
            //else if (CurrentUser.CanViewScanOut && !CurrentUser.CanViewScanIn)
            //{
            //    NavigateToPage(new ScanOutView(this.CurrentUser), "MULTI-SCANNER ( OUT )");
            //}
            //// 4. กรณีอื่นๆ (เผื่อไว้)
            //else
            //{
            //    NavigateToPage(new DashboardView(this.CurrentUser), "DASHBOARD");
            //}
            if (CurrentUser.CanViewDashboard)
            {
                NavigateToPage(new DashboardView(this.CurrentUser), "DASHBOARD");
            }
            // ถ้าเป็นพนักงาน ScanIn (และมีสิทธิ์) ให้ไปหน้า ScanIn เลย
            else if (CurrentUser.CanViewScanIn && !CurrentUser.CanViewScanOut)
            {
                NavigateToPage(new ScanInView(this.CurrentUser), "MULTI-SCANNER ( IN )");
            }
            // ถ้าเป็นพนักงาน ScanOut (และมีสิทธิ์) ให้ไปหน้า ScanOut เลย
            else if (CurrentUser.CanViewScanOut && !CurrentUser.CanViewScanIn)
            {
                NavigateToPage(new ScanOutView(this.CurrentUser), "MULTI-SCANNER ( OUT )");
            }
            // มีสิทธิ์ทั้ง ScanIn และ ScanOut -> เปิด ScanIn เป็นค่าเริ่มต้น
            else if (CurrentUser.CanViewScanIn && CurrentUser.CanViewScanOut)
            {
                NavigateToPage(new ScanInView(this.CurrentUser), "MULTI-SCANNER ( IN )");
            }
            // 🆕 มีสิทธิ์ ProductControl
            else if (CurrentUser.CanViewProductControl)
            {
                NavigateToPage(new ProductControlView(this.CurrentUser), "INVENTORY REGISTRATION");
            }
            // กรณีอื่นๆ (ไม่มีสิทธิ์อะไรเลยจริงๆ เผื่อกันพัง — ทางปฏิบัติไม่ควรเกิดถ้าตั้ง Permission ครบ)
            else
            {
                NavigateToPage(new DashboardView(this.CurrentUser), "DASHBOARD");
            }

            #endregion

        }
        #endregion

        #region === [ Window Closing Event Handler : ออกสู่ระบบด้วย X Windows ] ===

        private void MainView_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // 1. เช็คว่ามี User ค้างอยู่จริงๆ ไหม
                if (this.CurrentUser != null && !string.IsNullOrEmpty(this.CurrentUser.UserId))
                {
                    // 2. เรียกใช้ Service อัปเดต (ตรวจสอบว่า UpdateLogoutStatus ไม่ได้เป็น async)
                    var auth = new AuthService();
                    auth.UpdateLogoutStatus(this.CurrentUser.UserId);

                    // 3. (Optional) เขียน Log ไว้หน่อยว่าปิดด้วยปุ่ม X
                    LogService.WriteLog(CurrentUser.UserId, "EXIT", "ผู้ใช้งานสั่งปิดโปรแกรมโดยตรง ( Window Closing )", "");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error on Closing: {ex.Message}");
            }
        }

        #endregion

        #region === [ Toggle Sidebar Animation : การหดเข้ากับการขยายออกของ Sidebar ] ===

        private void btnToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            // Responsive Logic: 75px สำหรับไอคอนอย่างเดียว, 260px สำหรับเมนูเต็ม
            double targetWidth = IsSidebarOpen ? 80 : 260;

            DoubleAnimation animation = new DoubleAnimation
            {
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(350),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            SidebarContainer.BeginAnimation(WidthProperty, animation);
            IsSidebarOpen = !IsSidebarOpen;
        }

        #endregion

        #region === [ Navigation Helper : ฟังก์ชั่นกลางสำหรับการนำทางไปยังหน้าอื่นๆ ] ===

        public void NavigateToPage(object page, string title)
        {
            txtPageTitle.Text = title.ToUpper();
            MainFrame.Navigate(page);
        }

        #endregion

        #region Navigation Sidebar Events

        //private void BtnDashboard_Click(object sender, RoutedEventArgs e)
        // => NavigateToPage(new DashboardView(this.CurrentUser), "DASHBOARD");
        // ✅ เพิ่ม Guard Check สิทธิ์ ให้ Dashboard ด้วย (เดิมไม่มี)
        private void BtnDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (!CurrentUser.CanViewDashboard)
            {
                NotificationManager.Show("Access Denied", "คุณไม่มีสิทธิ์เข้าใช้งานหน้า Dashboard", false);
                return;
            }
            NavigateToPage(new DashboardView(this.CurrentUser), "DASHBOARD");
        }

        private void BtnStore_Click(object sender, RoutedEventArgs e)
         => NavigateToPage(new StoreMaxMinView(this.CurrentUser), "STORE ( MAX-MIN )");

        #region === [ ปุ่มสำหรับการใช้งานรูปแบบเก่า เเบบไม่กำหนดสิทธิ์หรือเงื่อนไขการใช้งาน ] ===

        //    private void BtnScanIn_Click(object sender, RoutedEventArgs e)
        //=> NavigateToPage(new ScanInView(this.CurrentUser), "MULTI-SCANNER ( IN )");

        //    private void BtnScanOut_Click(object sender, RoutedEventArgs e) 
        //=> NavigateToPage(new ScanOutView(this.CurrentUser), "MULTI-SCANNER ( OUT )");

        #endregion
        private void BtnScanIn_Click(object sender, RoutedEventArgs e)
        {
            // ถ้าพนักงานไม่มีสิทธิ์เข้าใช้ระบบ ScanIn จะไม่ยอมให้โหลดหน้าต่างขึ้นมา
            if (!CurrentUser.CanViewScanIn)
            {
                NotificationManager.Show("Access Denied", "คุณไม่มีสิทธิ์เข้าใช้งานระบบจัดเก็บสินค้าเข้า (Scan In)", false);
                return;
            }
            NavigateToPage(new ScanInView(this.CurrentUser), "MULTI-SCANNER ( IN )");
        }

        private void BtnScanOut_Click(object sender, RoutedEventArgs e)
        {
            // ถ้าพนักงานไม่มีสิทธิ์เข้าใช้ระบบ ScanOut จะไม่ยอมให้โหลดหน้าต่างขึ้นมา
            if (!CurrentUser.CanViewScanOut)
            {
                NotificationManager.Show("Access Denied", "คุณไม่มีสิทธิ์เข้าใช้งานระบบเบิกจ่ายสินค้าออก (Scan Out)", false);
                return;
            }
            NavigateToPage(new ScanOutView(this.CurrentUser), "MULTI-SCANNER ( OUT )");
        }

        //private void BtnProductControl_Click(object sender, RoutedEventArgs e)
        // => NavigateToPage(new ProductControlView(this.CurrentUser), "INVENTORY REGISTRATION");
        // ✅ เพิ่ม Guard Check สิทธิ์
        private void BtnProductControl_Click(object sender, RoutedEventArgs e)
        {
            if (!CurrentUser.CanViewProductControl)
            {
                NotificationManager.Show("Access Denied", "คุณไม่มีสิทธิ์เข้าใช้งานหน้า Inventory Registration", false);
                return;
            }
            NavigateToPage(new ProductControlView(this.CurrentUser), "INVENTORY REGISTRATION");
        }

        #endregion

        #region === [ Reserved for Future Development : สำรองไว้เผื่อได้พัฒนาในอนาคต ] ===
        // สำรองไว้เผื่อได้พัฒนาในอนาคต
        //    private void BtnPR_Click(object sender, RoutedEventArgs e)
        //=> NavigateToPage(new PRView(this.CurrentUser), "PURCHASE REQUEST");

        // สำรองไว้เผื่อได้พัฒนาในอนาคต
        //    private void BtnManageUser_Click(object sender, RoutedEventArgs e) 
        //=> NavigateToPage(new ManageUserView(), "USER ACCESS");

        // สำรองไว้เผื่อได้พัฒนาในอนาคต
        //private void btnProductControls_Click(object sender, RoutedEventArgs e)
        // => NavigateToPage(new ProductControlView(), "PRODUCT CONTROLS");

        #endregion

        #region === [ Logout Buttons : ปุ่มออกสู่ระบบเลิกใช้งาน ] ===

        private async void Logout_Click(object sender, RoutedEventArgs e)
        {
            bool isConfirm = DialogHelper.ShowConfirm("คุณต้องการออกจากระบบใช่หรือไม่?", "CONFIRM LOGOUT");

            if (isConfirm)
            {
                _authService.UpdateLogoutStatus(CurrentUser.UserId);                          // 1. สั่ง Offline 
                LogService.WriteLog(CurrentUser.UserId, "LOGOUT", "ออกจากระบบสำเร็จ", "");       // 2. บันทึก Log การ Logout
                NotificationManager.Show("Logout", "ออกจากระบบสำเร็จ", true);                    // 3. แสดง Notification การ Logout
                this.IsHitTestVisible = false;
                await Task.Delay(800);                                                        // 4. กำหนด Delay เล็กน้อยเพื่อให้ Notification แสดงก่อนปิดหน้าต่าง
                new LoginView().Show();                                                       // 5. กลับไปหน้า Login
                this.Closing -= MainView_Closing;                                             // ถอด Event ออกชั่วคราวเพื่อไม่ให้รันซ้ำตอน Close()
                this.Close();
            }
        }

        #endregion

        #region === [ Sidebar Sub-Menu Animation ] ===
        private void ExpanderScanner_Expanded(object sender, RoutedEventArgs e)
        {
            // แอนิเมชั่นกางออก: จากความสูง 0 ไป 80 (ปุ่มละ 40px)
            DoubleAnimation ani = new DoubleAnimation(0, 80, TimeSpan.FromSeconds(0.3));
            ani.EasingFunction = new QuarticEase() { EasingMode = EasingMode.EaseOut };
            SubMenuStack.BeginAnimation(StackPanel.HeightProperty, ani);
        }

        private void ExpanderScanner_Collapsed(object sender, RoutedEventArgs e)
        {
            // แอนิเมชั่นหดกลับ
            DoubleAnimation ani = new DoubleAnimation(SubMenuStack.ActualHeight, 0, TimeSpan.FromSeconds(0.3));
            ani.EasingFunction = new QuarticEase() { EasingMode = EasingMode.EaseIn };
            SubMenuStack.BeginAnimation(StackPanel.HeightProperty, ani);
        }
        #endregion
    }
}