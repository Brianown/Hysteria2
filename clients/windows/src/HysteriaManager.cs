using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace HysteriaLite
{
    public sealed class HysteriaManager
    {
        private const string CoreUrl =
            "https://github.com/apernet/hysteria/releases/latest/download/hysteria-windows-amd64.exe";

        private readonly string baseDirectory;
        private Process process;
        private readonly object logLock = new object();
        private string logPath;

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

        public void Start(HysteriaProfile profile)
        {
            Stop();
            EnsureCore();

            string configPath = Path.Combine(baseDirectory, "client.yaml");
            File.WriteAllText(configPath, BuildConfig(profile), new UTF8Encoding(false));
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
            if (File.Exists(HysteriaPath))
                return;
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "HysteriaLite");
                    client.DownloadFile(CoreUrl, HysteriaPath);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "缺少 hysteria.exe，自动下载失败：" + ex.Message + "\r\n" +
                    "请手动下载 hysteria-windows-amd64.exe 并改名为 hysteria.exe 放在本目录。");
            }
        }

        private string BuildConfig(HysteriaProfile profile)
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
            b.AppendLine("  name: hysteria");
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
    }
}
