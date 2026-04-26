using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics;
using WinRT.Interop;

namespace Auto_Clicker
{
    // MainWindow.xaml 对应的后台代码（code-behind）。
    // 这里处理窗口初始化、快捷键注册、自动点击逻辑等。
    public sealed partial class MainWindow : Window
    {
        // 常量说明（与 Win32 API 交互时使用）：
        private const int DefaultWindowWidth = 600; // 默认窗口宽度，确保状态/诊断文本可完整展示
        private const int DefaultWindowHeight = 360; // 默认窗口高度，避免新增诊断区域被裁剪
        private const int WhKeyboardLl = 13; // 低级键盘钩子类型
        private const int WmKeyDown = 0x0100; // 键盘按下消息
        private const uint VkF10 = 0x79; // F10 键的虚拟键码
        private const uint WmLButtonDown = 0x0201; // 鼠标左键按下窗口消息
        private const uint WmLButtonUp = 0x0202; // 鼠标左键抬起窗口消息
        private const int MkLButton = 0x0001; // wParam 标记：鼠标左键状态

        // 注意：需要持有委托引用，避免被 GC 回收导致回调失效
        private readonly LowLevelKeyboardProc _hookCallback;
        private IntPtr _hookHandle;
        private bool _hookInstalled;
        private bool _isClicking;
        private CancellationTokenSource? _clickLoopCts;
        private int _hotKeyTriggerCount;
        private int _injectedClickCount;
        private DateTime _lastDiagnosticUpdateUtc = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();
            ConfigureWindow();

            // 持有委托引用，避免被 GC 回收
            _hookCallback = KeyboardHookCallback;
            Closed += MainWindow_Closed;

            // 安装低级键盘钩子以在全局范围内监听 F10
            InstallKeyboardHook();
            UpdateDiagnosticText();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            // 关闭窗口时清理：停止连点、卸载键盘钩子
            StopClicking();
            UninstallKeyboardHook();
        }

