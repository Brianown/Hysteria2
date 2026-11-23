# Hysteria2 Lite for Windows

基于 Hysteria2 官方内核的 TUN 图形客户端，界面与 Xray Lite 类似。

## 使用

1. 下载 `HysteriaLite-Windows.zip` 并解压；
2. 运行 `HysteriaLite.exe`（需要管理员权限）；
3. 点“导入链接”，粘贴服务器生成的 `hysteria2://` 链接；
4. 选中节点 → 连接；
5. 压缩包已内置 `hysteria.exe`，无需联网下载；
6. TUN 全局模式，QUIC + TLS 1.3。

如果内核损坏，可以手动下载：

```text
https://github.com/apernet/hysteria/releases/latest/download/hysteria-windows-amd64.exe
```

改名为 `hysteria.exe`，放在 `HysteriaLite.exe` 同目录。

## 文件

```text
HysteriaLite.exe   界面
wintun.dll         Windows TUN 驱动
hysteria.exe       已内置 Hysteria2 内核
client.yaml        连接时自动生成
hysteria.log       内核日志
```

## 构建

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

需要 .NET Framework 4.x 的 `csc.exe`。
