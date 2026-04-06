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
    public sealed partial class MainWindow : Window
    {
        private const int GwlWndProc = -4;
        private const int HotKeyId = 0x1200;
        private const uint WmHotKey = 0x0312;
        private const uint VkF10 = 0x79;
        private const uint MouseeventfLeftdown = 0x0002;
        private const uint MouseeventfLeftup = 0x0004;

        private readonly WndProc _wndProcDelegate;
        private IntPtr _hwnd;
        private IntPtr _originalWndProc;
        private bool _hotKeyRegistered;
        private bool _isClicking;
        private CancellationTokenSource? _clickLoopCts;

        public MainWindow()
        {
            InitializeComponent();
            ConfigureWindow();

            _wndProcDelegate = WindowProc;
            Activated += MainWindow_Activated;
            Closed += MainWindow_Closed;

            TryInitializeHotKeyInterop();
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            TryInitializeHotKeyInterop();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
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
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            AppWindow appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd));
            appWindow.Resize(new SizeInt32(460, 300));
        }

        private void TryInitializeHotKeyInterop()
        {
            if (_hotKeyRegistered)
            {
                return;
            }

            _hwnd = WindowNative.GetWindowHandle(this);
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

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

            if (!RegisterHotKey(_hwnd, HotKeyId, 0, VkF10))
            {
                int registerError = Marshal.GetLastWin32Error();
                StatusTextBlock.Text = $"状态：F10 热键注册失败（错误码 {registerError}）";
                return;
            }

            _hotKeyRegistered = true;
            StatusTextBlock.Text = "状态：已停止（F10 可用）";
        }

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
            if (!TryGetInterval(out int intervalMs))
            {
                StatusTextBlock.Text = "状态：请填写有效的点击间隔（>= 1 毫秒）";
                return;
            }

            if (!GetCursorPos(out POINT currentPoint))
            {
                StatusTextBlock.Text = "状态：获取鼠标位置失败";
                return;
            }

            _clickLoopCts?.Cancel();
            _clickLoopCts?.Dispose();

            _clickLoopCts = new CancellationTokenSource();
            _isClicking = true;
            StatusTextBlock.Text = $"状态：连点中（{intervalMs}ms, X={currentPoint.X}, Y={currentPoint.Y}）";

            _ = RunClickLoopAsync(currentPoint, intervalMs, _clickLoopCts.Token);
        }

        private void StopClicking()
        {
            _isClicking = false;
            _clickLoopCts?.Cancel();
            _clickLoopCts?.Dispose();
            _clickLoopCts = null;

            if (_hotKeyRegistered)
            {
                StatusTextBlock.Text = "状态：已停止";
            }
        }

        private async Task RunClickLoopAsync(POINT point, int intervalMs, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    SetCursorPos(point.X, point.Y);
                    mouse_event(MouseeventfLeftdown, 0, 0, 0, UIntPtr.Zero);
                    mouse_event(MouseeventfLeftup, 0, 0, 0, UIntPtr.Zero);
                    await Task.Delay(intervalMs, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // ignore cancel
            }
        }

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

        private IntPtr WindowProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
        {
            if (message == WmHotKey && wParam.ToInt32() == HotKeyId)
            {
                ToggleAutoClick();
                return IntPtr.Zero;
            }

            if (_originalWndProc != IntPtr.Zero)
            {
                return CallWindowProc(_originalWndProc, hWnd, message, wParam, lParam);
            }

            return DefWindowProc(hWnd, message, wParam, lParam);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private delegate IntPtr WndProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
            {
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            }

            return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }
    }
}
