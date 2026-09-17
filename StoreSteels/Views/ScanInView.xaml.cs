using StoreSteels.Helpers;
using StoreSteels.Models;
using StoreSteels.ViewModels;
using System;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace StoreSteels.Views
{
    public partial class ScanInView : Page
    {
        private ScanInViewModel _viewModel;
        private DispatcherTimer _fastTimer; // ช่วยตัดรอบความเร็วสูง กรณีบาร์โค้ดไหลมาไม่ครบ
        private DateTime _lastScanTime = DateTime.MinValue; // ป้องกันการยิงเบิ้ลซ้ำซ้อน

        // ⚙️ ระบบคิวประสิทธิภาพสูง รองรับการยิงรัวสับๆ แบบไม่หน่วง UI
        private readonly ConcurrentQueue<string> _scanQueue = new ConcurrentQueue<string>();
        private bool _isProcessingQueue = false;
        private readonly object _queueLock = new object();

        public ScanInView(UserSession user)
        {
            InitializeComponent();
            RunEntryAnimation();

            _viewModel = new ScanInViewModel { CurrentUser = user };
            this.DataContext = _viewModel;

            txtBarcodeInput.Focus();
            this.Loaded += (s, e) => txtBarcodeInput.Focus();

            // ตั้งค่าตัวจับเวลาระดับด่วนพิเศษ (15 มิลลิวินาที) เผื่อกรณีไม่มีคำว่า Piece ส่งมา หรือหลุดจังหวะ
            _fastTimer = new DispatcherTimer(DispatcherPriority.Send);
            _fastTimer.Interval = TimeSpan.FromMilliseconds(300);
            _fastTimer.Tick += FastTimer_Tick;
        }

        #region === [ Logic ดักจับ Text เปลี่ยนแบบออโต้ (ถอดแบบจากเวอร์ชันเก่า) ] ===
        private void txtBarcodeInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            string rawData = txtBarcodeInput.Text; // ดึงค่าแบบไม่พึ่ง Trim เพื่อรักษาสปีด

            // ✨ ถอด Logic ทองคำจาก WinForms: ถ้าเจอนามสกุลท้ายบาร์โค้ดคำว่า "Piece" ให้ตัดรอบทันทีไม่ต้องรอใคร!
            if (!string.IsNullOrEmpty(rawData) && rawData.Contains("Piece"))
            {
                _fastTimer.Stop(); // สั่งเบรกเวลาทันที
                CommitBarcodeAction(rawData);
                return;
            }

            // ถ้ายังไม่เจอคำว่า Piece ให้จับเวลารอเผื่อสแกนเนอร์กำลังส่งตัวอักษรถัดไปมา (Reset Timer)
            _fastTimer.Stop();
            _fastTimer.Start();
        }

        private void FastTimer_Tick(object sender, EventArgs e)
        {
            _fastTimer.Stop();
            string input = txtBarcodeInput.Text;
            if (!string.IsNullOrWhiteSpace(input))
            {
                CommitBarcodeAction(input);
            }
        }
        #endregion

        #region === [ ฟังก์ชันสับคัตเอาท์ข้อความ เคลียร์กล่องข้อความทันทีใน 1ms ] ===
        private void CommitBarcodeAction(string rawData)
        {
            // 1. ล้างช่องรับข้อมูลทันที เพื่อให้หน้าจอว่าง สแกนเนอร์ยิงนัดถัดไปได้เลย ไม่ต้องรอโหลดภาพ/ต่อ DB
            txtBarcodeInput.Text = string.Empty;

            // 2. ดักดักป้องกันการยิงซ้ำซ้อน (Double Scan) ภายใน 1 วินาที
            if ((DateTime.Now - _lastScanTime).TotalMilliseconds < 0)
            {
                return;
            }
            _lastScanTime = DateTime.Now; // บันทึกเวลาสแกนล่าสุด

            // 3. โยนข้อมูลเข้าคิวรอประมวลผลหลังบ้านแบบเงียบๆ
            _scanQueue.Enqueue(rawData);

            // 4. สั่งให้ระบบ Worker เริ่มทำงาน (ทำงานแบบ Async ไม่กวนหน้าจอ)
            _ = ProcessQueueAsync();
        }
        #endregion

        #region === [ รองรับกรณีคุณนนท์ต่อคีย์บอร์ดพิมพ์แล้วกด Enter เองด้วย ] ===
        private void txtBarcodeInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true; // บล็อกระบบไม่ให้ส่งเสียงตึ๊ดหรือกระตุก
                _fastTimer.Stop();

                string input = txtBarcodeInput.Text;
                if (!string.IsNullOrWhiteSpace(input))
                {
                    CommitBarcodeAction(input);
                }
            }
        }
        #endregion

        #region === [ ระบบคิวประมวลผลเบื้องหลังสปีดเทอร์โบ ] ===
        private async Task ProcessQueueAsync()
        {
            lock (_queueLock)
            {
                if (_isProcessingQueue) return;
                _isProcessingQueue = true;
            }

            try
            {
                // ไล่เคลียร์บาร์โค้ดที่ค้างอยู่ในคิวแบบเรียงลำดับ ป้องกันฐานข้อมูล Lock ตัวเอง
                while (_scanQueue.TryDequeue(out string barcode))
                {
                    if (_viewModel != null)
                    {
                        try
                        {
                            string cleanInput = barcode.Trim();

                            // 🚀 จุดนี้จะไปรันโค้ดต่อ DB ดึงรูปภาพ และ Log ลงฐานข้อมูลใน ViewModel แบบ Async 
                            await _viewModel.ProcessScan(cleanInput);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Error ในคิว]: {ex.Message}");
                        }
                    }
                }
            }
            finally
            {
                lock (_queueLock)
                {
                    _isProcessingQueue = false;
                }

                // คืนโฟกัสกลับมาที่ช่องยิงบาร์โค้ดแบบนุ่มนวล
                Dispatcher.Invoke(() =>
                {
                    if (!txtBarcodeInput.IsFocused)
                    {
                        txtBarcodeInput.Focus();
                    }
                });
            }
        }
        #endregion

        #region === [ ClearDaily & Animation ] ===
        private async void ClearDaily_Click(object sender, RoutedEventArgs e)
        {
            if (await Task.Run(() => DialogHelper.ShowConfirm("ต้องการล้างรายการวันนี้ทั้งหมดใช่หรือไม่?", "ยืนยันการล้างข้อมูล")))
            {
                _viewModel.ScannedItems?.Clear();
                DialogHelper.ShowSuccess("ล้างรายการเรียบร้อยแล้ว");
            }
        }

        private void RunEntryAnimation()
        {
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromSeconds(0.4) };
            this.BeginAnimation(Page.OpacityProperty, fadeIn);

            if (PageTransform != null)
            {
                var slideUp = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 20,
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.4),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };
                PageTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideUp);
            }
        }
        #endregion
    }
}