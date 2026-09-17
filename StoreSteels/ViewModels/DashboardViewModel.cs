//using System;
//using System.ComponentModel;
//using System.Collections.ObjectModel;
//using System.Runtime.CompilerServices;
//using StoreSteels.Models;
//using StoreSteels.Services;
//using LiveCharts;
//using LiveCharts.Wpf;
//using System.Windows.Media;

//namespace StoreSteels.ViewModels
//{
//    public class DashboardViewModel : INotifyPropertyChanged
//    {
//        private readonly DashboardService _dashboardService;

//        // ข้อมูลหลักสำหรับ Binding เข้าคุม Control
//        public ObservableCollection<DashboardSummaryCard> Cards { get; set; }

//        // ตัวแปรสำหรับคุมระบบชาร์ตของ LiveCharts
//        public SeriesCollection PieSeriesCollection { get; set; }
//        public SeriesCollection BarSeriesCollection { get; set; }
//        public string[] ChartLabels { get; set; }
//        public Func<double, string> ValuesFormatter { get; set; }

//        private string _currentMonthYearLabel;
//        public string CurrentMonthYearLabel
//        {
//            get => _currentMonthYearLabel;
//            set { _currentMonthYearLabel = value; OnPropertyChanged(); }
//        }

//        public DashboardViewModel()
//        {
//            _dashboardService = new DashboardService();
//            CurrentMonthYearLabel = DateTime.Now.ToString("MMMM yyyy");

//            // 1. โหลดข้อมูลลงการ์ดสรุป
//            Cards = new ObservableCollection<DashboardSummaryCard>(_dashboardService.GetSummaryCards());

//            // 2. ประมวลผลและสร้างชาร์ตวงกลม (Pie Chart)
//            LoadPieChartData();

//            // 3. ประมวลผลและสร้างชาร์ตแท่งเปรียบเทียบ (Bar Chart)
//            LoadBarChartData();
//        }

//        //private void LoadPieChartData()
//        //{
//        //    PieSeriesCollection = new SeriesCollection();
//        //    var customerOrders = _dashboardService.GetMonthlyCustomerOrders();

//        //    foreach (var order in customerOrders)
//        //    {
//        //        PieSeriesCollection.Add(new PieSeries
//        //        {
//        //            Title = order.CustomerName,
//        //            Values = new ChartValues<double> { order.OrderAmount },
//        //            DataLabels = true,
//        //            LabelPoint = chartPoint => $"{chartPoint.Y} ({chartPoint.Participation:P0})",
//        //            Fill = (Brush)new BrushConverter().ConvertFromString(order.HexColor)
//        //        });
//        //    }
//        //}
//        private void LoadPieChartData()
//        {
//            PieSeriesCollection = new SeriesCollection();

//            // 🚀 เรียกดึงข้อมูลจาก Service ปกติ (กรองเดือนปัจจุบันจาก SQL View แล้ว)
//            var customerOrders = _dashboardService.GetMonthlyCustomerOrders();

//            foreach (var order in customerOrders)
//            {
//                PieSeriesCollection.Add(new PieSeries
//                {
//                    Title = order.CustomerName,
//                    Values = new ChartValues<double> { order.OrderAmount },
//                    DataLabels = true,

//                    // 🍰 ตัด InnerRadius ออกแล้ว เหลือแค่ PushOut เพื่อดันชิ้นเค้กให้แยกกันสวย ๆ
//                    PushOut = 2,

//                    LabelPoint = chartPoint => $"{chartPoint.Y:N0} ({chartPoint.Participation:P0})",
//                    Fill = (Brush)new BrushConverter().ConvertFromString(order.HexColor)
//                });
//            }
//        }

//        // แสดงตามเดือนที่ import Forecast Order Automation
//        //private void LoadBarChartData()
//        //{
//        //    var forecastOrders = _dashboardService.GetYearlyForecastOrder();

