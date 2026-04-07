# CLAUDE.md

## 项目说明

这个仓库是一个基于 WinUI 3 的 Windows 连点器。界面提供点击间隔输入框，程序监听全局热键 `F10`，用于在当前鼠标位置开始或停止连点。

## 仓库结构

- `Auto-Clicker.slnx`：解决方案入口
- `Auto-Clicker/Auto-Clicker.csproj`：项目定义
- `Auto-Clicker/MainWindow.xaml`：主界面
- `Auto-Clicker/MainWindow.xaml.cs`：主要运行逻辑
- `Auto-Clicker/App.xaml*`：应用启动相关文件
- `Auto-Clicker/Assets/`：应用资源文件

## 技术栈

- .NET 10（目标框架：`net10.0-windows10.0.19041.0`）
- WinUI 3
- Windows App SDK 1.8
- 通过 `user32.dll` 进行 Windows 桌面互操作

## 重点约束

- 这是一个刻意保持小而简单的项目，不要过度设计
- 大部分核心逻辑集中在 `MainWindow.xaml.cs`
- 当前默认热键是 `F10`，不是 `F8`
- 保持项目既能在 Visual Studio 中顺畅构建，也能通过 `dotnet build` 构建

## 编码建议

- 优先做小范围、直接的修改，不要轻易引入大重构
- 保持当前 C# 命名和代码风格一致
- 界面应尽量保持简单、直观、符合原生桌面程序习惯
- 没有充分理由时不要增加新依赖
- 不要编辑 `bin/` 或 `obj/` 下的生成文件

## 修改行为时需要注意

- 实现变更后，要同步更新用户可见文案
- 状态提示必须准确、易读
- 热键注册与释放逻辑要保持成对、对称
- 停止逻辑必须仍然能够可靠取消连点循环

## 构建与验证

- 构建命令：
  - `dotnet build .\Auto-Clicker.slnx -c Release`
- 手动验证建议：
  - 启动程序
  - 将点击间隔设为 `100` 之类的有效值
  - 按 `F10` 开始连点
  - 再按一次 `F10` 停止连点
  - 如果热键被占用，确认界面会显示注册失败提示

## 边界说明

- 把它当作本地桌面小工具，而不是通用框架
- 除非用户明确要求，不要加入隐藏后台行为或权限敏感改动
- 如果文档和源码冲突，以源码为准
