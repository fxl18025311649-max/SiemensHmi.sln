using System.Configuration;
using System.Data;
using System.Windows;

namespace SiemensHmi
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 捕获 UI 线程上的未处理异常
            DispatcherUnhandledException += (s, args) =>
            {
                System.Windows.MessageBox.Show($"UI 线程异常：\n{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                    "崩溃了", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;   // 不要让程序退出
            };

            // 捕获后台线程上的未处理异常
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                System.Windows.MessageBox.Show($"后台线程异常：\n{ex?.Message}\n\n{ex?.StackTrace}",
                    "崩溃了", MessageBoxButton.OK, MessageBoxImage.Error);
            };
        }
    }

}