//        //    var forecastValues = new ChartValues<double>();
//        //    var orderValues = new ChartValues<double>();
//        //    var labels = new string[forecastOrders.Count];

//        //    for (int i = 0; i < forecastOrders.Count; i++)
//        //    {
//        //        forecastValues.Add(forecastOrders[i].ForecastValue);
//        //        orderValues.Add(forecastOrders[i].OrderValue);
//        //        labels[i] = forecastOrders[i].MonthName;
//        //    }

//        //    BarSeriesCollection = new SeriesCollection
//        //    {
//        //        new ColumnSeries
//        //        {
//        //            Title = "Forecast",
//        //            Values = forecastValues,
//        //            // #D1C4E9 = ชอบสีนี้สีม่วงลาเวนเดอร์พาสเทล #B39DDB = เข้มกว่าเดิมสีม่วงสว่างแนวโมเดิร์น
//        //            // #90CAF9 = สีฟ้าอ่อนพาสเทล
//        //            Fill = (Brush)new BrushConverter().ConvertFromString("#90CAF9")
//        //        },
//        //        new ColumnSeries
//        //        {
//        //            Title = "Order",
//        //            Values = orderValues,
//        //            Fill = (Brush)new BrushConverter().ConvertFromString("#6A1B9A")
//        //        }
//        //    };

//        //    ChartLabels = labels;
//        //    ValuesFormatter = value => value.ToString("N0"); // ฟอร์แมตแสดงคอมม่าเช่น 1,000,000
//        //}
//        private void LoadBarChartData()
//        {
//            var forecastOrders = _dashboardService.GetYearlyForecastOrder();

//            var forecastValues = new ChartValues<double>();
//            var orderValues = new ChartValues<double>();
//            var deliveryValues = new ChartValues<double>(); // 🚀 1. สร้างชุดข้อมูลแท่งใหม่
//            var labels = new string[forecastOrders.Count];

//            for (int i = 0; i < forecastOrders.Count; i++)
//            {
//                forecastValues.Add(forecastOrders[i].ForecastValue);
//                orderValues.Add(forecastOrders[i].OrderValue);
//                deliveryValues.Add(forecastOrders[i].DeliveryValue); // 🚀 2. แอดข้อมูลจริงใส่ List
//                labels[i] = forecastOrders[i].MonthName;
//            }

//            BarSeriesCollection = new SeriesCollection
//    {
//        new ColumnSeries
//        {
//            Title = "Forecast",
//            Values = forecastValues,
//            Fill = (Brush)new BrushConverter().ConvertFromString("#90CAF9") // สีฟ้าอ่อนพาสเทลเดิม
//        },
//        new ColumnSeries
//        {
//            Title = "Order",
//            Values = orderValues,
//            Fill = (Brush)new BrushConverter().ConvertFromString("#6A1B9A") // สีม่วงสว่างเดิม
//        },
//        new ColumnSeries
//        {
//            Title = "Delivery", // 🚀 3. เพิ่มแท่งจัดส่งจริงเข้าไปในกราฟ
//            Values = deliveryValues,
//            // เลือกใช้สีเขียวโมเดิร์นพาสเทล (#4DB6AC) ให้ดูเป็นยอดที่เสร็จสิ้นสมบูรณ์ สบายตาและตัดกับสีเดิมได้ดีครับ
//            Fill = (Brush)new BrushConverter().ConvertFromString("#B39DDB")
//        }
//    };

//            ChartLabels = labels;
//            ValuesFormatter = value => value.ToString("N0");
//        }


//        // แบบแสดงกราฟพร้อมกัน 12 แท่ง
//        //    private void LoadBarChartData()
//        //    {
//        //        // ดึงข้อมูลจาก Service (ซึ่งจะได้รับข้อมูลครบ 12 แถวเสมอ)
//        //        var forecastOrders = _dashboardService.GetYearlyForecastOrder();

