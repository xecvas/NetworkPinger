using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Network_Pinger.Models;

namespace Network_Pinger.Utilities
{
    // Handles async CSV export of ping results
    public static class ExportHelper
    {
        public static async Task ExportToCsvAsync(string filePath, IEnumerable<PingResultModel> results)
        {
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                await writer.WriteLineAsync("Date,Timestamp,Target IP,Bytes,Latency (ms),TTL,Status").ConfigureAwait(false);

                foreach (var item in results)
                    await writer.WriteLineAsync($"{item.TestDate},{item.TimeStamp},{item.IPAddress},{item.Bytes},{item.Latency},{item.TTL},{item.Status}").ConfigureAwait(false);
            }
        }
    }
}