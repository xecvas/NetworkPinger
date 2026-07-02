using System.Windows.Media;

namespace Network_Pinger.Models
{
    // Represents a single ping result for UI data-binding
    public class PingResultModel
    {
        // Display data
        public string TestDate { get; set; }
        public string TimeStamp { get; set; }
        public string IPAddress { get; set; }
        public string Bytes { get; set; }
        public string Latency { get; set; }
        public string TTL { get; set; }
        public string Status { get; set; }

        // Row appearance
        public SolidColorBrush RowColor { get; set; }
        public bool IsInfoRow { get; set; }
    }
}