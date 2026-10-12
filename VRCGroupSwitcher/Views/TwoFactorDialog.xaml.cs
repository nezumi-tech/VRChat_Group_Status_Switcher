using System.Windows;
using System.Windows.Input;

namespace VRCGroupSwitcher.Views
{
    public partial class TwoFactorDialog : Window
    {
        public string Code { get; private set; } = string.Empty;

        public TwoFactorDialog(string prompt = "認証コードを入力してください。")
        {
            InitializeComponent();
            TxtPrompt.Text = prompt;
            TxtCode.Focus();
        }

        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            Submit();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TxtCode_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Submit();
            }
        }

        private void Submit()
        {
            string code = TxtCode.Text.Trim();
            if (string.IsNullOrEmpty(code))
            {
                TxtError.Text = "コードを入力してください。";
                TxtError.Visibility = Visibility.Visible;
                return;
            }

            Code = code;
            DialogResult = true;
            Close();
        }
    }
}
