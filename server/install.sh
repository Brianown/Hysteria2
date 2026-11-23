#!/usr/bin/env bash
# Hysteria2 一键安装：QUIC + TLS 1.3 + BBR，UDP 443
# 可选：HY_OBFS=1 启用 Salamander 混淆；默认不启用，以便模拟 HTTP/3。
set -Eeuo pipefail

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; BOLD='\033[1m'; RESET='\033[0m'
info() { printf '%b[INFO]%b %s\n' "${YELLOW}" "${RESET}" "$*"; }
ok() { printf '%b[ OK ]%b %s\n' "${GREEN}" "${RESET}" "$*"; }
warn() { printf '%b[WARN]%b %s\n' "${YELLOW}" "${RESET}" "$*"; }
die() { printf '%b[FAILED]%b %s\n' "${RED}" "${RESET}" "$*" >&2; exit 1; }
trap 'die "脚本在第 $LINENO 行执行失败。"' ERR

[[ ${EUID} -eq 0 ]] || die "请用 root 运行"
HY_PORT="${HY_PORT:-443}"
SERVER_NAME="${SERVER_NAME:-www.apple.com}"
HY_OBFS="${HY_OBFS:-0}"
[[ ${HY_PORT} =~ ^[0-9]+$ ]] || die "HY_PORT 必须是数字"
[[ ${SERVER_NAME} =~ ^[A-Za-z0-9.-]+$ ]] || die "SERVER_NAME 不合法"

detect_pm() {
    if command -v apt-get >/dev/null 2>&1; then PM=apt
    elif command -v dnf >/dev/null 2>&1; then PM=dnf
    elif command -v yum >/dev/null 2>&1; then PM=yum
    elif command -v zypper >/dev/null 2>&1; then PM=zypper
    elif command -v pacman >/dev/null 2>&1; then PM=pacman
    else PM=unknown; fi
}

install_deps() {
    detect_pm
    info "包管理器：${PM}"
    case "${PM}" in
        apt)
            export DEBIAN_FRONTEND=noninteractive
            apt-get update -qq
            apt-get install -y -qq --no-install-recommends curl openssl ca-certificates qrencode >/dev/null
            ;;
        dnf)
            "${PM}" install -y curl openssl ca-certificates qrencode >/dev/null 2>&1 || \
                "${PM}" install -y curl openssl ca-certificates >/dev/null
            ;;
        yum)
            yum install -y curl openssl ca-certificates >/dev/null
            yum install -y qrencode >/dev/null 2>&1 || warn "qrencode 未安装，二维码不可用。"
            ;;
        zypper)
            zypper --non-interactive install curl openssl ca-certificates qrencode >/dev/null
            ;;
        pacman)
            pacman -Sy --noconfirm --needed curl openssl ca-certificates qrencode >/dev/null
            ;;
        *) die "未知包管理器，请手动安装 curl/openssl 后重试" ;;
    esac
}

if [[ ${SKIP_DEPS:-0} == "1" ]]; then
    warn "SKIP_DEPS=1，跳过依赖安装。"
else
    install_deps
fi
for c in curl openssl systemctl; do command -v "${c}" >/dev/null || die "缺少 ${c}"; done

if ss -lun 2>/dev/null | awk '{print $4}' | grep -qE "[:.]${HY_PORT}$"; then
    die "UDP ${HY_PORT} 已被占用。可改用 HY_PORT=8443。"
fi

ARCH="$(uname -m)"
case "${ARCH}" in
    x86_64|amd64) HY_BIN="hysteria-linux-amd64" ;;
    aarch64|arm64) HY_BIN="hysteria-linux-arm64" ;;
    *) die "不支持的架构：${ARCH}" ;;
esac

info "下载 Hysteria2（${HY_BIN}）……"
TMP="$(mktemp -d)"
trap 'rm -rf "${TMP}"' EXIT
for i in 1 2 3 4 5; do
    if curl -fL --retry 3 --connect-timeout 30 --max-time 900 \
        -o "${TMP}/hysteria" \
        "https://github.com/apernet/hysteria/releases/latest/download/${HY_BIN}"; then
        break
    fi
    warn "下载失败，第 ${i}/5 次重试"
    sleep 5
done
[[ -s ${TMP}/hysteria ]] || die "Hysteria2 下载失败"
install -m 755 "${TMP}/hysteria" /usr/local/bin/hysteria
/usr/local/bin/hysteria version | head -n 1

PASSWORD="$(openssl rand -hex 16)"
OBFS_PASSWORD="$(openssl rand -hex 16)"
install -d -m 700 /etc/hysteria
install -d -m 700 /root/hysteria2

