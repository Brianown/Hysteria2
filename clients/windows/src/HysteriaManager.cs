using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Text;
using System.Threading;

namespace HysteriaLite
{
    public sealed class HysteriaManager
    {
        private const string CoreUrl =
            "https://github.com/apernet/hysteria/releases/latest/download/hysteria-windows-amd64.exe";
        private const long CoreMinBytes = 15 * 1024 * 1024;

        private readonly string baseDirectory;
        private Process process;
        private readonly object logLock = new object();
        private string logPath;
        private bool proxyWasSet;
        private object oldProxyEnable;
        private object oldProxyServer;

        public HysteriaManager(string baseDirectory)
        {
            this.baseDirectory = baseDirectory;
        }

        public string HysteriaPath
        {
            get { return Path.Combine(baseDirectory, "hysteria.exe"); }
        }

        public bool IsRunning
        {
            get
            {
                try { return process != null && !process.HasExited; }
                catch (Exception) { return false; }
            }
        }

        public void Start(HysteriaProfile profile, bool tunMode)
        {
            Stop();
            EnsureCore();
            if (!tunMode) SetSystemProxy();

            string configPath = Path.Combine(baseDirectory, "client.yaml");
            File.WriteAllText(configPath, BuildConfig(profile, tunMode), new UTF8Encoding(false));
            logPath = Path.Combine(baseDirectory, "hysteria.log");
            try { File.WriteAllText(logPath, string.Empty, new UTF8Encoding(false)); } catch (Exception) { }

            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = HysteriaPath;
            info.Arguments = "client -c \"" + configPath + "\"";
            info.WorkingDirectory = baseDirectory;
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;

            process = new Process();
            process.StartInfo = info;
            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs args)
            {
                if (!string.IsNullOrEmpty(args.Data)) AppendLog(args.Data);
            };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs args)
            {
                if (!string.IsNullOrEmpty(args.Data)) AppendLog(args.Data);
            };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            Thread.Sleep(5000);
            if (process.HasExited)
            {
                int code = process.ExitCode;
                string log = ReadLogTail();
                process = null;
                throw new InvalidOperationException(
                    "Hysteria2 启动失败（退出码 " + code + "）。\r\n" + log);
            }

