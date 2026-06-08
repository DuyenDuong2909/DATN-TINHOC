using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace AutoCADToRevitApplication
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            string tabName = "Dựng mô hình Revit";
            application.CreateRibbonTab(tabName);

            RibbonPanel panel = application.CreateRibbonPanel(tabName, "Đồ án tốt nghiệp");

            string assemblyPath = typeof(App).Assembly.Location;

            PushButtonData buttonData = new PushButtonData(
                name: "OpenConverter",
                text: "CAD 2D\nRevit 3D",
                assemblyName: assemblyPath,
                className: "AutoCADToRevitApplication.Command"
            );

            PushButton button = panel.AddItem(buttonData) as PushButton;
            string iconPath = Path.Combine(
                    Path.GetDirectoryName(assemblyPath),
                    "Images", "cad2revit.png");

            if (File.Exists(iconPath))
            {
                button.LargeImage = new BitmapImage(
                    new Uri(iconPath, UriKind.Absolute));
            }
            button.ToolTip = "Mở công cụ chuyển đổi file AutoCAD sang Revit 3D";

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
