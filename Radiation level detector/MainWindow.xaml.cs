using System;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Radiation_level_detector
{
    public class SensorInfo
    {
        public string Name { get; set; }
        public string Coordinates { get; set; }
        public string District { get; set; }
        public double RadiationLevel { get; set; }
        public double DangerPercentage { get; set; }
    }
    public partial class MainWindow : Window
    {
        // =============================================
        // DOIMIY O'ZGARMASLAR
        // =============================================

        // Maksimal radiatsiya qiymati (nSv/h)
        private const double MaxRadiationValue = 1000000.0;

        // Animatsiya davomiyligi (soniyada)
        private const double AnimationDuration = 1.5;

        // Ma'lumotni yangilash oraligi (millisekundlarda)
        private const int DataUpdateIntervalMs = 500;

        // Xabarlarni ko'rsatish vaqti (soniyada)
        private const int NotificationDisplayTimeSec = 3;

        // Serial port ulanish tezligi
        private const int DefaultBaudRate = 9600;

        // Port tanlanmaganligi haqidagi xabar matni
        private const string NoPortSelectedText = "Portga ulanmagan yoki boshqa port tanlangan.";

        // Portlarni tekshirish oraligi (soniyada)
        private const int PortCheckIntervalSec = 2;

        // =============================================
        // UI ELEMENTLARNING JOYLASHUVI
        // =============================================

        // Asosiy va yordamchi ko'rsatgichlarning oynadagi nisbiy joylashuvi
        // (widthRatio, heightRatio) formatida
        private readonly (double width, double height)[] elementPositions = new[]
        {
            (0.55, 0.2),   // Asosiy ko'rsatgich
            (0.54, 0.02),  // 1-ko'rsatgich
            (0.46, 0.08),  // 2-ko'rsatgich
            (0.46, 0.73),   // 3-ko'rsatgich
            (0.523, 0.553)  // 4-ko'rsatgich
        };

        // =============================================
        // DASTUR HOLATINI SAQLASH UCHUN O'ZGARUVCHILAR
        // =============================================

        // Joriy radiatsiya qiymati (0-100% oralig'ida)
        private double currentRadiationValue = 0;

        // Serial port ulanishi uchun obyekt
        private SerialPort serialPort;

        // Xabarlarni vaqtincha ko'rsatish uchun taymer
        private DispatcherTimer notificationTimer;

        // UI ni yangilash uchun taymer
        private DispatcherTimer dataUpdateTimer;

        // Portlarni kuzatish uchun taymer
        private DispatcherTimer portCheckTimer;

        // Portlar ro'yxati yangilanayotganligini bildiradigan flag
        private bool isUpdatingPorts = false;

        // Oxirgi tanlangan port nomi
        private string lastSelectedPort = string.Empty;

        // Oldingi portlar ro'yxati (o'zgarishlarni aniqlash uchun)
        private string[] lastKnownPorts = Array.Empty<string>();



        // =============================================
        // ASOSIY OYNA KONSTRUKTORI
        // =============================================

        public MainWindow()
        {
            // WPF komponentlarini ishga tushirish
            InitializeComponent();

            // Taymerlarni sozlash
            InitializeTimers();

            // Port boshqaruvi tizimini ishga tushirish
            InitializePortManagement();

            // Barcha UI elementlarni yangilash
            UpdateAllUIElements();

            // Boshlang'ich holatda port tanlanmaganligini ko'rsatish
            currentValueText.Text = NoPortSelectedText;
        }

        // =============================================
        // DASTURNI ISHGA TUSHIRISH METODLARI
        // =============================================

        /// <summary>
        /// Barcha kerakli taymerlarni ishga tushirish
        /// </summary>
        private void InitializeTimers()
        {
            InitializeNotificationTimer();
            InitializeDataUpdateTimer();
        }

        /// <summary>
        /// Port boshqaruvi tizimini ishga tushirish
        /// </summary>
        private void InitializePortManagement()
        {
            // Oyna o'lchami o'zgarganda UI elementlarni qayta joylashtirish
            this.SizeChanged += OnWindowSizeChanged;

            // Portlar combobox'ini ishga tushirish
            InitializePortComboBox();

            // Port kuzatuvchisini ishga tushirish
            StartPortWatcher();
        }

        /// <summary>
        /// Xabarlarni ko'rsatish taymerini sozlash
        /// </summary>
        private void InitializeNotificationTimer()
        {
            notificationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(NotificationDisplayTimeSec)
            };
            notificationTimer.Tick += (s, e) =>
            {
                // Belgilangan vaqtdan keyin xabarni yashirish
                NotificationText.Visibility = Visibility.Collapsed;
                notificationTimer.Stop();
            };
        }

        /// <summary>
        /// UI yangilash taymerini ishga tushirish
        /// </summary>
        private void InitializeDataUpdateTimer()
        {
            dataUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DataUpdateIntervalMs)
            };
            dataUpdateTimer.Tick += (s, e) => UpdateAllUIElements();
            dataUpdateTimer.Start();
        }

        /// <summary>
        /// Portlar ro'yxatini boshlang'ich holatga keltirish
        /// </summary>
        private void InitializePortComboBox()
        {
            isUpdatingPorts = true;
            portComboBox.Items.Clear();
            portComboBox.Items.Add("Tanlang...");
            portComboBox.SelectedIndex = 0;
            RefreshPortList();
            isUpdatingPorts = false;
        }

        // =============================================
        // PORTLARNI BOSHQARISH METODLARI
        // =============================================

        /// <summary>
        /// Portlarni avtomatik kuzatish tizimini ishga tushirish
        /// </summary>
        private void StartPortWatcher()
        {
            try
            {
                // Faqat Windows tizimida ishlashini tekshirish
                if (!OperatingSystem.IsWindows())
                {
                    Debug.WriteLine("Port kuzatuvchisi faqat Windowsda ishlaydi");
                    return;
                }

                // Portlarni muntazam tekshirish uchun taymer
                portCheckTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(PortCheckIntervalSec)
                };

                // Boshlang'ich portlar ro'yxatini saqlash
                lastKnownPorts = SerialPort.GetPortNames();

                // Taymer ishga tushganda bajariladigan amallar
                portCheckTimer.Tick += (s, e) =>
                {
                    // Hozirgi mavjud portlarni olish
                    var currentPorts = SerialPort.GetPortNames();

                    // Yangi qo'shilgan portlarni aniqlash
                    var added = currentPorts.Except(lastKnownPorts);
                    foreach (var port in added)
                    {
                        // UI thread'ida xabar ko'rsatish va ro'yxatni yangilash
                        Dispatcher.Invoke(() => ShowNotification($"Yangi port qo'shildi: {port}"));
                        Dispatcher.Invoke(RefreshPortList);
                    }

                    // O'chirilgan portlarni aniqlash
                    var removed = lastKnownPorts.Except(currentPorts);
                    foreach (var port in removed)
                    {
                        // UI thread'ida xabar ko'rsatish va ro'yxatni yangilash
                        Dispatcher.Invoke(() => ShowNotification($"Port o'chirildi: {port}"));
                        Dispatcher.Invoke(RefreshPortList);
                    }

                    // Portlar ro'yxatini yangilash
                    lastKnownPorts = currentPorts;
                };

                // Taymerni ishga tushirish
                portCheckTimer.Start();
                Debug.WriteLine("Port kuzatuvchisi muvaffaqiyatli ishga tushirildi.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Port kuzatuvchisini ishga tushirishda xato: {ex.Message}");
            }
        }

        /// <summary>
        /// Portlar ro'yxatini yangilash
        /// </summary>
        private void RefreshPortList()
        {
            // Faqat Windows tizimida ishlashini tekshirish
            if (!OperatingSystem.IsWindows())
            {
                ShowNotification("Serial portlar faqat Windowsda ishlaydi");
                return;
            }

            try
            {
                isUpdatingPorts = true;

                // Mavjud portlarni olish
                string[] ports = SerialPort.GetPortNames();

                // Oldingi tanlovni saqlab qolish
                string currentSelection = portComboBox.SelectedItem?.ToString();

                // Ro'yxatni tozalash va boshlang'ich elementni qo'shish
                portComboBox.Items.Clear();
                portComboBox.Items.Add("Tanlang...");

                // Mavjud portlarni ro'yxatga qo'shish
                foreach (string port in ports)
                {
                    portComboBox.Items.Add(port);
                }

                // Agar oldin tanlangan port hali mavjud bo'lsa, uni tanlab qo'yish
                if (!string.IsNullOrEmpty(lastSelectedPort) && ports.Contains(lastSelectedPort))
                {
                    portComboBox.SelectedItem = lastSelectedPort;
                }
                // Yoki avvalgi tanlov hali mavjud bo'lsa
                else if (!string.IsNullOrEmpty(currentSelection) && ports.Contains(currentSelection))
                {
                    portComboBox.SelectedItem = currentSelection;
                }
                // Aks holda boshlang'ich holatga o'tkazish
                else
                {
                    portComboBox.SelectedIndex = 0;
                    currentValueText.Text = NoPortSelectedText;
                }

                Debug.WriteLine($"Portlar ro'yxati yangilandi. Jami {ports.Length} ta port.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Portlarni yangilashda xato: {ex.Message}");
                ShowNotification($"Portlarni yangilashda xato: {ex.Message}");
            }
            finally
            {
                isUpdatingPorts = false;
            }
        }

        /// <summary>
        /// Port tanlanganda yoki o'zgartirilganda ishlaydigan metod
        /// </summary>
        private void portComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Agar portlar yangilanayotgan bo'lsa, hech narsa qilmaslik
            if (isUpdatingPorts) return;

            // Agar "Tanlang..." tanlangan bo'lsa
            if (portComboBox.SelectedIndex == 0)
            {
                // Agar oldin port tanlangan bo'lsa, xabar ko'rsatish
                if (!string.IsNullOrEmpty(lastSelectedPort))
                {
                    ShowNotification($"{lastSelectedPort} portidan chiqildi");
                    lastSelectedPort = string.Empty;
                }

                // Portni yopish va xabar ko'rsatish
                ClosePort();
                currentValueText.Text = NoPortSelectedText;
                return;
            }

            // Yangi port tanlangan bo'lsa
            if (portComboBox.SelectedItem != null)
            {
                lastSelectedPort = portComboBox.SelectedItem.ToString();
                OpenPort(lastSelectedPort);
            }
        }

        /// <summary>
        /// Belgilangan portga ulanish
        /// </summary>
        /// <param name="portName">Ulanish uchun port nomi</param>
        private void OpenPort(string portName)
        {
            // Avvalgi ulanishni yopish
            ClosePort();

            try
            {
                // Yangi serial port obyektini yaratish
                serialPort = new SerialPort(portName, DefaultBaudRate);

                // Ma'lumot kelganda ishlaydigan metodni ulash
                serialPort.DataReceived += SerialPort_DataReceived;

                // Xato yuz berganda ishlaydigan metod
                serialPort.ErrorReceived += (s, e) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ShowNotification($"{portName} portida xato yuz berdi");
                        currentValueText.Text = NoPortSelectedText;
                    });
                };

                // Portni ochish
                serialPort.Open();
                ShowNotification($"{portName} porti muvaffaqiyatli ulandi!");
            }
            catch (Exception ex)
            {
                // Xato yuz berganda xabar ko'rsatish va boshlang'ich holatga qaytarish
                ShowNotification($"Portni ochishda xatolik: {ex.Message}");
                portComboBox.SelectedIndex = 0;
                currentValueText.Text = NoPortSelectedText;
            }
        }

        /// <summary>
        /// Portdan ma'lumot kelganda ishlaydigan metod
        /// </summary>
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (serialPort == null || !serialPort.IsOpen) return;

                string data = serialPort.ReadLine().Trim();

                if (double.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out double radiationValue))
                {
                    Dispatcher.Invoke(() =>
                    {
                        // 2 ta kasr bilan hisoblash
                        double percentage = Math.Round((radiationValue / MaxRadiationValue) * 100.0, 2);
                        currentValueText.Text = $"{radiationValue:F2} nSv/h";
                        currentRadiationValue = percentage; // Butun songa yaxlitlamasdan saqlash

                        // UI ni yangilash
                        UpdateAllUIElements();
                    });
                }
                else
                {
                    Dispatcher.Invoke(() => {
                        ShowNotification($"Noto'g'ri ma'lumot formati: '{data}'");
                        currentValueText.Text = NoPortSelectedText;
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    ShowNotification($"Ma'lumot o'qishda xato: {ex.Message}");
                    currentValueText.Text = NoPortSelectedText;
                });
            }
        }

        /// <summary>
        /// Portni yopish va resurslarni tozalash
        /// </summary>
        private void ClosePort()
        {
            if (serialPort != null)
            {
                try
                {
                    // Agar port ochiq bo'lsa
                    if (serialPort.IsOpen)
                    {
                        // Hodisalarni olib tashlash va portni yopish
                        serialPort.DataReceived -= SerialPort_DataReceived;
                        serialPort.Close();
                    }

                    // Resurslarni tozalash
                    serialPort.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Portni yopishda xato: {ex.Message}");
                }
                finally
                {
                    serialPort = null;
                }
            }
        }

        // =============================================
        // UI BOSHQARISH METODLARI
        // =============================================

        /// <summary>
        /// Foydalanuvchiga xabar ko'rsatish
        /// </summary>
        /// <param name="message">Ko'rsatiladigan xabar matni</param>
        private void ShowNotification(string message)
        {
            NotificationText.Text = message;
            NotificationText.Visibility = Visibility.Visible;
            notificationTimer.Start();
        }

        /// <summary>
        /// Oyna o'lchami o'zgarganda UI elementlarni qayta joylashtirish
        /// </summary>
        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateAllUIElements();
        }

        /// <summary>
        /// Barcha UI elementlarni yangilash
        /// </summary>
        private void UpdateAllUIElements()
        {
            // Asosiy ko'rsatgichni yangilash
            UpdateUIElement(AnimationGrid, elementPositions[0].width, elementPositions[0].height,
                          Circle, GlowEffect, glowScale, PercentText, currentRadiationValue);

            // Yordamchi ko'rsatgichlarni yangilash (qiymat 0)
            for (int i = 1; i < elementPositions.Length; i++)
            {
                UpdateUIElement(
                    i == 1 ? AnimationGrid1 : i == 2 ? AnimationGrid2 : i == 3 ? AnimationGrid3 : AnimationGrid4,
                    elementPositions[i].width, elementPositions[i].height,
                    i == 1 ? Circle1 : i == 2 ? Circle2 : i == 3 ? Circle3 : Circle4,
                    i == 1 ? GlowEffect1 : i == 2 ? GlowEffect2 : i == 3 ? GlowEffect3 : GlowEffect4,
                    i == 1 ? glowScale1 : i == 2 ? glowScale2 : i == 3 ? glowScale3 : glowScale4,
                    i == 1 ? PercentText1 : i == 2 ? PercentText2 : i == 3 ? PercentText3 : PercentText4,
                    0);
            }
        }

        /// <summary>
        /// Yakka UI elementni yangilash
        /// </summary>
        private void UpdateUIElement(Grid grid, double widthRatio, double heightRatio,
                Ellipse circle, Ellipse glowEffect, ScaleTransform glowScale,
                TextBlock percentText, double value)
        {
            // Element joylashuvini yangilash
            UpdateElementPosition(grid, widthRatio, heightRatio);

            // Ma'lumot kelmayotgan holatni tekshirish
            bool noData = currentValueText.Text == NoPortSelectedText;

            // Ko'rinishini yangilash
            if (grid == AnimationGrid) 
                UpdateElementAppearance(circle, glowEffect, percentText, value,  noData);

            // Animatsiyani boshlash (agar kerak bo'lsa)
            StartGlowAnimationIfNeeded(glowEffect, glowScale, false); //(grid == AnimationGrid) ? noData : false
        }
        /// <summary>
        /// Elementning tashqi ko'rinishini yangilash
        /// </summary>
        private void UpdateElementAppearance(Ellipse circle, Ellipse glowEffect, TextBlock percentText, double value, bool noData)
        {
            if (noData)
            {
                // Ma'lumot kelmayotgan holat
                Color grayColor = Color.FromRgb(100, 100, 100); // Kulrang rang
                circle.Fill = new SolidColorBrush(grayColor);
                glowEffect.Fill = new SolidColorBrush(grayColor);
                percentText.Text = "?";
                percentText.Foreground = Brushes.White; // Qoraroq matn rangi DarkGray
            }
            else
            {
                //ShowNotification($"{glowEffect.Name} ");
                // Normal holat
                Color color = CalculateRadiationColor(value);
                circle.Fill = new SolidColorBrush(color);
                glowEffect.Fill = new SolidColorBrush(color);
                percentText.Text = $"{value:F2}%";
                percentText.Foreground = Brushes.Blue; // Oq matn rangi
            }
        }

        /// <summary>
        /// Yorug'lik animatsiyasini boshlash (agar hali boshlanmagan bo'lsa)
        /// </summary>
        private void StartGlowAnimationIfNeeded(Ellipse glowEffect, ScaleTransform glowScale, bool noData)
        {
            // Agar ma'lumot kelmayotgan bo'lsa, animatsiyani o'chirish
            if (noData)
            {
                glowEffect.BeginAnimation(UIElement.OpacityProperty, null);
                glowScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                glowScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                glowEffect.Opacity = 0.3; // Past shaffoflik
                return;
            }

            // Agar animatsiya allaqachon ishlamayotgan bo'lsa
            if (!glowScale.HasAnimatedProperties)
            {
                // Kattalik animatsiyasi
                var glowAnimation = new DoubleAnimation
                {
                    From = 0.4,
                    To = 1.5,
                    Duration = TimeSpan.FromSeconds(AnimationDuration),
                    RepeatBehavior = RepeatBehavior.Forever
                };

                // Shaffoflik animatsiyasi
                var opacityAnimation = new DoubleAnimation
                {
                    From = 0.5,
                    To = 0.0,
                    Duration = TimeSpan.FromSeconds(AnimationDuration),
                    RepeatBehavior = RepeatBehavior.Forever
                };

                // Animatsiyalarni boshlash
                glowScale.BeginAnimation(ScaleTransform.ScaleXProperty, glowAnimation);
                glowScale.BeginAnimation(ScaleTransform.ScaleYProperty, glowAnimation);
                glowEffect.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
            }
        }

        /// <summary>
        /// Elementning oynadagi joylashuvini yangilash
        /// </summary>
        private void UpdateElementPosition(Grid grid, double widthRatio, double heightRatio)
        {
            // Markaz nuqtasini hisoblash
            double centerX = ActualWidth * widthRatio;
            double centerY = ActualHeight * heightRatio;

            // Margin qiymatlarini hisoblash (manfiy bo'lmasligiga ishonch hosil qilish)
            double leftMargin = Math.Max(0, centerX - (grid.ActualWidth / 2));
            double topMargin = Math.Max(0, centerY - (grid.ActualHeight / 2));

            // Joylashuvni o'rnatish
            grid.Margin = new Thickness(leftMargin, topMargin, 0, 0);
            grid.HorizontalAlignment = HorizontalAlignment.Left;
            grid.VerticalAlignment = VerticalAlignment.Top;
        }

        /// <summary>
        /// Elementning tashqi ko'rinishini yangilash
        /// </summary>
        private void UpdateElementAppearance(Ellipse circle, Ellipse glowEffect, TextBlock percentText, double value)
        {
            // Radiatsiya darajasiga qarab rangni hisoblash
            Color color = CalculateRadiationColor(value);

            // Elementlarga rang berish
            circle.Fill = new SolidColorBrush(color);
            glowEffect.Fill = new SolidColorBrush(color);

            // Foiz qiymatini ko'rsatish
            percentText.Text = $"{value}%";
        }

        /// <summary>
        /// Radiatsiya darajasiga qarab rangni hisoblash
        /// </summary>
        /// <returns>
        /// Qizil (yuqori daraja) -> Sariq (o'rta) -> Yashil (past daraja)
        /// </returns>
        private Color CalculateRadiationColor(double value)
        {
            value = (value >=0 && value <= 100) ? value : 100;
            // Qizil komponent (yuqori darajada maksimal)
            byte r = (byte)((value > 50) ? 255 : 255 * value / 50);

            // Yashil komponent (past darajada maksimal)
            byte g = (byte)((value <= 50) ? 255 : 255 * (100 - value) / 50);

            // Ko'k komponent ishlatilmaydi
            return Color.FromRgb(r, g, 0);
        }

        /// <summary>
        /// Yorug'lik animatsiyasini boshlash (agar hali boshlanmagan bo'lsa)
        /// </summary>
        private void StartGlowAnimationIfNeeded(Ellipse glowEffect, ScaleTransform glowScale)
        {
            // Agar animatsiya allaqachon ishlamayotgan bo'lsa
            if (!glowScale.HasAnimatedProperties)
            {
                // Kattalik animatsiyasi
                var glowAnimation = new DoubleAnimation
                {
                    From = 0.4,
                    To = 1.5,
                    Duration = TimeSpan.FromSeconds(AnimationDuration),
                    RepeatBehavior = RepeatBehavior.Forever
                };

                // Shaffoflik animatsiyasi
                var opacityAnimation = new DoubleAnimation
                {
                    From = 0.5,
                    To = 0.0,
                    Duration = TimeSpan.FromSeconds(AnimationDuration),
                    RepeatBehavior = RepeatBehavior.Forever
                };

                // Animatsiyalarni boshlash
                glowScale.BeginAnimation(ScaleTransform.ScaleXProperty, glowAnimation);
                glowScale.BeginAnimation(ScaleTransform.ScaleYProperty, glowAnimation);
                glowEffect.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
            }
        }

        // =============================================
        // RESURSLARNI TOZALASH
        // =============================================

        /// <summary>
        /// Oyna yopilganda resurslarni tozalash
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            CleanupResources();
        }

        /// <summary>
        /// Barcha resurslarni tozalash
        /// </summary>
        private void CleanupResources()
        {
            // Portni yopish
            ClosePort();

            // Ma'lumot yangilash taymerini to'xtatish
            if (dataUpdateTimer != null)
            {
                dataUpdateTimer.Stop();
                dataUpdateTimer = null;
            }

            // Port kuzatish taymerini to'xtatish
            if (portCheckTimer != null)
            {
                portCheckTimer.Stop();
                portCheckTimer = null;
            }
        }
    }
}