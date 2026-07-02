using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Network_Pinger.Models;
using Network_Pinger.Services;
using Network_Pinger.Utilities;

namespace Network_Pinger
{
    public partial class MainWindow : Window
    {
        // --- Fields ---
        private AppSettings _settings;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly ObservableCollection<PingResultModel> _pingResults;
        private readonly PingEngine _pingEngine;

        private readonly SolidColorBrush _infoBrush = ThemeHelper.GetBrush("#17A2B8");
        private readonly SolidColorBrush _stopBrush = ThemeHelper.GetBrush("#DC3545");

        private long _totalLatency;
        private int _successfulPings;

        public MainWindow()
        {
            InitializeComponent();
            _pingResults = new ObservableCollection<PingResultModel>();
            _pingEngine = new PingEngine();
            dgOutput.ItemsSource = _pingResults;

            LoadSettings();
        }

        // --- Settings persistence ---
        private void LoadSettings()
        {
            _settings = AppSettings.Load();
            ApplySettingsToUI();
        }

        private void ApplySettingsToUI()
        {
            txtTarget.Text = _settings.TargetAddress;
            txtDelay.Text = _settings.Interval;
            txtCount.Text = _settings.PingCount;
            txtNormalMs.Text = _settings.NormalThreshold;
            txtWarningMs.Text = _settings.WarningThreshold;
            txtRtoMs.Text = _settings.TimeOutThreshold;
            txtBuffer.Text = _settings.BufferSize;
            txtTimeout.Text = _settings.Timeout;
            chkDontFragment.IsChecked = _settings.DontFragment;
            chkSound.IsChecked = _settings.PlaySound;

            btnNormalColor.Background = ThemeHelper.GetBrush(_settings.SuccessColor);
            btnWarningColor.Background = ThemeHelper.GetBrush(_settings.WarningColor);
            btnRtoColor.Background = ThemeHelper.GetBrush(_settings.ErrorColor);

            themeToggle.IsChecked = _settings.IsDarkMode;
            ApplyTheme(_settings.IsDarkMode);
        }

        private void SaveSettingsFromUI()
        {
            _settings.TargetAddress = txtTarget.Text;
            _settings.Interval = txtDelay.Text;
            _settings.PingCount = txtCount.Text;
            _settings.NormalThreshold = txtNormalMs.Text;
            _settings.WarningThreshold = txtWarningMs.Text;
            _settings.TimeOutThreshold = txtRtoMs.Text;
            _settings.BufferSize = txtBuffer.Text;
            _settings.Timeout = txtTimeout.Text;
            _settings.DontFragment = chkDontFragment.IsChecked ?? false;
            _settings.PlaySound = chkSound.IsChecked ?? false;

            if (btnNormalColor.Background is SolidColorBrush normalBrush) _settings.SuccessColor = normalBrush.Color.ToString();
            if (btnWarningColor.Background is SolidColorBrush warningBrush) _settings.WarningColor = warningBrush.Color.ToString();
            if (btnRtoColor.Background is SolidColorBrush rtoBrush) _settings.ErrorColor = rtoBrush.Color.ToString();

            _settings.IsDarkMode = themeToggle.IsChecked ?? false;
            _settings.Save();
        }

        // --- Window chrome ---
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            SaveSettingsFromUI();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            Application.Current.Shutdown();
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            ToggleUIState(true);

            _settings = new AppSettings();
            ApplySettingsToUI();
            SaveSettingsFromUI();
            ResetMetrics();
        }

        // --- Theme ---
        private void ApplyTheme(bool isDark)
        {
            ThemeHelper.ApplyTheme(this.Resources, isDark, true);
            dgOutput.Background = isDark ? ThemeHelper.GetBrush("#1E1E1E") : Brushes.Transparent;
        }

        private void themeToggle_Click(object sender, RoutedEventArgs e) => ApplyTheme(themeToggle.IsChecked == true);

