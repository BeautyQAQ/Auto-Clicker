# AGENT.md

## 项目概览

- 项目类型：基于 .NET 8 的 WinUI 3 桌面程序
- 解决方案文件：`Auto-Clicker.slnx`
- 主项目：`Auto-Clicker/Auto-Clicker.csproj`
- 当前默认全局热键：`F10`
- 核心行为：按下 `F10` 后，以当前配置的毫秒间隔，在启动时记录的鼠标位置持续连点；再次按下 `F10` 停止

## 关键文件

- `Auto-Clicker/MainWindow.xaml`：主界面布局
- `Auto-Clicker/MainWindow.xaml.cs`：热键注册、窗口初始化、状态更新、连点循环
- `Auto-Clicker/App.xaml` 与 `Auto-Clicker/App.xaml.cs`：应用入口与启动流程
- `Auto-Clicker/Auto-Clicker.csproj`：目标框架、运行时标识、Windows App SDK 依赖
- `Auto-Clicker/Properties/PublishProfiles/`：`win-x86`、`win-x64`、`win-arm64` 的发布配置

## 常用命令

- 构建：
  - `dotnet build .\Auto-Clicker.slnx -c Release`
- 在 Visual Studio 中运行：
  - 打开 `Auto-Clicker.slnx` 后按 `F5`
- 发布：
  - 使用 `Auto-Clicker/Properties/PublishProfiles/` 下已有的发布配置

## 修改规则

- 代码修改尽量集中在 `Auto-Clicker/` 源码目录
- 不要手动修改 `bin/` 或 `obj/` 下的生成文件
- 保持当前可空引用设置和现有代码风格
- 除非用户明确要求，否则界面文案保持中文
- 如果修改热键，必须同步更新：
  - `MainWindow.xaml.cs` 中注册热键使用的虚拟键常量
  - `MainWindow.xaml` 中的提示文案
  - `MainWindow.xaml.cs` 中和热键相关的状态提示文本

## 架构说明

- 程序通过 `RegisterHotKey` 注册 Windows 全局热键
- 通过 `SetWindowLongPtr` 安装自定义窗口过程，用来接收 `WM_HOTKEY`
- 连点逻辑运行在一个由 `CancellationTokenSource` 控制的异步循环中
- 鼠标输入模拟依赖 `user32.dll` 中的 `SetCursorPos` 和 `mouse_event`
- 鼠标坐标在开始连点时只记录一次，之后持续点击该固定位置

## 安全与行为注意事项

- 这是一个会全局模拟鼠标输入的工具，行为变更会直接影响整个桌面环境
- 除非用户明确要求，不要引入开机自启、后台常驻、隐藏执行等行为
- 如果改动输入模拟或热键逻辑，务必确认“再次按热键停止”仍然可靠

## 验证清单

- `dotnet build .\Auto-Clicker.slnx -c Release` 可以成功通过
- 程序可以正常启动
- 按 `F10` 可以开始连点
- 再按一次 `F10` 可以停止连点
- 输入非法间隔值时会显示清晰的状态提示
- 热键注册失败时，状态栏会正确显示错误信息

## 已知上下文

- 如果 `README.md` 与实际实现不一致，以源码为准
