using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// 应用程序入口点（App.xaml 对应的后台代码）
// 这里负责初始化应用，并在启动时创建主窗口。
namespace Auto_Clicker
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    ///
    /// 注释（面向初学者）：
    /// - App 类是整个 WinUI 应用的入口，等同于传统 Win32 的 WinMain 或控制台程序的 Main 方法。
    /// - 在 OnLaunched 中创建并激活主窗口（MainWindow），这样应用才会显示 UI。
    /// </summary>
    public partial class App : Application
    {
        // 保存主窗口的引用（可选），便于在应用的其他地方访问
        private Window? _window;

        /// <summary>
        /// 构造函数：应用程序初始化时被调用。
        /// InitializeComponent 会加载 XAML 定义的资源（例如 App.xaml 中的资源）。
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 当应用被启动时调用此方法。
        /// 这里创建主窗口并激活它，使其可见并接收输入。
        /// </summary>
        /// <param name="args">有关启动的信息（比如是否从二进制包激活等）。</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
