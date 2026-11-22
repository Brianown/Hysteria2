# Hysteria2

Hysteria2 是基于 QUIC/UDP 的抗丢包代理，适合高延迟、丢包严重的网络。

## 它与 Xray REALITY / WireGuard 的区别

| 特性 | Hysteria2 | Xray REALITY | WireGuard |
| --- | --- | --- | --- |
| 传输 | QUIC/UDP + TLS 1.3 | TCP + TLS 1.3 + REALITY | UDP + NoiseIK |
| 丢包链路速度 | 好 | 较差 | 很好 |
| 伪装成正常网站 | 中等（QUIC/HTTP3 外观） | 最强 | 弱 |
| 主动探测 | 中等 | 强 | 弱 |
| 客户端 TUN | 支持 | 支持 | 支持 |

Hysteria2 不是 REALITY，不能做到“主动探测时返回真实网站证书”的效果；它更适合“速度优先、能接受 QUIC 外观”的场景。

## 服务端安装

```bash
curl -fsSL https://github.com/Brianown/Hysteria2/archive/refs/heads/main.tar.gz -o /tmp/hysteria2.tar.gz && sudo tar xzf /tmp/hysteria2.tar.gz -C /opt && sudo bash /opt/Hysteria2-main/server/install.sh
```

自定义端口/域名：

```bash
sudo HY_PORT=443 SERVER_NAME=www.apple.com bash server/install.sh
```

启用 Salamander 混淆：

```bash
sudo HY_OBFS=1 bash server/install.sh
```

安装完成后：

```text
/root/hysteria2/uri.txt
/root/hysteria2/uri.png
```

云安全组需要放行 **UDP 443**（或自定义 HY_PORT）。

## 客户端

### Windows

- [NekoRay / NekoBox](https://github.com/MatsuriDayo/nekoray/releases/latest)
- 或 v2rayN（支持 Hysteria2）

导入服务器上的 `hysteria2://` URI，开启 TUN/系统代理。

### Android

- [NekoBox for Android](https://github.com/MatsuriDayo/NekoBoxForAndroid/releases/latest)

导入 URI 或扫码。

## 关于证书

默认使用自签名证书：

- 客户端 URI 带 `insecure=1`；
- 能加密，但不会像 REALITY 那样返回真实网站证书；
- 正式长期使用建议绑定域名，配置 ACME 证书，去掉 `insecure=1`。

## 项目状态

服务端安装脚本已提供；客户端使用成熟的 NekoBox/NekoRay/v2rayN，不重复造 GUI。
