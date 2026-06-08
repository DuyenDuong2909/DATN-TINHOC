using Autodesk.Revit.UI;
using AutoCADToRevitApplication.ViewModels;
using System.Windows;

namespace AutoCADToRevitApplication.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow(UIApplication uiApp)
        {
            InitializeComponent();

            var vm = new MainViewModel();
            vm.RequestClose += Close;
            DataContext = vm;
            vm.Initialize(uiApp);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