//        //        var forecastValues = new ChartValues<double>();
//        //        var orderValues = new ChartValues<double>();
//        //        var labels = new string[forecastOrders.Count];

//        //        for (int i = 0; i < forecastOrders.Count; i++)
//        //        {
//        //            forecastValues.Add(forecastOrders[i].ForecastValue);
//        //            orderValues.Add(forecastOrders[i].OrderValue);
//        //            labels[i] = forecastOrders[i].MonthName; // จะได้ค่า 'Jan', 'Feb', 'Mar' ตามลำดับจาก DB
//        //        }

//        //        BarSeriesCollection = new SeriesCollection
//        //{
//        //    new ColumnSeries
//        //    {
//        //        Title = "Forecast",
//        //        Values = forecastValues,
//        //        Fill = (Brush)new BrushConverter().ConvertFromString("#CBD5E0")
//        //    },
//        //    new ColumnSeries
//        //    {
//        //        Title = "Order",
//        //        Values = orderValues,
//        //        Fill = (Brush)new BrushConverter().ConvertFromString("#6A1B9A")
//        //    }
//        //};

//        //        ChartLabels = labels; // ส่งแกน X ขนาด 12 เดือนไปให้ UI
//        //        ValuesFormatter = value => value.ToString("N0");
//        //    }


//        public event PropertyChangedEventHandler PropertyChanged;
//        protected void OnPropertyChanged([CallerMemberName] string name = null)
//        {
//            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
//        }
//    }
//}


using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using System.Windows;
using StoreSteels.Models;
using StoreSteels.Services;
using LiveCharts;
using LiveCharts.Wpf;
using System.Windows.Media;
using System.Linq; // เพิ่มเข้ามาเพื่อช่วยค้นหาข้อมูลได้ง่ายขึ้น

