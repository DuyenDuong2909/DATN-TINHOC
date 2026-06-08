using AutoCADToRevitApplication.Views;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoCADToRevitApplication
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData,
                              ref string message,
                              ElementSet elements)
        {
            try
            {
                var window = new MainWindow(commandData.Application);
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                // Lấy lỗi gốc sâu nhất
                Exception inner = ex;
                while (inner.InnerException != null)
                    inner = inner.InnerException;

                TaskDialog.Show("Lỗi Chi Tiết",
                    $"Loại lỗi: {inner.GetType().FullName}\n\n" +
                    $"Thông báo: {inner.Message}\n\n" +
                    $"Stack:\n{inner.StackTrace}");

                message = inner.Message;
                return Result.Failed;
            }
        }
    }
}
 