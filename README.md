# 屏幕共享 · 桌面版（共享 / 观看 二合一）

原生桌面程序：**一个软件、一个 exe、两个标签页**。
Windows 电脑（Win10/11，无需安装任何运行时，.NET Framework 4.8 系统自带）双击即用。

## 文件

| 文件 | 说明 |
|---|---|
| `bin\ScreenShare.exe` | 二合一软件（共享端 + 观看端） |
| `build.ps1` | 重新编译（用系统自带 csc，无需 SDK） |

## 使用步骤（两台电脑各运行同一个 exe）

**共享方（A 电脑）**
1. 双击 `ScreenShare.exe` → 切到「**共享端（发送）**」页；
2. 点「**开始共享**」（默认 PNG 无损 / 20fps；想更流畅可切 JPEG）；
3. 窗口显示本机 IP，等待观看端自动接入；「已连接观看端」列表实时显示。

**观看方（B 电脑）**
1. 双击 `ScreenShare.exe` → 切到「**观看端（接收）**」页；
2. 程序**自动向局域网广播发现**共享端 → 左侧列表出现 → **自动连接、自动出画面**；
3. 也可右上角输入 `IP:端口` 点「直连」（跨网段兜底）；
4. 双击画面 / **F11** 全屏（Esc 退出），看状态栏的帧率与分辨率；
5. 断线自动重连。

## 一键自动配置雷电网桥（雷电 / USB4 点对点网络）

两个页面里都有**「自动配置雷电网桥」**按钮（与《雷电网桥自动配置》工具同源逻辑，脚本已嵌入 exe）：

1. 用雷电 / USB4 线连接两台电脑；
2. **两台电脑都点「自动配置雷电网桥」**（A 页或 B 页均可）；
3. UAC 弹窗选「是」（需管理员）；
4. 自动完成：检测 USB4 P2P 网卡 → 启用等待链路 → UDP 握手（按 MAC 决定主从）→ 自动分配 `192.168.250.1/30` 与 `.2/30` → 专用网络 + 仅对端可 Ping 的防火墙规则 → 连通性测试；
5. 3 分钟内两端同时进入等待即可；可选勾选「开启文件共享(SMB)」支持 `\\192.168.250.2` 访问。

## 工作原理

- **自动联系**：观看端启动后向所有网卡定向广播发 UDP `SCREENSHARE|DISC`；共享端监听 UDP 45555 并单播回应 `SCREENSHARE|HERE|主机名|端口`——零配置即插即用；
- **传输**：共享端 GDI 抓取屏幕**原始像素**（BitBlt 无色彩转换）→ PNG **无损**编码（可选 JPEG）→ TCP 45556 点对点帧流（帧头 `[格式][宽][高][长度]`），每观看端独立发送队列（快慢互不影响）；
- 同一台电脑可同时开两个实例（一个共享、一个观看），端口互不冲突（TCP 45556 仅共享端监听；观看端只发出站连接）。

## 防火墙（仅共享端需要）

共享端首次运行若弹 Windows 防火墙询问请选「允许」。未弹出时以管理员执行：

```
netsh advfirewall firewall add rule name="ScreenShare" dir=in action=allow protocol=TCP localport=45556 profile=private,domain
netsh advfirewall firewall add rule name="ScreenShare-UDP" dir=in action=allow protocol=UDP localport=45555 profile=private,domain
```

## 常见问题

- **没自动发现**：确认两台设备同一网段互通（雷电网桥 192.168.250.x / WiFi）；可右上角手动直连；
- **画面卡**：共享端降帧率或切 JPEG；4K 屏建议 JPEG 质量 85~90；
- **颜色**：PNG 为屏幕原始像素无损传输，无色差；JPEG 有轻微有损；
- **只抓主屏**：当前版本共享主显示器全屏；多屏/区域选择后续版本再加。

## 命令行参数（可选）

```
ScreenShare.exe -headless                  # 无人值守共享（测试/服务器）
ScreenShare.exe -format jpeg -fps 25 -quality 85 -port 45600
```

## 项目文件

- `App.cs` 入口与参数（`-headless` 等）
- `HostEngine.cs` 共享引擎（捕获/编码/UDP 发现/TCP 帧流）
- `ScreenShareForm.cs` 二合一主窗体（共享页 + 观看页）
- `BridgeConfigurer.cs` 雷电网桥自动配置（内嵌脚本提取执行）
- `Enable-ThunderboltBridge.ps1` 雷电网桥配置脚本（编译时嵌入 exe）
- `scripts\test-client.js` 收帧校验脚本（测试用）
