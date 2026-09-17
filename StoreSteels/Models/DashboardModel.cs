using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StoreSteels.Models
{
    public class DashboardSummaryCard : INotifyPropertyChanged
    {
        private string _title;
        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged(); // 🚀 เพิ่มตัวนี้เข้ามาสะกิด DataTrigger ของ XAML ด้วยครับ
                }
            }
        }
        public string GifPath { get; set; }

        private string _value;
        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged(); // 🚀 ส่งสัญญาณบอก TextBlock บน XAML ให้เปลี่ยนเลขทันที
                }
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class CustomerOrderShare
    {
        public string CustomerName { get; set; }
        public double OrderAmount { get; set; }
        public double Percentage { get; set; }
        public string HexColor { get; set; }
    }

    public class MonthlyForecastOrderCompare
    {
        public string MonthName { get; set; }
        public double ForecastValue { get; set; }
        public double OrderValue { get; set; }

        public double DeliveryValue { get; set; } // 🚀 แท่งใหม่ที่เพิ่มเข้ามา
    }
}