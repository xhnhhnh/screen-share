using System;
using System.Windows.Forms;

namespace ScreenShare
{
    /// <summary>命令行参数（-port / -format / -fps / -quality / -headless / -host / -viewer）</summary>
    public static class Settings
    {
        public static int Port = 45556;               // A 端 TCP 帧流端口
        public static int DiscoveryPort = 45555;      // A 端 UDP 发现端口
        public static string Format = "png";          // png(无损) / jpeg
        public static int Fps = 20;
        public static int Quality = 90;               // JPEG 质量（PNG 忽略）
        public static bool Headless = false;          // 无窗体模式（测试用）
    }

    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            CrashGuard.Attach();

            // 解析命令行
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "-port" && i + 1 < args.Length) { int.TryParse(args[++i], out Settings.Port); }
                else if (a == "-discovery-port" && i + 1 < args.Length) { int.TryParse(args[++i], out Settings.DiscoveryPort); }
                else if (a == "-format" && i + 1 < args.Length) { Settings.Format = args[++i].ToLowerInvariant(); }
                else if (a == "-fps" && i + 1 < args.Length) { int.TryParse(args[++i], out Settings.Fps); }
                else if (a == "-quality" && i + 1 < args.Length) { int.TryParse(args[++i], out Settings.Quality); }
                else if (a == "-headless") { Settings.Headless = true; }
            }

            if (Settings.Headless)
            {
                // 无窗体引擎模式：无人值守共享（自动化测试/服务器场景）
                HostEngine engine = new HostEngine();
                engine.Start();
                System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ScreenShareForm());
        }
    }
}