        // --- Input helpers (numeric textboxes, color picker) ---
        private void NumericTextBox_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is TextBox txt && int.TryParse(txt.Text, out int val))
            {
                txt.Text = Math.Max(0, val + (e.Delta > 0 ? 1 : -1)).ToString();
                e.Handled = true;
            }
        }

        private void NumericTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox txt && int.TryParse(txt.Text, out int val))
            {
                if (e.Key == Key.Up) { txt.Text = (val + 1).ToString(); e.Handled = true; }
                else if (e.Key == Key.Down) { txt.Text = Math.Max(0, val - 1).ToString(); e.Handled = true; }
            }
        }

        private void ColorPicker_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Background is SolidColorBrush currentBrush)
            {
                using (var dialog = new System.Windows.Forms.ColorDialog())
                {
                    var c = currentBrush.Color;
                    dialog.Color = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        var dc = dialog.Color;
                        btn.Background = new SolidColorBrush(Color.FromArgb(dc.A, dc.R, dc.G, dc.B));
                    }
                }
            }
        }

        // --- Ping control (start/stop, result handling, metrics) ---
        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            string target = txtTarget.Text.Trim();
            if (string.IsNullOrWhiteSpace(target)) return;

            ResetMetrics();
            txtAverage.Visibility = Visibility.Visible;
            ToggleUIState(false);

            var config = BuildPingConfig(target);
            var mySession = _cancellationTokenSource = new CancellationTokenSource();
            var progress = new Progress<PingResultModel>(OnPingResultReceived);

            try
            {
                await _pingEngine.StartPingingAsync(config, progress, mySession.Token);
            }
            finally
            {
                // Only the still-current session may reset UI state; a stale/cancelled
                // session finishing late must not clobber a newer session that has since started.
                if (ReferenceEquals(_cancellationTokenSource, mySession))
                    ToggleUIState(true);
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            AppendResult(new PingResultModel
            {
                TestDate = string.Empty,
                TimeStamp = string.Empty,
                IPAddress = string.Empty,
                Bytes = string.Empty,
                Latency = string.Empty,
                TTL = string.Empty,
                Status = "PING STOPPED BY USER",
                RowColor = _stopBrush,
                IsInfoRow = true
            });
        }

        private void OnPingResultReceived(PingResultModel result)
        {
            if (!result.IsInfoRow && result.Status == "Success" && long.TryParse(result.Latency, out long latencyMs))
            {
                _totalLatency += latencyMs;
                _successfulPings++;
                txtAverage.Text = $"Average: {_totalLatency / _successfulPings} ms";
            }

            AppendResult(result);
        }

        // Add a result row and scroll it into view
        private void AppendResult(PingResultModel result)
        {
            _pingResults.Add(result);
            if (_pingResults.Count > 0)
                dgOutput.ScrollIntoView(_pingResults[_pingResults.Count - 1]);
        }

        private void ResetMetrics()
        {
            _pingResults.Clear();
            _totalLatency = 0;
            _successfulPings = 0;
            txtAverage.Text = "Average: 0 ms";
            txtAverage.Visibility = Visibility.Collapsed;
        }

        private void ToggleUIState(bool isEnabled)
        {
            btnStart.IsEnabled = isEnabled;
            btnStop.IsEnabled = !isEnabled;
            panelSettings.IsEnabled = isEnabled;
        }

        // --- Config building / parsing ---
        private PingConfig BuildPingConfig(string target) => new PingConfig
        {
            Target = target,
            Timeout = ParseInt(txtTimeout.Text, 4000),
            BufferSize = ParseInt(txtBuffer.Text, 32),
            Count = ParseInt(txtCount.Text, 0),
            DelayMilliseconds = ParseInt(txtDelay.Text, 1) * 1000,
            DontFragment = chkDontFragment.IsChecked == true,
            PlaySound = chkSound.IsChecked == true,
            WarningThresholdMs = ParseInt(txtWarningMs.Text, 200),
            RtoThresholdMs = ParseInt(txtRtoMs.Text, 500),
            ColorNormal = btnNormalColor.Background as SolidColorBrush,
            ColorWarning = btnWarningColor.Background as SolidColorBrush,
            ColorRto = btnRtoColor.Background as SolidColorBrush,
            ColorInfo = _infoBrush
        };

        private int ParseInt(string input, int fallback) => int.TryParse(input, out int result) ? result : fallback;

        // --- Export ---
        private async void btnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_pingResults.Count == 0)
            {
                MessageBox.Show("No data available to export.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"PingLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                Title = "Save Ping Results"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    btnExport.IsEnabled = false;
                    await ExportHelper.ExportToCsvAsync(dialog.FileName, _pingResults);
                    AppendResult(new PingResultModel
                    {
                        TestDate = string.Empty,
                        TimeStamp = string.Empty,
                        IPAddress = string.Empty,
                        Bytes = string.Empty,
                        Latency = string.Empty,
                        TTL = string.Empty,
                        Status = "CSV EXPORT COMPLETED SUCCESSFULLY",
                        RowColor = _infoBrush,
                        IsInfoRow = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    btnExport.IsEnabled = true;
                }
            }
        }
    }
}