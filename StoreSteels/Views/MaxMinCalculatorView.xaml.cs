using System;
using System.Windows.Controls;
using StoreSteels.Models;
using StoreSteels.ViewModels;

namespace StoreSteels.Views
{
    public partial class MaxMinCalculatorView : Page
    {
        public UserSession CurrentUser { get; private set; }

        public MaxMinCalculatorView(UserSession session)
        {
            InitializeComponent();
            this.CurrentUser = session;

            // ผูกหน้า UI เข้ากับระบอบงาน ViewModel หลัก
            this.DataContext = new MaxMinCalViewModel();
        }


    }
}