            // TUN 已建立后再做一次连通性检查；失败就自动退出，避免整台电脑断网。
            if (tunMode)
            {
                try
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    using (WebClient probe = new WebClient())
                    {
                        probe.Proxy = null;
                        probe.Headers.Add("User-Agent", "HysteriaLite");
                        probe.DownloadData("http://www.baidu.com");
                    }
                }
                catch (Exception)
                {
                    string log = ReadLogTail();
                    Stop();
                    throw new InvalidOperationException(
                        "TUN 已启动但无法访问网络，已自动断开并恢复系统网络。\r\n" + log);
                }
            }
        }

        public void Stop()
        {
            if (process == null)
                return;
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
            }
            catch (Exception) { }
            finally
            {
                try { process.Dispose(); } catch (Exception) { }
                process = null;
            }
        }

        private void EnsureCore()
        {
            if (File.Exists(HysteriaPath) && new FileInfo(HysteriaPath).Length >= CoreMinBytes)
                return;

            // 删除上次中断留下的半截文件，避免把 2MB 的残缺 exe 当成完整内核。
            try
            {
                if (File.Exists(HysteriaPath))
                    File.Delete(HysteriaPath);
            }
            catch (Exception)
            {
            }

            string partPath = HysteriaPath + ".part";
            try
            {
                try { if (File.Exists(partPath)) File.Delete(partPath); } catch (Exception) { }
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "HysteriaLite");
                    client.DownloadFile(CoreUrl, partPath);
                }
                if (!File.Exists(partPath) || new FileInfo(partPath).Length < CoreMinBytes)
                    throw new InvalidOperationException("下载到的 hysteria.exe 不完整。");
                File.Move(partPath, HysteriaPath);
            }
            catch (Exception ex)
            {
                try { if (File.Exists(partPath)) File.Delete(partPath); } catch (Exception) { }
                throw new InvalidOperationException(
                    "缺少完整的 hysteria.exe，自动下载失败：" + ex.Message + "\r\n\r\n" +
                    "请手动下载：\r\n" + CoreUrl + "\r\n" +
                    "改名为 hysteria.exe，放在 HysteriaLite.exe 同目录。");
            }
        }

        private string BuildConfig(HysteriaProfile profile, bool tunMode)
        {
            StringBuilder b = new StringBuilder();
            b.AppendLine("server: " + profile.Host + ":" + profile.Port);
            b.AppendLine("auth: " + profile.Auth);
            b.AppendLine("tls:");
            b.AppendLine("  sni: " + profile.Sni);
            b.AppendLine("  insecure: " + (profile.Insecure ? "true" : "false"));
            if (!string.IsNullOrEmpty(profile.ObfsPassword))
            {
                b.AppendLine("obfs:");
                b.AppendLine("  type: salamander");
                b.AppendLine("  salamander:");
                b.AppendLine("    password: " + profile.ObfsPassword);
            }
            b.AppendLine("tun:");
            b.AppendLine("  name: hysteria2");
            b.AppendLine("  mtu: 1400");
            b.AppendLine("  timeout: 1m");
            b.AppendLine("  address:");
            b.AppendLine("    ipv4: 100.100.100.101/30");
            b.AppendLine("  route:");
            b.AppendLine("    ipv4:");
            b.AppendLine("      - 0.0.0.0/0");
            b.AppendLine("  dns:");
            b.AppendLine("    - 1.1.1.1");
            b.AppendLine("    - 8.8.8.8");
            return b.ToString();
        }

        private void AppendLog(string line)
        {
            try
            {
                lock (logLock)
                {
                    if (!string.IsNullOrEmpty(logPath))
                        File.AppendAllText(logPath, line + "\r\n", new UTF8Encoding(false));
                }
            }
            catch (Exception) { }
        }

        private string ReadLogTail()
        {
            try
            {
                if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
                    return string.Empty;
                string text = File.ReadAllText(logPath);
                return text.Length > 3000 ? text.Substring(text.Length - 3000) : text;
            }
            catch (Exception) { return string.Empty; }
        }
        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetSetOption(IntPtr hInternet, int option, IntPtr buffer, int bufferLength);
        private const int InternetOptionSettingsChanged = 39;
        private const int InternetOptionRefresh = 37;

        private void SetSystemProxy()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
                {
                    if (key == null) throw new InvalidOperationException("无法打开 Internet Settings");
                    oldProxyEnable = key.GetValue("ProxyEnable");
                    oldProxyServer = key.GetValue("ProxyServer");
                    proxyWasSet = true;
                    key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
                    key.SetValue("ProxyServer", "127.0.0.1:18080", RegistryValueKind.String);
                }
                InternetSetOption(IntPtr.Zero, InternetOptionSettingsChanged, IntPtr.Zero, 0);
                InternetSetOption(IntPtr.Zero, InternetOptionRefresh, IntPtr.Zero, 0);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("设置系统代理失败：" + ex.Message);
            }
        }

        private void RestoreSystemProxy()
        {
            if (!proxyWasSet)
                return;
            proxyWasSet = false;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
                {
                    if (key == null) return;
                    if (oldProxyEnable == null) key.DeleteValue("ProxyEnable", false);
                    else key.SetValue("ProxyEnable", oldProxyEnable, RegistryValueKind.DWord);
                    if (oldProxyServer == null) key.DeleteValue("ProxyServer", false);
                    else key.SetValue("ProxyServer", oldProxyServer, RegistryValueKind.String);
                }
                InternetSetOption(IntPtr.Zero, InternetOptionSettingsChanged, IntPtr.Zero, 0);
                InternetSetOption(IntPtr.Zero, InternetOptionRefresh, IntPtr.Zero, 0);
            }
            catch (Exception) { }
        }    }
}
