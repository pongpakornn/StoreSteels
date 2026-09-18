using StoreSteels.Helpers;
using StoreSteels.Models;
using StoreSteels.Services;
using StoreSteels.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace StoreSteels.Views
{
    public partial class PackingCardView : Page
    {
        private readonly PackingCardViewModel _viewModel;
        private readonly PackingCardPrintService _printService = new PackingCardPrintService();
        private readonly PackingPrintLogService _printLogService = new PackingPrintLogService();

        public PackingCardView(UserSession session)
        {
            InitializeComponent();

            _viewModel = new PackingCardViewModel();
            _viewModel.CurrentUser = session;
            _viewModel.PropertyChanged += (s, e) => UpdateItemCountText();

            this.DataContext = _viewModel;

            _viewModel.GroupedItems.CollectionChanged += (s, e) => UpdateItemCountText();

            RunEntryAnimation();
        }

        private void UpdateItemCountText()
        {
            int total = _viewModel.VisibleItems.Count;
            int selected = _viewModel.VisibleItems.Count(x => x.IsSelected);
            txtItemCount.Text = $"แสดง {total} รายการ  |  เลือกแล้ว {selected} รายการ";
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ApplyFilter();
            UpdateItemCountText();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ClearSearch();
            UpdateItemCountText();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox == null) return;

            bool isChecked = checkBox.IsChecked ?? false;
            foreach (var item in _viewModel.VisibleItems)
            {
                item.IsSelected = isChecked;
            }

            dgPackingCards.Items.Refresh();
            UpdateItemCountText();
        }

        private void RowCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // อัปเดตพรีวิวการ์ดแบบ real-time ตามแถวล่าสุดที่ถูกเลือก
            if (sender is CheckBox chk && chk.DataContext is PackingCardModel item)
            {
                _viewModel.PreviewItem = item;
            }
            UpdateItemCountText();
        }

        private void dgPackingCards_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // คลิกแถวไหน (ไม่ต้องติ๊ก checkbox) ให้ไปพรีวิวการ์ดฝั่งขวาทันที
            if (dgPackingCards.SelectedItem is PackingCardModel item)
            {
                _viewModel.PreviewItem = item;
            }
        }

        private void dgPackingCards_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Infinite scroll: โหลดเพิ่มทีละ 10 แถวเมื่อเลื่อนใกล้สุดล่าง กันข้อมูลเยอะแล้วเครื่องค้าง
            if (!_viewModel.HasMoreToLoad) return;

            if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 40)
            {
                _viewModel.LoadMore();
            }
        }

        private void PrintSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = _viewModel.GetSelectedItems();
            if (selected.Count == 0)
            {
                DialogHelper.ShowWarning("กรุณาเลือกรายการที่ต้องการพิมพ์อย่างน้อย 1 รายการครับ");
                return;
            }

            RunPrintFlow(selected);
        }

        private void RunPrintFlow(List<PackingCardModel> items)
        {
            var progressView = new PrintProgressView();
            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                progressView.Owner = Application.Current.MainWindow;
            }
            progressView.UpdateProgress(0, items.Count);
            progressView.Show();

            try
            {
                var printedItems = _printService.PrintCards(items, (current, total) =>
                {
                    progressView.UpdateProgress(current, total);
                    // ปั๊มข้อความ UI ให้หลอดโปรเกรสขยับจริงระหว่างพิมพ์ทีละใบ (loop นี้ทำงานบน UI thread)
                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
                });

                string userId = _viewModel.CurrentUser?.UserId ?? "Unknown";
                foreach (var item in printedItems)
                {
                    // บันทึกประวัติการพิมพ์ลง PackingPrintLog (ฐานของเราเอง) - รอบถัดไป ERP query
                    // จะไม่ดึงรายการนี้กลับมาอีก และตัดออกจากลิสต์ที่แสดงอยู่ทันทีด้านล่าง
                    _printLogService.LogPrinted(item, userId);
                    LogService.WriteLog(userId, "PRINT_PACKING_CARD", $"Printed Packing Card | Ticket: {item.TicketNo} | Lot: {item.LotNo}", item.MaterialCode);
                }

                if (printedItems.Count > 0)
                {
                    _viewModel.RemoveItems(printedItems);
                    UpdateItemCountText();
                    DialogHelper.ShowSuccess($"พิมพ์ Packing Card สำเร็จ {printedItems.Count} ใบ");
                }
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError("พิมพ์ไม่สำเร็จ: " + ex.Message);
            }
            finally
            {
                progressView.Finish();
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
    }
}
