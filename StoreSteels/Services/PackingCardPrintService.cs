using StoreSteels.Models;
using System;
using System.Collections.Generic;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StoreSteels.Services
{
    // สร้าง Visual การ์ด Packing แล้วสั่งพิมพ์ทีละใบผ่าน PrintDialog/PrintVisual ของ WPF เอง
    // (โปรเจกต์นี้ยังไม่มี Print service กลางมาก่อน จึงสร้างใหม่โดยใช้กลไกมาตรฐานของ WPF)
    // ขนาดกระดาษ: เครื่องพิมพ์ Brother QL-800 ป้ายฉลากม้วนต่อเนื่อง สูง 62mm x กว้าง(แนวนอน) 70mm
    public class PackingCardPrintService
    {
        private const double MmToPx = 96.0 / 25.4; // WPF/PrintTicket ใช้หน่วย 1/96 นิ้ว
        private const double LabelWidthMm = 70;
        private const double LabelHeightMm = 62;
        private const double CardWidth = LabelWidthMm * MmToPx;
        private const double CardHeight = LabelHeightMm * MmToPx;

        // คืนค่าจำนวนใบที่พิมพ์สำเร็จ, onProgress แจ้งความคืบหน้าจริงทีละใบ (current, total)
        public int PrintCards(IList<PackingCardModel> items, Action<int, int> onProgress = null)
        {
            if (items == null || items.Count == 0) return 0;

            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() != true) return 0;

            try
            {
                printDialog.PrintTicket.PageMediaSize = new PageMediaSize(CardWidth, CardHeight);
                printDialog.PrintTicket.PageOrientation = PageOrientation.Landscape;
            }
            catch
            {
                // บาง driver ของเครื่องพิมพ์อาจไม่รองรับการกำหนดขนาดกระดาษเอง - ปล่อยให้ใช้ค่า default ของเครื่องแทน
            }

            int printed = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var visual = BuildCardVisual(items[i]);
                visual.Measure(new Size(CardWidth, CardHeight));
                visual.Arrange(new Rect(new Size(CardWidth, CardHeight)));

                printDialog.PrintVisual(visual, $"Packing Card - {items[i].TicketNo}");
                printed++;

                onProgress?.Invoke(printed, items.Count);
            }

            return printed;
        }

        public FrameworkElement BuildCardVisual(PackingCardModel item)
        {
            var border = new Border
            {
                Width = CardWidth,
                Height = CardHeight,
                Background = Brushes.White,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(14)
            };

            var grid = new Grid();
            for (int i = 0; i < 5; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // แถว 1: Bill No เต็มแถว ตัวใหญ่
            var billNo = new TextBlock
            {
                Text = item.TicketNo,
                FontSize = 22,
                FontWeight = FontWeights.Black,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(billNo, 0);
            Grid.SetColumn(billNo, 0);
            grid.Children.Add(billNo);

            // มุมขวาบน: QR Code (ครอบคลุมแถว 1-3)
            var qrImage = new Image
            {
                Width = 90,
                Height = 90,
                Source = item.QrImage,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetRow(qrImage, 0);
            Grid.SetRowSpan(qrImage, 3);
            Grid.SetColumn(qrImage, 1);
            grid.Children.Add(qrImage);

            // แถว 2: Group | Work Order
            grid.Children.Add(MakeRow($"Group: {item.GroupCode}", $"Work Order: {item.WorkOrder}", 1));

            // แถว 3: Lot No | Material Code
            grid.Children.Add(MakeRow($"Lot No: {item.LotNo}", $"Material Code: {item.MaterialCode}", 2));

            // แถว 4: Part Name เต็มแถว
            var partName = new TextBlock
            {
                Text = item.JobName,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 6)
            };
            Grid.SetRow(partName, 3);
            Grid.SetColumn(partName, 0);
            Grid.SetColumnSpan(partName, 2);
            grid.Children.Add(partName);

            // แถว 5: Quantity (ไม่ต้องพิมพ์ TicketDate ลงบน Packing Card ตามที่ร้องขอ)
            var qtyText = new TextBlock
            {
                Text = $"Quantity: {item.Qty:0.##}",
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            Grid.SetRow(qtyText, 4);
            Grid.SetColumn(qtyText, 0);
            Grid.SetColumnSpan(qtyText, 2);
            grid.Children.Add(qtyText);

            border.Child = grid;
            return border;
        }

        private static UIElement MakeRow(string left, string right, int row)
        {
            var panel = new Grid();
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftText = new TextBlock { Text = left, FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
            var rightText = new TextBlock { Text = right, FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
            Grid.SetColumn(rightText, 1);

            panel.Children.Add(leftText);
            panel.Children.Add(rightText);

            Grid.SetRow(panel, row);
            Grid.SetColumn(panel, 0);
            return panel;
        }
    }
}
