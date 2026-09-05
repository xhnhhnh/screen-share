using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ScreenShare
{
    /// <summary>
    /// A 端引擎：GDI 捕获屏幕（屏幕原始像素，无色彩转换）→ PNG/JPEG 编码
    /// → TCP 帧流发送；UDP 广播发现（B 端自动联系）。
    /// </summary>
    public sealed class HostEngine : IDisposable
    {
        public const byte FmtPng = 0;
        public const byte FmtJpeg = 1;

        public event Action<int> ClientCountChanged;
        public event Action<string> Log;

        private CaptureThread _capture;
        private UdpListener _udp;
        private TcpListener _tcp;
        private readonly object _clientsLock = new object();
        private readonly List<ClientSession> _clients = new List<ClientSession>();
        private bool _started;

        public int Port { get { return Settings.Port; } }
        public int ClientCount { get { lock (_clientsLock) return _clients.Count; } }

        /// <summary>已连接客户端地址列表（界面展示用）</summary>
        public List<string> GetClientEndpoints()
        {
            List<string> list = new List<string>();
            lock (_clientsLock)
            {
                foreach (ClientSession c in _clients)
                    if (c.Alive) list.Add(c.RemoteEp);
            }
            return list;
        }

        public void Start()
        {
            if (_started) return;
            _started = true;

            _tcp = new TcpListener(IPAddress.Any, Settings.Port);
            _tcp.Start();
            Thread tAccept = new Thread(AcceptLoop);
            tAccept.IsBackground = true;
            tAccept.Start();

            _udp = new UdpListener(Settings.DiscoveryPort, "SCREENSHARE|HERE|" + SafeHostName() + "|" + Settings.Port + "|");
            try { _udp.Start(); } catch (Exception e) { LogMsg("UDP 发现端口启动失败: " + e.Message); }

            _capture = new CaptureThread(OnFrame);
            _capture.Start();
            LogMsg("A 端服务已启动: TCP " + Settings.Port + " / UDP 发现 " + Settings.DiscoveryPort + " / 格式 " +
                   (Settings.Format == "png" ? "PNG(无损)" : "JPEG") + " / " + Settings.Fps + "fps");
        }

        /// <summary>每帧对所有客户端入队（每客户端保留最新一帧）</summary>
        private void OnFrame(byte format, int width, int height, byte[] imageData)
        {
            FrameBuffer frame = new FrameBuffer(format, width, height, imageData);
            lock (_clientsLock)
            {
                for (int i = _clients.Count - 1; i >= 0; i--)
                {
                    ClientSession c = _clients[i];
                    if (c.Alive) c.Enqueue(frame);
                    else { _clients.RemoveAt(i); c.Dispose(); }
                }
            }
        }

        private void AcceptLoop()
        {
            while (_started)
            {
                try
                {
                    TcpClient tcp = _tcp.AcceptTcpClient();
                    ClientSession c = new ClientSession(tcp, RemoveClient);
                    lock (_clientsLock) _clients.Add(c);
                    c.Start();
                    LogMsg("客户端接入: " + c.RemoteEp + "（当前 " + ClientCount + " 台）");
                    if (ClientCountChanged != null) ClientCountChanged(ClientCount);
                }
                catch { if (_started) LogMsg("接受连接失败"); }
            }
        }

        private void RemoveClient(ClientSession c)
        {
            lock (_clientsLock) _clients.Remove(c);
            LogMsg("客户端断开: " + c.RemoteEp + "（当前 " + ClientCount + " 台）");
            if (ClientCountChanged != null) ClientCountChanged(ClientCount);
        }

        private void LogMsg(string s) { if (Log != null) Log(s); }

        public void Dispose()
        {
            if (!_started) return;
            _started = false;
            if (_capture != null) _capture.Stop();
            if (_udp != null) _udp.Stop();
            if (_tcp != null) { try { _tcp.Stop(); } catch { } }
            lock (_clientsLock) { foreach (ClientSession c in _clients) c.Dispose(); _clients.Clear(); }
        }

        /// <summary>本机非内部 IPv4 地址（用于界面展示）</summary>
        public static List<string> GetLanIps()
        {
            List<string> ips = new List<string>();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (IPAddress.IsLoopback(ip.Address)) continue;
                        ips.Add(ip.Address.ToString());
                    }
                }
            }
            catch { }
            return ips;
        }

        private static string SafeHostName()
        {
            try { return Environment.MachineName.Replace("|", "_"); } catch { return "host"; }
        }
    }

    /// <summary>一帧数据（只读共享）</summary>
    internal sealed class FrameBuffer
    {
        public readonly byte Format;
        public readonly int Width;
        public readonly int Height;
        public readonly byte[] Data;
        public FrameBuffer(byte f, int w, int h, byte[] d) { Format = f; Width = w; Height = h; Data = d; }
    }

    /// <summary>单个客户端会话：最新帧队列 + 发送线程（慢客户端不拖慢捕获）</summary>
    internal sealed class ClientSession : IDisposable
    {
        private readonly TcpClient _tcp;
        private readonly Action<ClientSession> _onClose;
        private readonly object _lock = new object();
        private readonly Queue<FrameBuffer> _q = new Queue<FrameBuffer>();
        private readonly AutoResetEvent _ev = new AutoResetEvent(false);
        private Thread _sendThread;
        private volatile bool _alive = true;

        public bool Alive { get { return _alive; } }
        public string RemoteEp { get; private set; }

        public ClientSession(TcpClient tcp, Action<ClientSession> onClose)
        {
            _tcp = tcp;
            _onClose = onClose;
            try { RemoteEp = tcp.Client.RemoteEndPoint.ToString(); } catch { RemoteEp = "?"; }
        }

        public void Start()
        {
            _sendThread = new Thread(SendLoop);
            _sendThread.IsBackground = true;
            _sendThread.Start();
        }

        /// <summary>入队最新帧（队列始终只保留最新一帧）</summary>
        public void Enqueue(FrameBuffer f)
        {
            lock (_lock)
            {
                _q.Clear();
                _q.Enqueue(f);
            }
            _ev.Set();
        }

        private void SendLoop()
        {
            try
            {
                NetworkStream ns = _tcp.GetStream();
                byte[] header = new byte[13];
                while (_alive)
                {
                    _ev.WaitOne();
                    FrameBuffer f = null;
                    lock (_lock)
                    {
                        if (_q.Count > 0) { f = _q.Dequeue(); _q.Clear(); }
                    }
                    if (f == null) continue;
                    // 帧头: [format(1)][width(4)][height(4)][len(4)] 大端
                    header[0] = f.Format;
                    header[1] = (byte)(f.Width >> 24); header[2] = (byte)(f.Width >> 16);
                    header[3] = (byte)(f.Width >> 8); header[4] = (byte)f.Width;
                    header[5] = (byte)(f.Height >> 24); header[6] = (byte)(f.Height >> 16);
                    header[7] = (byte)(f.Height >> 8); header[8] = (byte)f.Height;
                    int len = f.Data.Length;
                    header[9] = (byte)(len >> 24); header[10] = (byte)(len >> 16);
                    header[11] = (byte)(len >> 8); header[12] = (byte)len;
                    ns.Write(header, 0, header.Length);
                    ns.Write(f.Data, 0, f.Data.Length);
                    ns.Flush();
                }
            }
            catch { }
            finally { Shutdown(); }
        }

        private void Shutdown()
        {
            if (!_alive) return;
            _alive = false;
            try { _tcp.Close(); } catch { }
            try { if (_onClose != null) _onClose(this); } catch { }
        }

        public void Dispose()
        {
            _alive = false;
            _ev.Set();
            try { _tcp.Close(); } catch { }
            _ev.Dispose();
        }
    }

    /// <summary>UDP 广播发现应答：收到 "SCREENSHARE|DISC" → 单播回 HERE 信息</summary>
    internal sealed class UdpListener
    {
        private readonly int _port;
        private readonly string _reply;
        private UdpClient _udp;
        private Thread _thread;
        private volatile bool _alive;

        public UdpListener(int port, string reply) { _port = port; _reply = reply; }

        public void Start()
        {
            _alive = true;
            _udp = new UdpClient();
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udp.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
            _thread = new Thread(Loop);
            _thread.IsBackground = true;
            _thread.Start();
        }

        private void Loop()
        {
            while (_alive)
            {
                try
                {
                    IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                    byte[] d = _udp.Receive(ref ep);
                    string s = Encoding.UTF8.GetString(d);
                    if (s == "SCREENSHARE|DISC")
                    {
                        byte[] reply = Encoding.UTF8.GetBytes(_reply);
                        _udp.Send(reply, reply.Length, ep); // 单播回请求者（自动联系）
                    }
                }
                catch { if (_alive) Thread.Sleep(100); }
            }
        }

        public void Stop()
        {
            _alive = false;
            try { if (_udp != null) _udp.Close(); } catch { }
        }
    }

    /// <summary>捕获线程：GDI CopyFromScreen（纯 BitBlt，无色彩转换）→ 编码 → 回调</summary>
    internal sealed class CaptureThread
    {
        private readonly Action<byte, int, int, byte[]> _onFrame;
        private Thread _thread;
        private volatile bool _alive;

        public CaptureThread(Action<byte, int, int, byte[]> onFrame) { _onFrame = onFrame; }

        public void Start()
        {
            _alive = true;
            _thread = new Thread(Loop);
            _thread.IsBackground = true;
            _thread.Name = "screen-capture";
            _thread.Start();
        }

        private static ImageCodecInfo JpegCodec()
        {
            foreach (ImageCodecInfo c in ImageCodecInfo.GetImageEncoders())
                if (c.MimeType == "image/jpeg") return c;
            return null;
        }

        private void Loop()
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            double interval = 1000.0 / (Settings.Fps > 0 ? Settings.Fps : 20);
            bool jpeg = Settings.Format != "png";
            int quality = Settings.Quality < 1 ? 1 : (Settings.Quality > 100 ? 100 : Settings.Quality);
            ImageCodecInfo codec = jpeg ? JpegCodec() : null;
            EncoderParameters eps = null;
            if (jpeg)
            {
                eps = new EncoderParameters(1);
                eps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
            }

            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            sw.Start();
            while (_alive)
            {
                sw.Restart();
                try
                {
                    using (Bitmap bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb))
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                        }
                        using (MemoryStream ms = new MemoryStream(4096 * 1024))
                        {
                            if (jpeg) bmp.Save(ms, codec, eps);
                            else bmp.Save(ms, ImageFormat.Png);
                            byte[] data = ms.ToArray();
                            if (data.Length > 0 && _alive)
                                _onFrame(jpeg ? HostEngine.FmtJpeg : HostEngine.FmtPng, bounds.Width, bounds.Height, data);
                        }
                    }
                }
                catch { /* 捕获偶发失败跳过本帧 */ }

                // 按目标帧率节拍
                long elapsed = sw.ElapsedMilliseconds;
                if (elapsed < interval) Thread.Sleep((int)(interval - elapsed));
            }
            if (eps != null) eps.Dispose();
        }

        public void Stop() { _alive = false; }
    }
}