namespace StoreSteels.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        private readonly DashboardService _dashboardService;
        private DispatcherTimer _refreshTimer;

        public ObservableCollection<DashboardSummaryCard> Cards { get; set; }
        public SeriesCollection PieSeriesCollection { get; set; }
        public SeriesCollection BarSeriesCollection { get; set; }
        public string[] ChartLabels { get; set; }
        public Func<double, string> ValuesFormatter { get; set; }

        private string _currentMonthYearLabel;
        public string CurrentMonthYearLabel
        {
            get => _currentMonthYearLabel;
            set { _currentMonthYearLabel = value; OnPropertyChanged(); }
        }

        public DashboardViewModel()
        {
            _dashboardService = new DashboardService();
            CurrentMonthYearLabel = DateTime.Now.ToString("MMMM yyyy");

            // 🚀 โหลดข้อมูลลงการ์ดครั้งแรกสุดตอนเปิดหน้าจอ (สร้างออบเจกต์ไว้เซ็ตติ้งครั้งเดียว)
            Cards = new ObservableCollection<DashboardSummaryCard>(_dashboardService.GetSummaryCards());

            // โหลดข้อมูลชาร์ตต่างๆ
            LoadPieChartData();
            LoadBarChartData();

            // 🚀 เริ่มทำงานระบบจับเวลาอัปเดตอัตโนมัติ
            SetupRefreshTimer();
        }

        #region === [ Function : Auto-Refresh Data or Update Data to Realtime ] ===

        private void SetupRefreshTimer()
        {
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(0); // 🕒 เทสเรียลไทม์ที่ 10 วินาทีครับ (ถ้าใช้งานจริงค่อยแก้กลับเป็น 30 นาทีนะ)
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            RefreshDashboardData();
        }

        /// <summary>
        /// ฟังก์ชันศูนย์กลางในการเจาะจงอัปเดตเฉพาะตัวเลข Max และ Min Product แบบไม่ให้หน้าจอกระตุก
        /// </summary>
        private void RefreshDashboardData()
        {
            try
            {
                // ดึงข้อมูลใหม่จาก DB
                var freshCards = _dashboardService.GetSummaryCards();
                if (freshCards == null || Cards == null) return;

                // ดึง Dispatcher ของแอปพลิเคชันหลักมาเตรียมอัปเดต UI
                var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

                foreach (var currentCard in Cards)
                {
                    if (currentCard.Title == null) continue;

                    string titleTrimmed = currentCard.Title.Trim();

                    if (titleTrimmed.Equals("Max Product", StringComparison.OrdinalIgnoreCase))
                    {
                        var freshData = freshCards.FirstOrDefault(c => c.Title != null && c.Title.Trim().Equals("Max Product", StringComparison.OrdinalIgnoreCase));
                        if (freshData != null && currentCard.Value != freshData.Value)
                        {
                            // บังคับอัปเดตผ่าน UI Thread เพื่อความชัวร์สูงสุด
                            dispatcher.Invoke(() => {
                                currentCard.Value = freshData.Value;
                            });
                        }
                    }
                    else if (titleTrimmed.Equals("Min Product", StringComparison.OrdinalIgnoreCase))
                    {
                        var freshData = freshCards.FirstOrDefault(c => c.Title != null && c.Title.Trim().Equals("Min Product", StringComparison.OrdinalIgnoreCase));
                        if (freshData != null && currentCard.Value != freshData.Value)
                        {
                            dispatcher.Invoke(() => {
                                currentCard.Value = freshData.Value;
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Dashboard Auto-Refresh Error]: {ex.Message}");
            }
        }

        #endregion

        #region === [ Function : Graph Circle & Stack ] ===

        // กราฟวงกลมแสดงสัดส่วนลูกค้าแสดง Order เฉพาะเดือนปัจจุบัน
        private void LoadPieChartData()
        {
            PieSeriesCollection = new SeriesCollection();
            var customerOrders = _dashboardService.GetMonthlyCustomerOrders();

            foreach (var order in customerOrders)
            {
                PieSeriesCollection.Add(new PieSeries
                {
                    Title = order.CustomerName,
                    Values = new ChartValues<double> { order.OrderAmount },
                    DataLabels = true,
                    PushOut = 2,
                    LabelPoint = chartPoint => $"{chartPoint.Y:N0} ({chartPoint.Participation:P0})",
                    Fill = (Brush)new BrushConverter().ConvertFromString(order.HexColor)
                });
            }
            OnPropertyChanged(nameof(PieSeriesCollection));
        }

        // กราฟแท่งแสดงการเทียบยอด Forecast, Order และ Delivery แสดงตามเดือนที่ import Forecast Order Automation
        private void LoadBarChartData()
        {
            var forecastOrders = _dashboardService.GetYearlyForecastOrder();
            var forecastValues = new ChartValues<double>();
            var orderValues = new ChartValues<double>();
            var deliveryValues = new ChartValues<double>();
            var labels = new string[forecastOrders.Count];

            for (int i = 0; i < forecastOrders.Count; i++)
            {
                forecastValues.Add(forecastOrders[i].ForecastValue);
                orderValues.Add(forecastOrders[i].OrderValue);
                deliveryValues.Add(forecastOrders[i].DeliveryValue);
                labels[i] = forecastOrders[i].MonthName;
            }

            BarSeriesCollection = new SeriesCollection
            {
                new ColumnSeries { Title = "Forecast", Values = forecastValues, Fill = (Brush)new BrushConverter().ConvertFromString("#90CAF9") },
                new ColumnSeries { Title = "Order", Values = orderValues, Fill = (Brush)new BrushConverter().ConvertFromString("#6A1B9A") },
                new ColumnSeries { Title = "Delivery", Values = deliveryValues, Fill = (Brush)new BrushConverter().ConvertFromString("#B39DDB") }
            };

            ChartLabels = labels;
            ValuesFormatter = value => value.ToString("N0");

            OnPropertyChanged(nameof(BarSeriesCollection));
            OnPropertyChanged(nameof(ChartLabels));
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}