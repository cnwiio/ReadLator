using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NovelTranslator.Core.Data;
using NovelTranslator.Core.Models;

namespace NovelTranslator.Core.Export;

/// <summary>
/// เซิร์ฟเวอร์ HTTP ขนาดเล็กสำหรับเปิดอ่านนิยายในเบราว์เซอร์มือถือผ่าน Wi-Fi วงเดียวกัน (ไม่ต้องใช้สิทธิ์ Admin)
/// </summary>
public sealed class LocalWifiServer : IDisposable
{
    private readonly NovelRepository _repository;
    private readonly Func<long?>? _getPreferredStoryId;
    private readonly int _preferredPort;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public int Port { get; private set; }
    public bool IsRunning { get; private set; }
    public string LanUrl => $"http://{GetLocalLanIp()}:{Port}/";
    public string LocalUrl => $"http://127.0.0.1:{Port}/";

    public LocalWifiServer(NovelRepository repository, Func<long?>? getPreferredStoryId = null, int preferredPort = 8765)
    {
        _repository = repository;
        _getPreferredStoryId = getPreferredStoryId;
        _preferredPort = preferredPort;
    }

    public void Start()
    {
        if (IsRunning) return;

        try
        {
            _listener = new TcpListener(IPAddress.Any, _preferredPort);
            _listener.Start();
        }
        catch (SocketException)
        {
            _listener?.Stop();
            _listener = new TcpListener(IPAddress.Any, 0);
            _listener.Start();
        }

        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _cts = new CancellationTokenSource();
        IsRunning = true;

        var token = _cts.Token;
        _ = Task.Run(() => AcceptLoopAsync(token), token);
    }

    public void Stop()
    {
        if (!IsRunning && _listener == null) return;
        IsRunning = false;
        try { _cts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }
        _listener = null;
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose() => Stop();

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                break;
            }

            _ = Task.Run(() => HandleClientAsync(client, ct), ct);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            try
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 10000;
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

                string? requestLine = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(requestLine)) return;

                // อ่าน header ที่เหลือจนจบบรรทัดว่าง
                string? headerLine;
                while (!string.IsNullOrEmpty(headerLine = await reader.ReadLineAsync(ct))) { }

                string[] parts = requestLine.Split(' ');
                string rawTarget = parts.Length >= 2 ? parts[1] : "/";

                if (rawTarget.StartsWith("/favicon.ico", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteResponseAsync(stream, "204 No Content", "text/plain", Array.Empty<byte>(), ct);
                    return;
                }

                var (storyIdParam, modeParam) = ParseQuery(rawTarget);
                var stories = await _repository.GetAllStoriesAsync();
                Story? activeStory = storyIdParam.HasValue
                    ? stories.FirstOrDefault(s => s.Id == storyIdParam.Value)
                    : null;

                if (activeStory == null)
                {
                    long? preferredId = _getPreferredStoryId?.Invoke();
                    activeStory = preferredId.HasValue
                        ? stories.FirstOrDefault(s => s.Id == preferredId.Value)
                        : null;
                }
                activeStory ??= stories.FirstOrDefault();

                string html;
                if (activeStory == null)
                {
                    html = "<!DOCTYPE html><html lang=\"th\"><meta charset=\"utf-8\"/><body style=\"font-family:sans-serif;padding:24px\">ยังไม่มีนิยายในระบบ</body></html>";
                }
                else
                {
                    var chapters = await _repository.GetChaptersByStoryAsync(activeStory.Id);
                    ExportContentMode mode = string.Equals(modeParam, "bilingual", StringComparison.OrdinalIgnoreCase)
                        ? ExportContentMode.Bilingual
                        : ExportContentMode.TranslatedOnly;
                    html = MobileExportService.BuildMobileHtml(activeStory, chapters, mode, stories);
                }

                byte[] body = Encoding.UTF8.GetBytes(html);
                await WriteResponseAsync(stream, "200 OK", "text/html; charset=utf-8", body, ct);
            }
            catch
            {
                // ข้ามกรณีไคลเอนต์ตัดการเชื่อมต่อก่อนส่งจบ
            }
        }
    }

    private static (long? StoryId, string? Mode) ParseQuery(string rawTarget)
    {
        int qIdx = rawTarget.IndexOf('?');
        if (qIdx < 0 || qIdx + 1 >= rawTarget.Length) return (null, null);

        long? storyId = null;
        string? mode = null;

        string query = rawTarget[(qIdx + 1)..];
        foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            string key = Uri.UnescapeDataString(pair[..eq]);
            string val = Uri.UnescapeDataString(pair[(eq + 1)..]);

            if (key.Equals("story", StringComparison.OrdinalIgnoreCase) && long.TryParse(val, out long id))
            {
                storyId = id;
            }
            else if (key.Equals("mode", StringComparison.OrdinalIgnoreCase))
            {
                mode = val;
            }
        }

        return (storyId, mode);
    }

    private static async Task WriteResponseAsync(NetworkStream stream, string status, string contentType, byte[] body, CancellationToken ct)
    {
        string headers =
            $"HTTP/1.1 {status}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Cache-Control: no-store\r\n" +
            "Connection: close\r\n\r\n";

        byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
        await stream.WriteAsync(headerBytes, ct);
        if (body.Length > 0)
        {
            await stream.WriteAsync(body, ct);
        }
        await stream.FlushAsync(ct);
    }

    public static string GetLocalLanIp()
    {
        try
        {
            var candidates = new List<(string Ip, int Priority)>();
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

                bool looksVirtual = nic.Name.Contains("vEthernet", StringComparison.OrdinalIgnoreCase)
                    || nic.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
                    || nic.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)
                    || nic.Description.Contains("WSL", StringComparison.OrdinalIgnoreCase);

                foreach (var ua in nic.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ua.Address)) continue;

                    byte[] b = ua.Address.GetAddressBytes();
                    bool isPrivateLan = b[0] == 192 && b[1] == 168
                        || b[0] == 10
                        || (b[0] == 172 && b[1] >= 16 && b[1] <= 31);

                    int priority = (looksVirtual ? 10 : 0) + (isPrivateLan ? 0 : 5) + (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 0 : 1);
                    candidates.Add((ua.Address.ToString(), priority));
                }
            }

            if (candidates.Count > 0)
            {
                return candidates.OrderBy(c => c.Priority).First().Ip;
            }
        }
        catch
        {
            // Fallback
        }

        return "127.0.0.1";
    }
}
