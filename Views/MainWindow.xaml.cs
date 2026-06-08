using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AutoCADToRevitApplication.Views
{
    public partial class MainWindow : Window
    {
        private readonly UIApplication _uiApp;

        public MainWindow(UIApplication uiApp)
        {
            InitializeComponent();
            _uiApp = uiApp;
        }

        private void BtnReadCad_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Chọn file CAD",
                Filter = "AutoCAD Files (*.dwg)|*.dwg"
            };

            if (dialog.ShowDialog() == true)
            {
                SetStatus($"Đã đọc CAD thành công: {System.IO.Path.GetFileName(dialog.FileName)}", true);
                BtnConvert.IsEnabled = true;
            }
        }

        private void BtnConvert_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs()) return;
            SetStatus("Đang chuyển đổi sang 3D...", false, isPending: true);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void SetStatus(string message, bool isSuccess, bool isPending = false)
        {
            StatusLabel.Content = message;
            StatusLabel.Foreground = isPending
                ? new SolidColorBrush(Color.FromRgb(0xF5, 0x7F, 0x17))
                : isSuccess
                    ? new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32))
                    : new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
        }

        private bool ValidateInputs()
        {
            if (!int.TryParse(TxtNumberOfFloors.Text, out int f) || f <= 0)
            { SetStatus("Lỗi: Số tầng không hợp lệ.", false); return false; }
            if (!double.TryParse(TxtFloorHeight.Text, out double h) || h <= 0)
            { SetStatus("Lỗi: Chiều cao tầng 1 không hợp lệ.", false); return false; }
            if (!double.TryParse(TxtSlabThickness.Text, out double s) || s <= 0)
            { SetStatus("Lỗi: Độ dày sàn không hợp lệ.", false); return false; }
            if (!double.TryParse(TxtBeamWidth.Text, out double bw) || bw <= 0 ||
                !double.TryParse(TxtBeamHeight.Text, out double bh) || bh <= 0)
            { SetStatus("Lỗi: Kích thước dầm không hợp lệ.", false); return false; }
            return true;
        }
    }
}
