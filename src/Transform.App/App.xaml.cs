#nullable enable

using System;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Npnp.Core;
using Npnp.Core.Services;
using Transform.App.Services;
using Transform.App.ViewModels;

namespace Transform.App
{
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider;

        public App()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // 使用 Npnp.Core 提供的扩展方法注册所有核心服务
            // 这会自动注册 ILcscApiService, ISchLibWriter, IPcbLibWriter, IExportService 等
            services.AddNpnpCore();
            
            // 注册 Transform.App 特定服务
            services.AddSingleton<ILcscApiServiceProvider, LcscApiServiceProvider>(sp =>
                new LcscApiServiceProvider(sp.GetRequiredService<ILcscApiService>()));
            services.AddSingleton<IClipboardMonitor, ClipboardMonitor>();

            // 注册 MainViewModel，注入所需服务
            services.AddTransient<MainViewModel>(sp =>
                new MainViewModel(sp.GetRequiredService<ILcscApiService>()));
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 先尝试创建 ViewModel，捕获类型初始化异常
                MainViewModel? viewModel = null;
                try
                {
                    viewModel = _serviceProvider.GetRequiredService<MainViewModel>();
                }
                catch (Exception ex)
                {
                    // 类型初始化异常
                    var innerEx = ex.InnerException ?? ex;
                    MessageBox.Show($"ViewModel 创建失败: {ex.Message}\n\n内部错误: {innerEx.Message}\n\n堆栈: {innerEx.StackTrace}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown(1);
                    return;
                }

                var lcscApiService = _serviceProvider.GetRequiredService<ILcscApiService>();
                var mainWindow = new MainWindow(viewModel, lcscApiService);
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                var innerEx = ex.InnerException ?? ex;
                MessageBox.Show($"应用启动失败: {ex.Message}\n\n内部错误: {innerEx.Message}\n\n堆栈: {innerEx.StackTrace}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }
    }
}