#nullable enable

using System;

namespace Transform.App.Services;

public interface IClipboardMonitor
{
    event EventHandler<string>? LcscIdDetected;
    bool IsMonitoring { get; }
    void Start();
    void Stop();
}

public class ClipboardMonitor : IClipboardMonitor, IDisposable
{
    private System.Threading.Timer? _timer;
    private string _lastClipboardContent = string.Empty;
    private bool _disposed = false;

    public event EventHandler<string>? LcscIdDetected;

    public bool IsMonitoring { get; private set; } = false;

    public void Start()
    {
        if (IsMonitoring) return;

        IsMonitoring = true;
        _timer = new System.Threading.Timer(CheckClipboard, null, 0, 500);
    }

    public void Stop()
    {
        IsMonitoring = false;
        _timer?.Dispose();
        _timer = null;
    }

    private void CheckClipboard(object? state)
    {
        try
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                var text = System.Windows.Clipboard.GetText();
                if (text != _lastClipboardContent)
                {
                    _lastClipboardContent = text;
                    var lcscId = ExtractLcscId(text);
                    if (!string.IsNullOrEmpty(lcscId))
                    {
                        LcscIdDetected?.Invoke(this, lcscId);
                    }
                }
            }
        }
        catch { }
    }

    private static string? ExtractLcscId(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, @"C-?\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Value.ToUpperInvariant() : null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
