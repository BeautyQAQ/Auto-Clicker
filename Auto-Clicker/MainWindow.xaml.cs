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
        private const int GwlWndProc = -4; // 用于替换窗口过程的索引
        private const int HotKeyId = 0x1200; // 注册热键时的 ID（随便选一个唯一值）
        private const uint WmHotKey = 0x0312; // Windows 消息：热键触发
        private const uint VkF10 = 0x79; // F10 键的虚拟键码
        private const uint InputMouse = 0; // SendInput 的鼠标输入类型
        private const uint MouseeventfLeftdown = 0x0002; // 鼠标左键按下
        private const uint MouseeventfLeftup = 0x0004; // 鼠标左键弹起
        private const uint WmLButtonDown = 0x0201; // 鼠标左键按下窗口消息
        private const uint WmLButtonUp = 0x0202; // 鼠标左键抬起窗口消息
        private const int MkLButton = 0x0001; // wParam 标记：鼠标左键状态

        // 用于保存原始窗口过程和委托，处理全局热键消息
        // 注意：需要持有委托引用，避免被 GC 回收导致回调失效
        private readonly WndProc _wndProcDelegate;
        private IntPtr _hwnd;
        private IntPtr _originalWndProc;
        private bool _hotKeyRegistered;
        private bool _isClicking;
        private CancellationTokenSource? _clickLoopCts;
        private int _hotKeyTriggerCount;
        private int _injectedClickCount;
        private DateTime _lastDiagnosticUpdateUtc = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();
            ConfigureWindow();

            // 将托管方法转换为委托以用于窗口过程替换
            _wndProcDelegate = WindowProc;
            // 订阅窗口激活和关闭事件
            Activated += MainWindow_Activated;
            Closed += MainWindow_Closed;

            // 尝试初始化热键互操作（与 Win32 交互注册热键）
            TryInitializeHotKeyInterop();
            UpdateDiagnosticText();
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            // 当窗口被激活（例如用户切换回应用）时，确保热键已注册
            TryInitializeHotKeyInterop();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            // 关闭窗口时清理：停止连点、注销热键、恢复原始窗口过程
            StopClicking();

            if (_hotKeyRegistered && _hwnd != IntPtr.Zero)
            {
                UnregisterHotKey(_hwnd, HotKeyId);
                _hotKeyRegistered = false;
            }

            if (_originalWndProc != IntPtr.Zero && _hwnd != IntPtr.Zero)
            {
                SetWindowLongPtr(_hwnd, GwlWndProc, _originalWndProc);
                _originalWndProc = IntPtr.Zero;
            }
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

        private void TryInitializeHotKeyInterop()
        {
            // 如果已经注册，就不重复注册
            if (_hotKeyRegistered)
            {
                return;
            }

            _hwnd = WindowNative.GetWindowHandle(this);
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            // 替换窗口过程以便接收 WM_HOTKEY 消息
            if (_originalWndProc == IntPtr.Zero)
            {
                IntPtr newWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
                IntPtr previousWndProc = SetWindowLongPtr(_hwnd, GwlWndProc, newWndProc);
                int setWndProcError = Marshal.GetLastWin32Error();
                if (previousWndProc == IntPtr.Zero && setWndProcError != 0)
                {
                    StatusTextBlock.Text = $"状态：窗口消息钩子初始化失败（错误码 {setWndProcError}）";
                    return;
                }

                _originalWndProc = previousWndProc;
            }

            // 注册全局热键（F10），修饰键参数为 0 表示不需要 Ctrl/Alt/Shift/Win
            if (!RegisterHotKey(_hwnd, HotKeyId, 0, VkF10))
            {
                int registerError = Marshal.GetLastWin32Error();
                StatusTextBlock.Text = $"状态：F10 热键注册失败（错误码 {registerError}）";
                return;
            }

            _hotKeyRegistered = true;
            StatusTextBlock.Text = "状态：已停止（F10 可用）";
            UpdateDiagnosticText();
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
            InjectionDiagnosticTextBlock.Text = "诊断：已启动注入循环，等待 SendInput 结果...";

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

            if (_hotKeyRegistered)
            {
                StatusTextBlock.Text = "状态：已停止";
            }

            if (InjectionDiagnosticTextBlock.Text.StartsWith("诊断：SendInput 成功", StringComparison.Ordinal))
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
                    // 首先尝试通过 SendInput 注入（适用于普通桌面应用）
                    if (!SetCursorPos(point.X, point.Y))
                    {
                        int cursorError = Marshal.GetLastWin32Error();
                        InjectionDiagnosticTextBlock.Text = $"诊断：SetCursorPos 失败（错误码 {cursorError}）";
                    }

                    uint sentCount = SendLeftClickInput();

                    // 同时向鼠标所在位置的窗口直接发送鼠标消息（适用于游戏等使用
                    // DirectInput / Raw Input 的程序，它们可能忽略 SendInput 注入）
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

                    if (sentCount != 2)
                    {
                        int sendError = Marshal.GetLastWin32Error();
                        InjectionDiagnosticTextBlock.Text = $"诊断：SendInput 返回 {sentCount}/2（错误码 {sendError}）";
                    }
                    else
                    {
                        _injectedClickCount++;
                        DateTime nowUtc = DateTime.UtcNow;
                        if ((nowUtc - _lastDiagnosticUpdateUtc).TotalMilliseconds >= 1000)
                        {
                            InjectionDiagnosticTextBlock.Text =
                                $"诊断：SendInput 成功 {_injectedClickCount} 次（最近 {DateTime.Now:HH:mm:ss}）";
                            _lastDiagnosticUpdateUtc = nowUtc;
                        }
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

        // 自定义窗口过程，用来接收 WM_HOTKEY 消息
        private IntPtr WindowProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
        {
            if (message == WmHotKey && wParam.ToInt32() == HotKeyId)
            {
                // 当 F10 被按下时切换自动点击状态
                _hotKeyTriggerCount++;
                UpdateDiagnosticText();
                ToggleAutoClick();
                return IntPtr.Zero;
            }

            // 将其他消息转发给原始窗口过程
            if (_originalWndProc != IntPtr.Zero)
            {
                return CallWindowProc(_originalWndProc, hWnd, message, wParam, lParam);
            }

            return DefWindowProc(hWnd, message, wParam, lParam);
        }

        // POINT 结构代表一个屏幕坐标点（X, Y）
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        // MOUSEINPUT / INPUT 用于 SendInput 注入鼠标事件
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        // WndProc 委托类型，用于窗口过程替换
        private delegate IntPtr WndProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

        // 将两个 16 位坐标值打包为 LPARAM（低位 = X，高位 = Y）
        private static IntPtr MakeLParam(int x, int y)
        {
            return (IntPtr)((y << 16) | (x & 0xFFFF));
        }

        // 使用 P/Invoke 声明与 Win32 API 的互操作函数
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        // SetWindowLongPtr 是一个跨平台（x86/x64）封装，用于设置窗口过程指针
        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
            {
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            }

            return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        private static uint SendLeftClickInput()
        {
            INPUT[] inputs =
            {
                new INPUT
                {
                    type = InputMouse,
                    U = new INPUTUNION
                    {
                        mi = new MOUSEINPUT
                        {
                            dwFlags = MouseeventfLeftdown
                        }
                    }
                },
                new INPUT
                {
                    type = InputMouse,
                    U = new INPUTUNION
                    {
                        mi = new MOUSEINPUT
                        {
                            dwFlags = MouseeventfLeftup
                        }
                    }
                }
            };

            return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }

        private void UpdateDiagnosticText()
        {
            string hotKeyState = _hotKeyRegistered ? "已注册" : "未注册";
            HotKeyDiagnosticTextBlock.Text =
                $"诊断：热键{hotKeyState}，F10 触发 {_hotKeyTriggerCount} 次";

            if (_injectedClickCount == 0 && !_isClicking)
            {
                InjectionDiagnosticTextBlock.Text = "诊断：尚未开始注入点击";
            }
        }
    }
}
