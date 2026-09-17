using StoreSteels.Views;
using System.Windows;

namespace StoreSteels.Helpers
{
    public static class DialogHelper
    {
        // 1. สำหรับงานสำเร็จ (สีเขียว - Auto Close)
        public static void ShowSuccess(string message, string title = "SUCCESS")
        {
            var win = new CustomDialogView(title, message, CustomDialogView.DialogType.Success);
            SetOwner(win);
            win.ShowDialog();
        }

        // 2. สำหรับการยืนยัน Yes/No (สีฟ้า)
        public static bool ShowConfirm(string message, string title = "CONFIRMATION")
        {
            var win = new CustomDialogView(title, message, CustomDialogView.DialogType.Confirm);
            SetOwner(win);
            return win.ShowDialog() == true;
        }

        // 3. สำหรับคำเตือน (สีส้มแดง - หน้าใหม่ที่เราเพิ่งทำ)
        public static void ShowWarning(string message, string title = "WARNING")
        {
            var win = new CustomWarningView(title, message);
            SetOwner(win);
            win.ShowDialog();
        }

        // 4. สำหรับข้อผิดพลาดร้ายแรง (สีแดงเข้ม - หน้าใหม่)
        public static void ShowError(string message, string title = "ERROR")
        {
            var win = new CustomErrorView(title, message);
            SetOwner(win);
            win.ShowDialog();
        }

        public static void ShowInfo(string message, string title = "PR DETAILS")
        {
            var win = new CustomDialogView(title, message, CustomDialogView.DialogType.Info);
            SetOwner(win);
            win.ShowDialog();
        }

        // Helper สำหรับจัดหน้าต่างให้อยู่กึ่งกลางโปรแกรมหลัก
        private static void SetOwner(Window win)
        {
            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                win.Owner = Application.Current.MainWindow;
            }
        }

    }
}