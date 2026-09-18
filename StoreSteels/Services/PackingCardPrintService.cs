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
    // ขนาดกระดาษ: เครื่องพิมพ์ Brother QL-800 ม้วนเทปต่อเนื่องกว้าง 62mm (ค่าตายตัวของม้วน) x ความยาว 75.4mm
    // (ค่าที่ทดสอบพิมพ์ได้จริงจาก Printing Preferences ของไดรเวอร์ - ใส่ Width/Height สลับกับที่เคยลอง
    //  70x62 ตรงๆ แล้วขึ้น error "ม้วนสติกเกอร์ไม่ตรงกับที่เลือกใช้ในแอปพลิเคชัน" เพราะ PageMediaSize ต้อง
    //  เป็นขนาด "ฐาน" ของม้วน (กว้าง 62 x ยาว 75.4) แล้วค่อยสั่ง PageOrientation.Landscape ให้มันหมุนตอนพิมพ์)
    public class PackingCardPrintService
    {
        private const double MmToPx = 96.0 / 25.4; // WPF/PrintTicket ใช้หน่วย 1/96 นิ้ว
        private const double RollWidthMm = 62;    // ความกว้างม้วนเทป (ค่าตายตัวของ QL-800)
        private const double LabelLengthMm = 75.4; // ความยาวป้ายต่อดวง (ปรับได้ตามที่ทดสอบพิมพ์ได้จริง)

        // ขนาด Visual ที่จะวาดจริง = ขนาดหลังหมุนเป็นแนวนอนแล้ว (ยาว x กว้าง)
        private const double CardWidth = LabelLengthMm * MmToPx;
        private const double CardHeight = RollWidthMm * MmToPx;

        // คืนรายการที่พิมพ์สำเร็จจริง (เรียงตามลำดับที่พิมพ์) - onProgress แจ้งความคืบหน้าจริงทีละใบ
        public List<PackingCardModel> PrintCards(IList<PackingCardModel> items, Action<int, int> onProgress = null)
        {
            var printed = new List<PackingCardModel>();
            if (items == null || items.Count == 0) return printed;

            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() != true) return printed;

            try
            {
                // PageMediaSize ต้องใส่เป็นขนาด "ฐาน" ของม้วน (กว้าง=62mm คงที่, ยาว=75.4mm) ไม่ใช่ขนาด
                // หลังหมุนแล้ว - แล้วให้ PageOrientation เป็นตัวหมุนแสดงผลเป็นแนวนอนแทน
                printDialog.PrintTicket.PageMediaSize = new PageMediaSize(RollWidthMm * MmToPx, LabelLengthMm * MmToPx);
                printDialog.PrintTicket.PageOrientation = PageOrientation.Landscape;
            }
            catch
            {
                // บาง driver ของเครื่องพิมพ์อาจไม่รองรับการกำหนดขนาดกระดาษเอง - ปล่อยให้ใช้ค่า default ของเครื่องแทน
            }

            for (int i = 0; i < items.Count; i++)
            {
                var visual = BuildCardVisual(items[i]);
                visual.Measure(new Size(CardWidth, CardHeight));
                visual.Arrange(new Rect(new Size(CardWidth, CardHeight)));

                printDialog.PrintVisual(visual, $"Packing Card - {items[i].TicketNo}");
                printed.Add(items[i]);

                onProgress?.Invoke(printed.Count, items.Count);
            }

            return printed;
        }

        // Layout ตามการ์ดตัวอย่าง: หัวการ์ด (โลโก้ + ชื่อบริษัท) + QR มุมขวาบน, แล้วตามด้วย
        // Bill/Group, Work Order/Lot No., Part Name เต็มแถว, Quantity/TicketDate
        public FrameworkElement BuildCardVisual(PackingCardModel item)
        {
            var border = new Border
            {
                Width = CardWidth,
                Height = CardHeight,
                Background = Brushes.White,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1.2),
                Padding = new Thickness(10)
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // divider
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // bill/group
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // workorder/lot
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // part name
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // qty/date

            root.Children.Add(BuildHeader(item));
            Grid.SetRow(root.Children[0], 0);

            var divider = new Border { Height = 1, Background = Brushes.LightGray, Margin = new Thickness(0, 6, 0, 6) };
            Grid.SetRow(divider, 1);
            root.Children.Add(divider);

            var billGroup = BuildFieldRow("Bill", item.TicketNo, "Group", item.GroupCode);
            Grid.SetRow(billGroup, 2);
            root.Children.Add(billGroup);

            var workLot = BuildFieldRow("Work Order", item.WorkOrder, "LOT NO.", item.LotNo);
            Grid.SetRow(workLot, 3);
            root.Children.Add(workLot);

            var partNameRow = BuildSingleFieldRow("Part Name", item.JobName);
            Grid.SetRow(partNameRow, 4);
            root.Children.Add(partNameRow);

            var qtyDate = BuildFieldRow("Quantity", $"{item.Qty:0.##}", "TicketDate", item.TicketDate == DateTime.MinValue ? "" : item.TicketDate.ToString("dd-MM-yyyy"));
            Grid.SetRow(qtyDate, 5);
            root.Children.Add(qtyDate);

            border.Child = root;
            return border;
        }

        private static UIElement BuildHeader(PackingCardModel item)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // "CH" เป็นตัวอักษรตัวใหญ่หนาเฉยๆ (ไม่มีกล่องพื้นหลัง) ตามแบบการ์ดตัวอย่างจริง
            var companyPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var logoText = new TextBlock
            {
                Text = "CH",
                Foreground = Brushes.Black,
                FontWeight = FontWeights.Black,
                FontSize = 22,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            companyPanel.Children.Add(logoText);

            var namePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            namePanel.Children.Add(new TextBlock { Text = "CH. RADIATORS CO.LTD.", FontWeight = FontWeights.Black, FontSize = 12, Foreground = Brushes.Black });
            namePanel.Children.Add(new TextBlock { Text = "บริษัท ซีเอชเรดิเอเตอร์ จำกัด", FontSize = 9, Foreground = Brushes.Black });
            companyPanel.Children.Add(namePanel);

            Grid.SetColumn(companyPanel, 0);
            grid.Children.Add(companyPanel);

            var qrBorder = new Border
            {
                Width = 42,
                Height = 42,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1)
            };
            qrBorder.Child = new Image { Source = item.QrImage, Stretch = Stretch.Uniform, Margin = new Thickness(2) };
            Grid.SetColumn(qrBorder, 1);
            grid.Children.Add(qrBorder);

            return grid;
        }

        // แถวคู่ label/value สองชุดในแถวเดียวกัน (เช่น Bill | Group)
        private static UIElement BuildFieldRow(string label1, string value1, string label2, string value2)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var l1 = MakeLabel(label1);
            Grid.SetColumn(l1, 0);
            var v1 = MakeValue(value1, new Thickness(4, 0, 10, 0));
            Grid.SetColumn(v1, 1);
            var l2 = MakeLabel(label2);
            Grid.SetColumn(l2, 2);
            var v2 = MakeValue(value2, new Thickness(4, 0, 0, 0));
            Grid.SetColumn(v2, 3);

            grid.Children.Add(l1);
            grid.Children.Add(v1);
            grid.Children.Add(l2);
            grid.Children.Add(v2);
            return grid;
        }

        // แถว label/value เดี่ยว เต็มแถว (Part Name)
        private static UIElement BuildSingleFieldRow(string label, string value)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var l = MakeLabel(label);
            Grid.SetColumn(l, 0);
            var v = MakeValue(value, new Thickness(4, 0, 0, 0));
            v.TextWrapping = TextWrapping.Wrap;
            Grid.SetColumn(v, 1);

            grid.Children.Add(l);
            grid.Children.Add(v);
            return grid;
        }

        private static TextBlock MakeLabel(string text) => new TextBlock
        {
            Text = text,
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        private static TextBlock MakeValue(string text, Thickness margin) => new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Margin = margin,
            VerticalAlignment = VerticalAlignment.Bottom
        };
    }
}