cat > "${TMP}/openssl.cnf" <<EOF
[req]
distinguished_name = dn
x509_extensions = v3
prompt = no
[dn]
CN = ${SERVER_NAME}
[v3]
subjectAltName = DNS:${SERVER_NAME}
EOF
openssl req -x509 -nodes -newkey rsa:2048 -days 3650 \
    -keyout /etc/hysteria/key.pem -out /etc/hysteria/cert.pem \
    -config "${TMP}/openssl.cnf" >/dev/null 2>&1
chmod 600 /etc/hysteria/key.pem /etc/hysteria/cert.pem

CONFIG=/etc/hysteria/config.yaml
cat > "${CONFIG}" <<EOF
listen: :${HY_PORT}
tls:
  cert: /etc/hysteria/cert.pem
  key: /etc/hysteria/key.pem
  sniGuard: disable
auth:
  type: password
  password: ${PASSWORD}
congestion:
  type: bbr
masquerade:
  type: proxy
  proxy:
    url: https://www.apple.com
    rewriteHost: true
EOF
if [[ ${HY_OBFS} == "1" ]]; then
    cat >> "${CONFIG}" <<EOF
obfs:
  type: salamander
  salamander:
    password: ${OBFS_PASSWORD}
EOF
fi
chmod 600 "${CONFIG}"

cat > /etc/systemd/system/hysteria-server.service <<'EOF'
[Unit]
Description=Hysteria2 server
After=network.target

[Service]
Type=simple
ExecStart=/usr/local/bin/hysteria server -c /etc/hysteria/config.yaml
Restart=on-failure
RestartSec=3
LimitNOFILE=65535

[Install]
WantedBy=multi-user.target
EOF
systemctl daemon-reload
systemctl enable --now hysteria-server.service >/dev/null 2>&1 || true
sleep 1
systemctl is-active --quiet hysteria-server.service || {
    journalctl -u hysteria-server -n 40 --no-pager || true
    die "Hysteria2 启动失败"
}
ok "Hysteria2 已启动：UDP ${HY_PORT}"

if command -v ufw >/dev/null 2>&1 && ufw status 2>/dev/null | grep -q "Status: active"; then
    ufw allow "${HY_PORT}/udp" >/dev/null 2>&1 || true
fi
if command -v firewall-cmd >/dev/null 2>&1 && firewall-cmd --state >/dev/null 2>&1; then
    firewall-cmd --permanent --add-port="${HY_PORT}/udp" >/dev/null 2>&1 || true
    firewall-cmd --reload >/dev/null 2>&1 || true
fi
ok "防火墙已放行 UDP ${HY_PORT}"

PUBLIC_IP="$(curl -4fsS --max-time 10 https://api.ipify.org 2>/dev/null || true)"
[[ -n ${PUBLIC_IP} ]] || PUBLIC_IP="$(curl -4fsS --max-time 10 https://ifconfig.me 2>/dev/null || true)"
[[ -n ${PUBLIC_IP} ]] || die "无法检测公网 IP"

URI="hysteria2://${PASSWORD}@${PUBLIC_IP}:${HY_PORT}/?insecure=1&sni=${SERVER_NAME}"
if [[ ${HY_OBFS} == "1" ]]; then
    URI="${URI}&obfs=salamander&obfs-password=${OBFS_PASSWORD}"
fi
URI="${URI}#Hysteria2-${SERVER_NAME}"

printf '%s\n' "${URI}" > /root/hysteria2/uri.txt
chmod 600 /root/hysteria2/uri.txt
if command -v qrencode >/dev/null 2>&1; then
    qrencode -o /root/hysteria2/uri.png -s 8 -m 2 < /root/hysteria2/uri.txt || true
    chmod 600 /root/hysteria2/uri.png 2>/dev/null || true
fi

cat <<EOF

${GREEN}${BOLD}Hysteria2 安装完成${RESET}

服务器    : ${PUBLIC_IP}
端口      : ${HY_PORT}/udp
SNI       : ${SERVER_NAME}
密码      : ${PASSWORD}
混淆      : $([[ ${HY_OBFS} == "1" ]] && echo "Salamander" || echo "关闭（模拟 HTTP/3）")
客户端 URI: /root/hysteria2/uri.txt
二维码    : /root/hysteria2/uri.png

完整链接（直接复制到客户端）：
${URI}

Windows：HysteriaLite / NekoRay / NekoBox / v2rayN 导入 URI
Android：NekoBox for Android 导入 URI

注意：
  1. 云安全组需要放行 UDP ${HY_PORT}；
  2. 自签名证书，客户端必须设置 insecure=1；
  3. 正式长期使用建议绑定域名并改用 ACME 证书；
  4. URI 含密码，不要发到公开群。
EOF
