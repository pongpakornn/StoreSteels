using StoreSteels.Helpers;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace StoreSteels.Views
{
    // ป็อปอัพกรอกจำนวน ใช้สำหรับ "คืนเหล็ก" ในหน้า Multi-Scanner (IN) - สไตล์เดียวกับ CustomDialogView
    public partial class QuantityInputDialog : Window
    {
        private static readonly Regex DigitsOnly = new Regex("^[0-9]+$");

        public int Quantity { get; private set; }

        public QuantityInputDialog(string title, string message)
        {
            InitializeComponent();

            lblTitle.Text = title;
            lblMessage.Text = message;

            this.Loaded += (s, e) =>
            {
                RunOpenAnimation();
                txtQuantity.Focus();
            };
        }

        private void txtQuantity_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !DigitsOnly.IsMatch(e.Text);
        }

        private void txtQuantity_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Confirm_Click(sender, e);
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtQuantity.Text, out int qty) && qty > 0)
            {
                Quantity = qty;
                this.DialogResult = true;
                CloseWindow();
            }
            else
            {
                DialogHelper.ShowWarning("กรุณากรอกจำนวนที่มากกว่า 0 ครับ");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            CloseWindow();
        }

        private void RunOpenAnimation()
        {
            DoubleAnimation fade = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.2));
            DoubleAnimation scale = new DoubleAnimation(0.8, 1, TimeSpan.FromSeconds(0.3))
            {
                EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut }
            };
            MainBorder.BeginAnimation(OpacityProperty, fade);
            WindowScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scale);
            WindowScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scale);
        }

        private void CloseWindow()
        {
            DoubleAnimation fade = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.15));
            fade.Completed += (s, e) => this.Close();
            MainBorder.BeginAnimation(OpacityProperty, fade);
        }
    }
}
