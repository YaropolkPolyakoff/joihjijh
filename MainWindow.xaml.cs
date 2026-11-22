using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DanceApp;

public partial class MainWindow : Window
{
    // Video playback state
    private bool _isPlaying;
    private bool _isSeeking;
    private double _currentSpeed = 1.0;
    private readonly DispatcherTimer _progressTimer;

    // Geometry strings for play/pause button icon
    private static readonly string IconPlay  = "M8,5.14V19.14L19,12.14L8,5.14Z";
    private static readonly string IconPause = "M14,19H18V5H14M6,19H10V5H6V19Z";

    // Camera orbit parameters
    private static readonly double CamDist  = 7.0;
    private static readonly double CamElev  = 2.0;
    private static readonly double CamPitch = -0.3;

    public MainWindow()
    {
        InitializeComponent();
        _progressTimer       = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _progressTimer.Tick += ProgressTimer_Tick;
    }

    // ─── Navigation ────────────────────────────────────────────────────────

    private void HideAllPanels()
    {
        PanelCatalog.Visibility = Visibility.Collapsed;
        PanelVideo.Visibility   = Visibility.Collapsed;
        Panel3D.Visibility      = Visibility.Collapsed;
    }

    private void BtnCatalog_Click(object sender, RoutedEventArgs e)
    {
        HideAllPanels();
        PanelCatalog.Visibility = Visibility.Visible;
    }

    private void BtnVideo_Click(object sender, RoutedEventArgs e)
    {
        HideAllPanels();
        PanelVideo.Visibility = Visibility.Visible;
    }

    private void Btn3D_Click(object sender, RoutedEventArgs e)
    {
        HideAllPanels();
        Panel3D.Visibility = Visibility.Visible;
    }

    // ─── Video: open file ──────────────────────────────────────────────────

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title  = "Выберите видеофайл",
            Filter = "Видео|*.mp4;*.avi;*.wmv;*.mkv|Все файлы|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        MediaPlayer.Source     = new Uri(dialog.FileName);
        MediaPlayer.SpeedRatio = _currentSpeed;
        MediaPlayer.Play();
        UpdatePlaybackState(true);
    }

    // ─── Video: play / pause ───────────────────────────────────────────────

    private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (MediaPlayer.Source is null)
            return;

        if (_isPlaying)
        {
            MediaPlayer.Pause();
            UpdatePlaybackState(false);
        }
        else
        {
            MediaPlayer.SpeedRatio = _currentSpeed;
            MediaPlayer.Play();
            UpdatePlaybackState(true);
        }
    }

    private void UpdatePlaybackState(bool nowPlaying)
    {
        _isPlaying         = nowPlaying;
        PlayPauseIcon.Data = Geometry.Parse(nowPlaying ? IconPause : IconPlay);
        if (nowPlaying) _progressTimer.Start();
        else            _progressTimer.Stop();
    }

    // ─── Video: speed  (cycles 1.0 -> 0.5 -> 2.0 -> 1.0) ─────────────────

    private void BtnSpeed_Click(object sender, RoutedEventArgs e)
    {
        if (Math.Abs(_currentSpeed - 1.0) < 0.01)
            _currentSpeed = 0.5;
        else if (Math.Abs(_currentSpeed - 0.5) < 0.01)
            _currentSpeed = 2.0;
        else
            _currentSpeed = 1.0;

        if (Math.Abs(_currentSpeed - 0.5) < 0.01)
            TxtSpeed.Text = "0.5x";
        else if (Math.Abs(_currentSpeed - 2.0) < 0.01)
            TxtSpeed.Text = "2.0x";
        else
            TxtSpeed.Text = "1.0x";

        if (_isPlaying)
            MediaPlayer.SpeedRatio = _currentSpeed;
    }

    // ─── Video: mirror ─────────────────────────────────────────────────────

    private void ChkMirror_Changed(object sender, RoutedEventArgs e)
        => VideoMirrorScale.ScaleX = ChkMirror.IsChecked == true ? -1.0 : 1.0;

    // ─── Video: progress slider ────────────────────────────────────────────

    private void ProgressTimer_Tick(object? sender, EventArgs e)
    {
        if (_isSeeking || !MediaPlayer.NaturalDuration.HasTimeSpan)
            return;

        double total = MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
        if (total <= 0)
            return;

        // Detach listener to avoid feedback loop while updating value
        SliderProgress.ValueChanged -= SliderProgress_ValueChanged;
        SliderProgress.Value         = MediaPlayer.Position.TotalSeconds / total * 100.0;
        SliderProgress.ValueChanged += SliderProgress_ValueChanged;
    }

    private void SliderProgress_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        => _isSeeking = true;

    private void SliderProgress_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isSeeking = false;
        PerformSeek();
    }

    private void SliderProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSeeking)
            PerformSeek();
    }

    private void PerformSeek()
    {
        if (!MediaPlayer.NaturalDuration.HasTimeSpan)
            return;

        double total = MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
        MediaPlayer.Position = TimeSpan.FromSeconds(SliderProgress.Value / 100.0 * total);
    }

    private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
        => SliderProgress.Value = 0;

    private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        UpdatePlaybackState(false);
        MediaPlayer.Position = TimeSpan.Zero;
        SliderProgress.Value = 0;
    }

    // ─── 3D: camera orbit via slider ───────────────────────────────────────

    private void SliderCamera_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        double angleDeg = e.NewValue;
        TxtCameraAngle.Text = $"{(int)angleDeg}\u00b0";

        double rad  = angleDeg * Math.PI / 180.0;
        double posX = CamDist * Math.Sin(rad);
        double posZ = CamDist * Math.Cos(rad);

        Cam3D.Position      = new Point3D(posX, CamElev, posZ);
        Cam3D.LookDirection = new Vector3D(-posX, CamPitch, -posZ);
    }
}
