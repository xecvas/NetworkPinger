using System.Windows.Media;

namespace Network_Pinger.Models
{
    // Encapsulates all parameters required for a ping sequence
    public class PingConfig
    {
        // Target and timing
        public string Target { get; set; }
        public int Timeout { get; set; }
        public int BufferSize { get; set; }
        public int Count { get; set; }
        public int DelayMilliseconds { get; set; }

        // Ping options
        public bool DontFragment { get; set; }
        public bool PlaySound { get; set; }

        // Latency thresholds (ms)
        public int WarningThresholdMs { get; set; }
        public int RtoThresholdMs { get; set; }

        // Row colors per status
        public SolidColorBrush ColorNormal { get; set; }
        public SolidColorBrush ColorWarning { get; set; }
        public SolidColorBrush ColorRto { get; set; }
        public SolidColorBrush ColorInfo { get; set; }
    }
}