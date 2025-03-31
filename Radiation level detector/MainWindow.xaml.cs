using System;
using System.Diagnostics;
using System.Globalization;
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
        private double currentValue = 0;
        private const double MaxRadiationValue = 50.0;
        private SerialPort serialPort;
        private DispatcherTimer notificationTimer;
        private DispatcherTimer dataUpdateTimer;
        // Position coefficients
        double w = 0.55, h = 0.2;
        double w1 = 0.54, h1 = 0.02;
        double w2 = 0.46, h2 = 0.08;
        double w3 = 0.46, h3 = 0.73;

        public MainWindow()
        {
            InitializeComponent();
            InitializeNotificationTimer();
            this.SizeChanged += OnWindowSizeChanged;
            InitializePortComboBox();
            InitializeDataUpdateTimer();
            UpdateAllUIElements();
        }

        private void InitializePortComboBox()
        {
            portComboBox.Items.Clear();
            // "Tanlang..." variantini qo'shamiz
            portComboBox.Items.Add("Tanlang...");
            portComboBox.SelectedIndex = 0; // Dastlab "Tanlang..." tanlangan bo'ladi

            // Portlarni qo'shamiz
            RefreshPortList();
        }


        private void InitializeDataUpdateTimer()
        {
            dataUpdateTimer = new DispatcherTimer();
            dataUpdateTimer.Interval = TimeSpan.FromMilliseconds(500);
            dataUpdateTimer.Tick += (s, e) => UpdateAllUIElements();
            dataUpdateTimer.Start();
        }

        private void RefreshPortList()
        {
            // Platformani tekshirish
            if (!OperatingSystem.IsWindows())
            {
                ShowNotification("Serial portlar faqat Windowsda ishlaydi");
                return;
            }

            try
            {
                // Portlarni olish
                string[] ports = SerialPort.GetPortNames();

                // Portlarni ComboBoxga qo'shish
                foreach (string port in ports)
                {
                    portComboBox.Items.Add(port);
                    Debug.WriteLine($"Topilgan port: {port}");
                }

                if (ports.Length > 0)
                {
                    ShowNotification($"{ports.Length} ta port topildi");
                }
                else
                {
                    ShowNotification("Hech qanday serial port topilmadi");
                }
            }
            catch (PlatformNotSupportedException ex)
            {
                ShowNotification($"Xato: {ex.Message}");
                Debug.WriteLine($"PlatformNotSupportedException: {ex}");
            }
            catch (Exception ex)
            {
                ShowNotification($"Portlarni olishda xato: {ex.Message}");
                Debug.WriteLine($"Exception: {ex}");
            }
        }

        private void portComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (portComboBox.SelectedIndex == 0) // "Tanlang..." tanlangan
            {
                ClosePort();
                ShowNotification("Port tanlanmadi");
                return;
            }

            if (portComboBox.SelectedItem != null)
            {
                OpenPort(portComboBox.SelectedItem.ToString());
            }
        }

        private void OpenPort(string portName)
        {
            ClosePort();
            try
            {
                serialPort = new SerialPort(portName, 9600);
                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.Open();
                ShowNotification($"{portName} porti muvaffaqiyatli ulandi!");
            }
            catch (Exception ex)
            {
                ShowNotification($"Portni ochishda xatolik: {ex.Message}");
                portComboBox.SelectedIndex = 0; // "Tanlang..." ga qaytamiz
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (serialPort == null || !serialPort.IsOpen)
                {
                    return; // Agar port yopilgan bo'lsa, hech narsa qilmaymiz
                }

                string data = serialPort.ReadLine().Trim();
                //Dispatcher.Invoke(() => ShowNotification($"Ma'lumot: {data}")); // Ma'lumotni ko'rsatish

                // Double qiymatni o'qish
                if (double.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out double radiationValue))
                {
                    Dispatcher.Invoke(() =>
                    {
                        double percentage = (radiationValue / MaxRadiationValue) * 100;

                        percentage = Math.Max(0, Math.Min(percentage, 100));

                        currentValueText.Text = $"{radiationValue.ToString()} nSv/h"; // 3 ta kasr joyi qo‘shildi
                        currentValue = (int)Math.Round(percentage);
                    });
                }
                else
                {
                    Dispatcher.Invoke(() => ShowNotification($"Noto'g'ri ma'lumot formati: '{data}'"));
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => ShowNotification($"Ma'lumot o'qishda xato: {ex.Message}"));
            }
        }

        private void ClosePort()
        {
            if (serialPort != null)
            {
                if (serialPort.IsOpen)
                {
                    serialPort.DataReceived -= SerialPort_DataReceived;
                    serialPort.Close();
                }
                serialPort.Dispose();
                serialPort = null;
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
                        TextBlock PercentText, double value)
        {
            // Joylashuvni yangilash
            double centerX = this.ActualWidth * widthRatio;
            double centerY = this.ActualHeight * heightRatio;

            double leftMargin = centerX - (grid.ActualWidth / 2);
            double topMargin = centerY - (grid.ActualHeight / 2);

            leftMargin = leftMargin < 0 ? 0 : leftMargin;
            topMargin = topMargin < 0 ? 0 : topMargin;

            grid.Margin = new Thickness(leftMargin, topMargin, 0, 0);
            grid.HorizontalAlignment = HorizontalAlignment.Left;
            grid.VerticalAlignment = VerticalAlignment.Top;

            // Rangni yangilash (qiymatga qarab)
            byte r = (byte)((value > 50) ? 255 : 255 * value / 50);
            byte g = (byte)((value <= 50) ? 255 : 255 * (100 - value) / 50);
            byte b = 0;

            Color interpolatedColor = Color.FromRgb(r, g, b);
            Circle.Fill = new SolidColorBrush(interpolatedColor);
            GlowEffect.Fill = new SolidColorBrush(interpolatedColor);
            PercentText.Text = $"{value}%";

            // Animatsiya parametrlari (doimiy qoldirilgan)
            const double animationDuration = 1.5; // Doimiy 1 soniya

            // Agar animatsiya allaqachon ishlamayotgan bo'lsa, boshlash
            if (!glowScale.HasAnimatedProperties)
            {
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
        }

        protected override void OnClosed(EventArgs e)
        {
            ClosePort();
            if (dataUpdateTimer != null)
            {
                dataUpdateTimer.Stop();
                dataUpdateTimer = null;
            }
            base.OnClosed(e);
        }
    }
}