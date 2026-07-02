using System;
using System.Diagnostics;
using System.Media;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Network_Pinger.Models;

namespace Network_Pinger.Services
{
    // Core engine for asynchronous ICMP ping operations
    public class PingEngine
    {
        private const string DateFormat = "dddd yyyy-MM-dd";

        // Entry point: resolves host then runs the ping loop
        public async Task StartPingingAsync(PingConfig config, IProgress<PingResultModel> progress, CancellationToken token)
        {
            IPAddress resolvedIp = await ResolveIpAsync(config.Target).ConfigureAwait(false);

            if (resolvedIp == null)
            {
                progress.Report(CreateErrorResult(config.Target, "Failed", config.ColorRto));
                progress.Report(CreateInfoResult("HOST RESOLUTION FAILED", config.ColorRto));
                if (config.PlaySound) SystemSounds.Exclamation.Play();
                return;
            }

            using (Ping pingSender = new Ping())
            {
                byte[] bufferData = new byte[config.BufferSize];
                new Random().NextBytes(bufferData);
                PingOptions options = new PingOptions { DontFragment = config.DontFragment, Ttl = 128 };

                int iteration = 0;
                bool isUnlimited = config.Count <= 0;

                while ((isUnlimited || iteration < config.Count) && !token.IsCancellationRequested)
                {
                    try
                    {
                        PingReply reply = await pingSender.SendPingAsync(resolvedIp, config.Timeout, bufferData, options).ConfigureAwait(false);
                        if (token.IsCancellationRequested) break;

                        ProcessReply(reply, config, progress);
                        iteration++;

                        if ((isUnlimited || iteration < config.Count) && !token.IsCancellationRequested)
                            await Task.Delay(config.DelayMilliseconds, token).ConfigureAwait(false);
                    }
                    catch (PingException)
                    {
                        if (!token.IsCancellationRequested)
                        {
                            progress.Report(CreateErrorResult(resolvedIp.ToString(), "Error", config.ColorRto));
                            if (config.PlaySound) SystemSounds.Exclamation.Play();
                        }
                    }
                    catch (OperationCanceledException) { break; }
                }

                if (config.Count > 0 && !token.IsCancellationRequested)
                    progress.Report(CreateInfoResult($"Ping completed for {config.Count} packets.", config.ColorInfo));
            }
        }

        // Resolve hostname or IP string to an IPAddress
        private async Task<IPAddress> ResolveIpAsync(string hostname)
        {
            try
            {
                if (IPAddress.TryParse(hostname, out IPAddress address)) return address;
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(hostname).ConfigureAwait(false);
                return addresses.Length > 0 ? addresses[0] : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DNS] Resolution error: {ex.Message}");
                return null;
            }
        }

        // Evaluate reply status and report a typed result row
        private void ProcessReply(PingReply reply, PingConfig config, IProgress<PingResultModel> progress)
        {
            string ip = reply.Address?.ToString() ?? "Unknown";

            if (reply.Status == IPStatus.Success)
            {
                SolidColorBrush rowColor = config.ColorNormal;
                if (reply.RoundtripTime >= config.RtoThresholdMs)
                {
                    rowColor = config.ColorRto;
                    if (config.PlaySound) SystemSounds.Hand.Play();
                }
                else if (reply.RoundtripTime >= config.WarningThresholdMs)
                    rowColor = config.ColorWarning;

                DateTime now = DateTime.Now;
                progress.Report(new PingResultModel
                {
                    TestDate = now.ToString(DateFormat),
                    TimeStamp = now.ToString("HH:mm:ss"),
                    IPAddress = ip,
                    Bytes = reply.Buffer.Length.ToString(),
                    Latency = reply.RoundtripTime.ToString(),
                    TTL = reply.Options?.Ttl.ToString() ?? "0",
                    Status = "Success",
                    RowColor = rowColor
                });
            }
            else
            {
                if (config.PlaySound) SystemSounds.Exclamation.Play();
                progress.Report(CreateErrorResult(ip, reply.Status == IPStatus.TimedOut ? "RTO" : "Failed", config.ColorRto));
            }
        }

        // Build a standard error result row
        private PingResultModel CreateErrorResult(string ip, string status, SolidColorBrush color)
        {
            DateTime now = DateTime.Now;
            return new PingResultModel { TestDate = now.ToString(DateFormat), TimeStamp = now.ToString("HH:mm:ss"), IPAddress = ip, Bytes = "-", Latency = "-", TTL = "-", Status = status, RowColor = color };
        }

        // Build a banner info row (no data fields)
        private PingResultModel CreateInfoResult(string message, SolidColorBrush color) =>
            new PingResultModel { TestDate = string.Empty, TimeStamp = string.Empty, IPAddress = string.Empty, Bytes = string.Empty, Latency = string.Empty, TTL = string.Empty, Status = message, RowColor = color, IsInfoRow = true };
    }
}