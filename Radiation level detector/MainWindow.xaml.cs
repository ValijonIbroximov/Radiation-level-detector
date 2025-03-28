using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Radiation_level_detector
{
    public partial class MainWindow : Window
    {
        private int currentValue = 1;
        private SerialPort serialPort;
        private DispatcherTimer notificationTimer;
        
        // Position coefficients
        double w = 0.55, h = 0.2;
        double w1 = 0.54, h1 = 0.02;
        double w2 = 0.46, h2 = 0.08;
        double w3 = 0.46, h3 = 0.73;

        public MainWindow()
        {
            InitializeComponent();
            InitializeSerialPort();
            InitializeNotificationTimer();
            this.SizeChanged += OnWindowSizeChanged;
            UpdateAllUIElements();
        }

        private void InitializeSerialPort()
        {
            try
            {
                // Find Arduino NANO port (typically starts with "COM" and has Arduino in description)
                foreach (string portName in SerialPort.GetPortNames())
                {
                    serialPort = new SerialPort(portName, 9600);
                    try
                    {
                        serialPort.Open();
                        serialPort.DataReceived += SerialPort_DataReceived;
                        ShowNotification("Arduino NANO ulandi!");
                        break;
                    }
                    catch
                    {
                        // Port not available, try next one
                        serialPort = null;
                    }
                }

                if (serialPort == null || !serialPort.IsOpen)
                {
                    ShowNotification("Arduino NANO topilmadi!");
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"Xatolik: {ex.Message}");
            }
        }

        private void InitializeNotificationTimer()
        {
            notificationTimer = new DispatcherTimer();
            notificationTimer.Interval = TimeSpan.FromSeconds(3);
            notificationTimer.Tick += (s, e) => 
            {
                NotificationText.Visibility = Visibility.Collapsed;
                notificationTimer.Stop();
            };
        }

        private void ShowNotification(string message)
        {
            NotificationText.Text = message;
            NotificationText.Visibility = Visibility.Visible;
            notificationTimer.Start();
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = serialPort.ReadLine().Trim();
                if (int.TryParse(data, out int newValue))
                {
                    Dispatcher.Invoke(() => 
                    {
                        currentValue = newValue;
                        UpdateUIvalue(AnimationGrid, w, h, Circle, GlowEffect, glowScale, PercentText, currentValue);
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => ShowNotification($"Ma'lumot o'qishda xato: {ex.Message}"));
            }
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateAllUIElements();
        }

        private void UpdateAllUIElements()
        {
            UpdateUIvalue(AnimationGrid, w, h, Circle, GlowEffect, glowScale, PercentText, currentValue);
            UpdateUIvalue(AnimationGrid1, w1, h1, Circle1, GlowEffect1, glowScale1, PercentText1, 0);
            UpdateUIvalue(AnimationGrid2, w2, h2, Circle2, GlowEffect2, glowScale2, PercentText2, 0);
            UpdateUIvalue(AnimationGrid3, w3, h3, Circle3, GlowEffect3, glowScale3, PercentText3, 0);
        }

        private void UpdateUIvalue(Grid grid, double widthRatio, double heightRatio,
                                Ellipse Circle, Ellipse GlowEffect, ScaleTransform glowScale,
                                TextBlock PercentText, int value)
        {
            // UI update code remains the same as in your original implementation
            double centerX = this.ActualWidth * widthRatio;
            double centerY = this.ActualHeight * heightRatio;

            double leftMargin = centerX - (grid.ActualWidth / 2);
            double topMargin = centerY - (grid.ActualHeight / 2);

            leftMargin = leftMargin < 0 ? 0 : leftMargin;
            topMargin = topMargin < 0 ? 0 : topMargin;

            grid.Margin = new Thickness(leftMargin, topMargin, 0, 0);
            grid.HorizontalAlignment = HorizontalAlignment.Left;
            grid.VerticalAlignment = VerticalAlignment.Top;

            byte r = (byte)((value > 50) ? 255 : 255 * value / 50);
            byte g = (byte)((value <= 50) ? 255 : 255 * (100 - value) / 50);
            byte b = 0;

            Debug.WriteLine($"Rang qiymatlari: R={r}, G={g}, B={b}");

            Color interpolatedColor = Color.FromRgb(r, g, b);
            Circle.Fill = new SolidColorBrush(interpolatedColor);
            GlowEffect.Fill = new SolidColorBrush(interpolatedColor);
            PercentText.Text = $"{value}%";

            double animationDuration = 1.5 - (value / 100.0);
            DoubleAnimation glowAnimation = new DoubleAnimation
            {
                From = 0.4,
                To = 1.5,
                Duration = TimeSpan.FromSeconds(animationDuration),
                RepeatBehavior = RepeatBehavior.Forever
            };

            DoubleAnimation opacityAnimation = new DoubleAnimation
            {
                From = 0.5,
                To = 0.0,
                Duration = TimeSpan.FromSeconds(animationDuration),
                RepeatBehavior = RepeatBehavior.Forever
            };

            glowScale.BeginAnimation(ScaleTransform.ScaleXProperty, glowAnimation);
            glowScale.BeginAnimation(ScaleTransform.ScaleYProperty, glowAnimation);
            GlowEffect.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
            }
            base.OnClosed(e);
        }
    }
}