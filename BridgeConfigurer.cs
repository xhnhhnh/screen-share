using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace ScreenShare
{
    /// <summary>
    /// 自动配置雷电/USB4 点对点网络：
    /// 提取内嵌配置脚本（与"雷电网桥自动配置"工具一致）→ 调用 PowerShell 执行。
    /// 脚本会自行检测管理员权限并弹出 UAC，在独立控制台窗口输出 7 步进度与结果。
    /// </summary>
    public static class BridgeConfigurer
    {
        public const string ResourceName = "ScreenShare.Bridge.ps1";

        /// <summary>把内嵌脚本提取到 %TEMP% 并返回路径（UTF-8 BOM，保证 PowerShell 5.1 正确解析中文）</summary>
        public static string ExtractScript()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string dest = Path.Combine(Path.GetTempPath(), "Enable-ThunderboltBridge.ps1");
            byte[] data = null;
            using (Stream s = asm.GetManifestResourceStream(ResourceName))
            {
                if (s == null) throw new InvalidOperationException("内嵌配置脚本缺失: " + ResourceName);
                using (MemoryStream ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    data = ms.ToArray();
                }
            }
            File.WriteAllBytes(dest, data); // 原脚本本身带 UTF-8 BOM，直接按字节写回
            return dest;
        }

        /// <summary>启动自动配置（独立控制台窗口；脚本自行 UAC 提权并显示进度）</summary>
        public static bool Run(bool enableFileSharing)
        {
            try
            {
                string script = ExtractScript();
                string pwsh = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "WindowsPowerShell\\v1.0\\powershell.exe");
                if (!File.Exists(pwsh)) pwsh = "powershell.exe";

                string args = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\"";
                if (enableFileSharing) args += " -EnableFileSharing";

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = pwsh;
                psi.Arguments = args;
                psi.UseShellExecute = true; // 弹出独立控制台窗口（脚本内容与其参考工具一致）
                psi.WorkingDirectory = Path.GetTempPath();
                Process p = Process.Start(psi);
                return p != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
