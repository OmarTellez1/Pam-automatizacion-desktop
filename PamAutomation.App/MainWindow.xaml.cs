using System.Windows;
using System.Windows.Threading;
using PamAutomation.App.ViewModels;

namespace PamAutomation.App
{
    public partial class MainWindow : Window
    {
        internal static readonly TimeSpan RequestTimeRefreshInterval = TimeSpan.FromMinutes(1);
        private readonly DispatcherTimer requestTimeRefreshTimer;

        public MainWindow()
        {
            InitializeComponent();
            requestTimeRefreshTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = RequestTimeRefreshInterval
            };
            requestTimeRefreshTimer.Tick += RefreshRequestTimeState;
            Loaded += StartRequestTimeRefresh;
            Closed += StopRequestTimeRefresh;
        }

        private void StartRequestTimeRefresh(object sender, RoutedEventArgs e)
        {
            (DataContext as MainWindowViewModel)?.RefreshTimeSensitiveState();
            requestTimeRefreshTimer.Start();
        }

        private void StopRequestTimeRefresh(object? sender, EventArgs e)
        {
            requestTimeRefreshTimer.Stop();
            requestTimeRefreshTimer.Tick -= RefreshRequestTimeState;
        }

        private void RefreshRequestTimeState(object? sender, EventArgs e) =>
            (DataContext as MainWindowViewModel)?.RefreshTimeSensitiveState();
    }
}
