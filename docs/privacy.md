# 隐私说明

Hysteria2 使用 QUIC + TLS 1.3：

- 本地运营商看不到 QUIC 内部的访问目标和内容；
- 服务器仍然能看到你访问的目标 IP/域名；
- QUIC 可以被识别、限速或阻断；
- 自签名证书不等同于 REALITY 的真实网站伪装；
- 没有任何协议能保证绝对匿名。

如果目标是最大限度抗主动探测，优先使用 Xray REALITY；如果目标是丢包链路上的速度，使用 Hysteria2 或 WireGuard。
