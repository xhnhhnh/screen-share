using System;
using System.IO;
using System.Windows.Forms;

namespace ScreenShare
{
    /// <summary>全局异常捕获：把完整堆栈写入 logs\crash.log，并弹出友好提示（替代系统闪崩框）</summary>
    public static class CrashGuard
    {
        public static void Attach()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
            {
                Report(e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                Exception ex = e.ExceptionObject as Exception;
                if (ex != null) Report(ex);
            };
        }

        private static void Report(Exception ex)
        {
            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, "crash.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + ex.GetType().FullName + "\r\n" +
                    ex.ToString() + "\r\n\r\n");
            }
            catch { }

            try
            {
                MessageBox.Show(
                    "发生错误：" + ex.Message + "\r\n\r\n" +
                    "详细信息已写入 logs\\crash.log（程序目录下）。\r\n" +
                    "点击「是」继续运行，点击「否」退出程序。",
                    "屏幕共享 - 意外错误",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            }
            catch { }
        }
    }
}