        private void ConfigureWindow()
        {
            // 获取窗口句柄并调整窗口大小（使用 WinUI 窗口 API）
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            AppWindow appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd));
            appWindow.Resize(new SizeInt32(DefaultWindowWidth, DefaultWindowHeight));
        }

        private void InstallKeyboardHook()
        {
            if (_hookInstalled)
            {
                return;
            }

            _hookHandle = SetWindowsHookEx(WhKeyboardLl, _hookCallback,
                GetModuleHandle(null), 0);

            if (_hookHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                StatusTextBlock.Text = $"状态：键盘钩子安装失败（错误码 {error}）";
                return;
            }

            _hookInstalled = true;
            StatusTextBlock.Text = "状态：已停止（F10 可用）";
            UpdateDiagnosticText();
        }

        private void UninstallKeyboardHook()
        {
            if (_hookInstalled && _hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
                _hookInstalled = false;
            }
        }

        // 切换开始/停止自动点击
        private void ToggleAutoClick()
        {
            if (_isClicking)
            {
                StopClicking();
            }
            else
            {
                StartClicking();
            }
        }

        private void StartClicking()
        {
            // 读取并验证点击间隔
            if (!TryGetInterval(out int intervalMs))
            {
                StatusTextBlock.Text = "状态：请填写有效的点击间隔（>= 1 毫秒）";
                return;
            }

            // 获取当前鼠标位置，自动点击会在该位置进行
            if (!GetCursorPos(out POINT currentPoint))
            {
                StatusTextBlock.Text = "状态：获取鼠标位置失败";
                return;
            }

            // 取消并释放上一次的 CancellationTokenSource（如果存在）
            _clickLoopCts?.Cancel();
            _clickLoopCts?.Dispose();

            _clickLoopCts = new CancellationTokenSource();
            _isClicking = true;
            _injectedClickCount = 0;
            _lastDiagnosticUpdateUtc = DateTime.MinValue;
            StatusTextBlock.Text = $"状态：连点中（{intervalMs}ms, X={currentPoint.X}, Y={currentPoint.Y}）";
            InjectionDiagnosticTextBlock.Text = "诊断：已启动 PostMessage 注入循环...";

            // 启动后台循环任务来持续点击（不阻塞 UI 线程）
            _ = RunClickLoopAsync(currentPoint, intervalMs, _clickLoopCts.Token);
        }

        private void StopClicking()
        {
            // 停止点击并释放资源
            _isClicking = false;
            _clickLoopCts?.Cancel();
            _clickLoopCts?.Dispose();
            _clickLoopCts = null;

            if (_hookInstalled)
            {
                StatusTextBlock.Text = "状态：已停止";
            }

            if (InjectionDiagnosticTextBlock.Text.StartsWith("诊断：PostMessage 已注入", StringComparison.Ordinal))
            {
                InjectionDiagnosticTextBlock.Text = "诊断：注入循环已停止";
            }
        }

        private async Task RunClickLoopAsync(POINT point, int intervalMs, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // 通过 PostMessage 向鼠标所在窗口直接发送鼠标消息
                    IntPtr targetHwnd = WindowFromPoint(point);
                    if (targetHwnd != IntPtr.Zero)
                    {
                        // 将屏幕坐标转换为目标窗口的客户区坐标
                        POINT clientPoint = point;
                        ScreenToClient(targetHwnd, ref clientPoint);
                        IntPtr clientLparam = MakeLParam(clientPoint.X, clientPoint.Y);

                        PostMessage(targetHwnd, WmLButtonDown, (IntPtr)MkLButton, clientLparam);
                        PostMessage(targetHwnd, WmLButtonUp, IntPtr.Zero, clientLparam);
                    }

                    _injectedClickCount++;
                    DateTime nowUtc = DateTime.UtcNow;
                    if ((nowUtc - _lastDiagnosticUpdateUtc).TotalMilliseconds >= 1000)
                    {
                        InjectionDiagnosticTextBlock.Text =
                            $"诊断：PostMessage 已注入 {_injectedClickCount} 次（最近 {DateTime.Now:HH:mm:ss}）";
                        _lastDiagnosticUpdateUtc = nowUtc;
                    }

                    // 使用可取消的延迟，保证停止时能及时退出循环
                    await Task.Delay(intervalMs, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 任务取消时抛出此异常，可以忽略
            }
        }

        // 从 NumberBox 中读取并验证间隔值
        private bool TryGetInterval(out int intervalMs)
        {
            intervalMs = 0;
            double value = IntervalNumberBox.Value;
            if (double.IsNaN(value) || value < 1 || value > int.MaxValue)
            {
                return false;
            }

            intervalMs = (int)Math.Round(value);
            return true;
        }

        // 低级键盘钩子回调：在全局范围内监听 F10 按下事件
        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam.ToInt32() == WmKeyDown)
            {
                KBDLLHOOKSTRUCT keyInfo = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                if (keyInfo.vkCode == VkF10)
                {
                    _hotKeyTriggerCount++;
                    UpdateDiagnosticText();
                    ToggleAutoClick();
                }
            }

            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        // POINT 结构代表一个屏幕坐标点（X, Y）
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        // 低级键盘钩子结构体（WH_KEYBOARD_LL 回调的 lParam 指向此结构）
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // 低级键盘钩子回调委托
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        // 将两个 16 位坐标值打包为 LPARAM（低位 = X，高位 = Y）
        private static IntPtr MakeLParam(int x, int y)
        {
            return (IntPtr)((y << 16) | (x & 0xFFFF));
        }

        // 使用 P/Invoke 声明与 Win32 API 的互操作函数
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        private void UpdateDiagnosticText()
        {
            string hookState = _hookInstalled ? "已安装" : "未安装";
            HotKeyDiagnosticTextBlock.Text =
                $"诊断：键盘钩子{hookState}，F10 触发 {_hotKeyTriggerCount} 次";

            if (_injectedClickCount == 0 && !_isClicking)
            {
                InjectionDiagnosticTextBlock.Text = "诊断：尚未开始注入点击";
            }
        }
    }
}
