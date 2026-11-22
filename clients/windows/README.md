# Hysteria2 客户端

## Windows

1. 下载 NekoRay/NekoBox：<https://github.com/MatsuriDayo/nekoray/releases/latest>
2. 解压运行；
3. 从服务器 `/root/hysteria2/uri.txt` 复制 `hysteria2://` 链接；
4. 从剪贴板导入；
5. 启动 TUN 模式。

## Android

1. 下载 NekoBox for Android：<https://github.com/MatsuriDayo/NekoBoxForAndroid/releases/latest>
2. 扫描 `/root/hysteria2/uri.png`，或导入 URI；
3. 连接。

## 注意

- 自签名证书需要 `insecure=1`；
- 如果启用 Salamander，URI 里会带 `obfs=salamander&obfs-password=...`；
- 云安全组放行 UDP 443。
