# Keyboard Cooldown Lock / 键盘降温锁

一个仅锁定键盘、保留鼠标操作的轻量 Windows 小工具。适合清洁键盘、暂时防止误触，或让正在运行任务的电脑短暂休息。

> Keyboard-only input lock for Windows. The mouse stays usable and a visible button always provides a recovery path.

## 功能

- 只拦截键盘，不拦截鼠标。
- 默认 15 分钟后自动解锁，可用鼠标延长 5 分钟。
- 中文置顶恢复窗口，提供醒目的“立即解锁键盘”按钮。
- 单实例保护，重复启动不会叠加多个钩子。
- 无驱动、无管理员权限、无后台服务。
- 异常、关闭和超时路径都会显式释放键盘钩子。

## 安全边界

- 适用于 Windows 10/11 桌面会话。
- 使用 `WH_KEYBOARD_LL` 用户态低级键盘钩子。
- `Ctrl+Alt+Delete` 属于 Windows 安全注意序列，不会被本工具拦截。
- 它不是 Windows 账户安全锁，不应用于防止未授权访问。
- 未使用 Interception 等驱动级拦截，因为其锁死输入的恢复风险更高。

## 使用

1. 构建项目：

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
   ```

2. 安装到当前 Windows 用户并创建桌面快捷方式：

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
   ```

3. 双击桌面的“键盘降温锁”。

## 命令行参数

- `--minutes 15`：按分钟设置，范围 1–120。
- `--seconds 10`：测试用秒数，范围 3–7200。
- `--self-test`：快速安装并卸载钩子，然后退出。

## 构建要求

- Windows PowerShell 5.1+
- .NET 10 SDK（仓库用 `global.json` 固定为 10.0.101）

`build.ps1` 使用锁定的 NuGet 依赖，在 `build/` 中生成 `win-x64`、self-contained 的单文件 WPF EXE。可通过 `YINGQI_DOTNET` 指定 SDK 位置。

构建链会先运行 `KeyboardLockComponent.Tests`，再执行独立 EXE 冒烟测试：真实注入按键的 Hook 自检、可见恢复窗口、自动超时、跨进程单实例，以及测试进程被强制终止后的 Hook 释放。也可单独运行：

```powershell
dotnet test .\tests\KeyboardLockComponent.Tests\KeyboardLockComponent.Tests.csproj -c Release
```

## 隐私

工具不记录按键、不读取按键内容、不联网，也不收集遥测数据。钩子回调只返回非零值以阻止事件继续传递。

## Component API

构建会同时生成独立 `KeyboardCoolDownLock.exe` 和可嵌入 Yingqi Tools 的 `KeyboardLockComponent.dll`：

- `KeyboardLockControl`：可嵌入 WPF 容器的 Fluent 设置面板。
- `KeyboardLockSession.TryStart(TimeSpan)`：启动带鼠标恢复界面的锁定会话。
- `KeyboardLockSession.Stop()`：释放钩子并停止会话。

独立 EXE 和嵌入组件使用相同的跨进程单实例锁，不会叠加多个键盘钩子。

## v2 界面

- `.NET 10 WPF` + `WPF UI 4.3.0`。
- 锁定窗口采用原生 Fluent 控件、倒计时、线性进度与“延长 5 分钟”。
- 支持系统浅色/深色资源，不使用驱动或后台服务。

## 参考实现与取舍

- [`jonneyj/Keyboard-Locker`](https://github.com/jonneyj/Keyboard-Locker)：可移植 EXE 和鼠标恢复思路。
- [`MrRogueKnight/KeyFreeze`](https://gist.github.com/MrRogueKnight/1be03d45d0785f831776951a3c4924fe)：`WH_KEYBOARD_LL` 选择性拦截和回调委托生命期处理。
- [`luke99810/Safe_Lock`](https://github.com/luke99810/Safe_Lock)：可见恢复 UI 和可执行文件打包；该项目同时锁鼠标，不符合本工具目标。
- 未采用 Interception 驱动级方案：恢复风险更高，可能拦截安全注意序列，也需要额外安装和权限。
- Microsoft [`LowLevelKeyboardProc`](https://learn.microsoft.com/windows/win32/winmsg/lowlevelkeyboardproc) / [`SetWindowsHookEx`](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowshookexw) 文档：回调必须尽快返回、安装线程必须有消息循环、退出前显式 `UnhookWindowsHookEx`。

## License

[MIT](LICENSE)
