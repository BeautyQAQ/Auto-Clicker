# Auto-Clicker (WinUI 3)

## 项目简介
这是一个基于 WinUI 3 的 Windows 桌面自动连点器。按下 `F10` 即可开始或停止点击；开始时会记录当前鼠标位置，并在该位置以固定间隔持续点击。

## 功能
- 全局热键 `F10` 开始/停止连点
- 可配置点击间隔（毫秒）
- 启动时锁定当前鼠标坐标，持续点击该位置
- 状态栏提示当前运行状态与坐标

## 运行环境
- Windows 10 1809+（TargetPlatformMinVersion: 10.0.17763.0）
- .NET 10 SDK（目标框架：`net10.0-windows10.0.19041.0`）/ Visual Studio 2026
- Windows App SDK 1.8（NuGet 引用）

## 快速开始
1. 用 Visual Studio 2022 打开 `Auto-Clicker.slnx` 或 `Auto-Clicker/Auto-Clicker.csproj`。
2. 还原 NuGet 依赖。
3. `F5` 运行。
4. 在界面里设置点击间隔，按 `F10` 开始或停止。

注意：如果 `F10` 被其他程序占用，程序会在状态栏提示热键注册失败。

## WinUI 3 工程结构说明（面向 Java/IntelliJ 用户）
- 解决方案 `Auto-Clicker.slnx`
  类似 IntelliJ 的“项目/工作区”，用于聚合多个 C# 项目。一个解决方案里可以包含多个 `.csproj`。
- 项目文件 `Auto-Clicker/Auto-Clicker.csproj`
  类似 `pom.xml` 或 `build.gradle`，定义目标框架、依赖包（NuGet）、编译/发布配置等。
- `App.xaml` + `App.xaml.cs`
  相当于应用入口和全局资源配置。`App.xaml` 负责资源字典，`App.xaml.cs` 的 `OnLaunched` 类似 `main()` 或 `SpringApplication.run()`。
- `MainWindow.xaml` + `MainWindow.xaml.cs`
  `MainWindow.xaml` 是 UI 声明式布局（类似 JavaFX 的 FXML 或 Android XML），`MainWindow.xaml.cs` 是 code-behind，处理事件与业务逻辑。
- `Package.appxmanifest`
  应用清单，包含应用标识、图标、能力声明和打包信息（MSIX）。
- `app.manifest`
  Win32 应用清单（DPI、权限等）。
- `Assets/`
  应用图标、启动画面等资源。
- `Properties/launchSettings.json`
  运行配置文件，类似 IntelliJ 的 Run Configuration。
- `Properties/PublishProfiles/`
  发布与打包配置（MSIX 发布）。
- `bin/`、`obj/`
  编译输出与中间文件（可忽略进版本控制）。

如果你熟悉 Java 生态，可以把 WinUI 3 理解为“WPF 的现代化实现 + Windows App SDK”，XAML 对应 UI 描述，`.csproj` 对应构建配置，`App`/`MainWindow` 对应应用入口与主窗体。
