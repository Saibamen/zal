using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using Firebase.Auth.UI;
using Microsoft.Win32;
using SIPSorcery.Net;
using Windows.UI.Notifications;
using Zal.Functions.Models;
using Zal.MajorFunctions;
using Application = System.Windows.Application;

namespace Zal
{
    public partial class AuthorizedPage : Page
    {
        public AuthorizedPage()
        {
            InitializeComponent();
            initialize();
        }

        private async Task initialize()
        {
            var ip = Zal.Backend.HelperFunctions.SpecificFunctions.IpGetter.getIp();
            ipText.Text = $"Your Local Address: {ip}";
            await LocalDatabase.Initialize();
            var user = FirebaseUI.Instance.Client.User;
            userName.Text = $"Welcome, {user.Info.DisplayName}.";
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                checkForUpdates();

            }).Start();
            updateCheckBoxesAsync();
            FrontendGlobalClass.Initialize(webrtcConnectionStateChanged: (sender, state) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (state == RTCPeerConnectionState.connecting)
                        {

                            webrtcConnectionStateIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4caf50"));
                            webrtcConnectionStateText.Text = "Mobile Connected";
                        }
                        else if (state == RTCPeerConnectionState.disconnected)
                        {
                            webrtcConnectionStateText.Text = "Mobile not Connected";
                            webrtcConnectionStateIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e63946"));
                        }

                        else if (state == RTCPeerConnectionState.connecting)
                        {
                            webrtcConnectionStateText.Text = "Mobile Connecting";
                            webrtcConnectionStateIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4caf50"));
                        }
                    });
                }
                ,
                (sender, state) =>
                {
                    if (state == ServerSocketConnectionState.Connecting)

                    {
                        Dispatcher.Invoke(() =>
                        {
                            connectionStateIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#219ebc"));
                            connectionStateText.Text = "Connecting to Server";
                        });

                    }
                    else if (state == ServerSocketConnectionState.Connected)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            connectionStateText.Text = "Connected to Server";
                            connectionStateIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4caf50"));
                        });
                    }
                }

                , async (sender, data) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (GpusList.Items.IsEmpty)
                        {
                            GpusList.Items.Clear();
                            foreach (var gpu in data.gpuData)
                            {
                                GpusList.Items.Add(gpu.name);
                                Logger.Log($"detected gpu:{gpu.name}");
                            }

                            //if primary gpu is not set, set it.
                            if (data.gpuData.Count != 0)
                            {
                                var primaryGpu = LocalDatabase.Instance.readKey("primaryGpu");


                                if (primaryGpu == null)
                                {
                                    Logger.Log($"setting primary gpu to {data.gpuData.First().name}");
                                    LocalDatabase.Instance.writeKey("primaryGpu", data.gpuData.First().name);
                                    GpusList.SelectedItem = data.gpuData.First().name;
                                }
                                else
                                {
                                    //check if the primary gpu exists inside this data, this is a useful check in case of the user changed their gpu
                                    bool doesPrimaryGpuExist = false;
                                    foreach (var gpu in data.gpuData)
                                    {
                                        if (gpu.name == primaryGpu.ToString())
                                        {
                                            doesPrimaryGpuExist = true;
                                            Logger.Log($"detected primary gpu:{gpu.name}");
                                            GpusList.SelectedItem = gpu.name;
                                        }
                                    }

                                    if (doesPrimaryGpuExist == false)
                                    {
                                        Logger.Log($"primary gpu not found, setting this gpu as default:{data.gpuData.First().name}");
                                        //reset primary gpu
                                        LocalDatabase.Instance.writeKey("primaryGpu", data.gpuData.First().name);
                                        GpusList.SelectedItem = data.gpuData.First().name;
                                    }
                                }
                            }
                            // GpusList.SelectedItem = primaryGpu;
                        }
                    });
                }

                );

            Logger.Log("program started");
        }

        private void addStringToListbox(string text)
        {
            Application.Current.Dispatcher.Invoke(new MethodInvoker(delegate
            {
                while (ListBox.Items.Count > 3)
                {
                    ListBox.Items.RemoveAt(ListBox.Items.Count - 1);
                }

                TextBlock block = new TextBlock();
                block.Text = $"{DateTime.Now.ToString("h:mm:ss tt")} - {text}";
                ListBox.Items.Insert(0, block);
            }));
        }

        private async Task checkForUpdates()
        {
            string latestVersion = new WebClient().DownloadString("https://zalapp.com/program-version");
            var currentVersion = System.Windows.Forms.Application.ProductVersion;
            if (latestVersion != currentVersion)
            {
                var dialog = System.Windows.Forms.MessageBox.Show("New update is available! do you want to update?", "Zal", MessageBoxButtons.YesNo);
                if (dialog == DialogResult.Yes)
                {
                    using (WebClient webClient = new WebClient())
                    {
                        try
                        {
                            string fileName = Path.Combine(Path.GetTempPath(), "zal.msi");
                            webClient.DownloadFile("https://zalapp.com/zal.msi", fileName);
                            Console.WriteLine("File downloaded successfully.");

                            Process p = new Process();
                            ProcessStartInfo pi = new ProcessStartInfo();
                            pi.UseShellExecute = true;
                            pi.FileName = fileName;
                            p.StartInfo = pi;
                            p.Start();
                        }
                        catch (Exception ex)
                        {
                            System.Windows.Forms.MessageBox.Show("An error occurred updating Zal: " + ex.Message);
                        }
                    }
                }
            }
        }

        private async Task updateCheckBoxesAsync()
        {
            var runOnStartup = (bool?)LocalDatabase.Instance.readKey("runOnStartup") ?? true;
            //replace false with saved settings
            runAtStartup.IsChecked = runOnStartup;

            RegistryKey rk = Registry.CurrentUser.OpenSubKey
                ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (runOnStartup)
                rk.SetValue("Zal", Process.GetCurrentProcess().MainModule.FileName);
            else
                rk.DeleteValue("Zal", false);
        }

        private void LogoutClicked(object sender, RoutedEventArgs e)
        {
            FirebaseUI.Instance.Client.SignOut();
        }

        private void viewLogClicked(object sender, RoutedEventArgs e)
        {
            Process.Start("notepad.exe", Logger.GetLogFilePath());
        }

        private async void copyBackendData(object sender, RoutedEventArgs e)
        {
            var data = await FrontendGlobalClass.Instance.dataManager.getBackendData();
            System.Windows.Clipboard.SetText(Newtonsoft.Json.JsonConvert.SerializeObject(data));

            var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastImageAndText02);

            // Fill in the text elements
            var stringElements = toastXml.GetElementsByTagName("text");
            stringElements[0].AppendChild(toastXml.CreateTextNode("Backend data copied!"));
            ToastNotificationManager.CreateToastNotifier("Zal").Show(new ToastNotification(toastXml));
        }

        private void copyRawBackendData(object sender, RoutedEventArgs e)
        {
            var data = FrontendGlobalClass.Instance.backend.getEntireComputerData();
            System.Windows.Clipboard.SetText(Newtonsoft.Json.JsonConvert.SerializeObject(data));

            var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastImageAndText02);

            // Fill in the text elements
            var stringElements = toastXml.GetElementsByTagName("text");
            stringElements[0].AppendChild(toastXml.CreateTextNode("raw data copied!"));
            ToastNotificationManager.CreateToastNotifier("Zal").Show(new ToastNotification(toastXml));
        }

        private void minimizeToTray_Click(object sender, RoutedEventArgs e)
        {
            //   Zal.Settings.Default.minimizeToTray = !Zal.Settings.Default.minimizeToTray;
            //   Zal.Settings.Default.Save();
            //   Zal.Settings.Default.Upgrade();
            //   updateCheckBoxes();
        }

        private async void runAtStartup_Click(object sender, RoutedEventArgs e)
        {
            var runOnStartup = (LocalDatabase.Instance.readKey("runOnStartup")) ?? false;
            await LocalDatabase.Instance.writeKey("runOnStartup", runOnStartup);

            var response = ((bool?)(LocalDatabase.Instance.readKey("runOnStartup")) ?? false);
            updateCheckBoxesAsync();
        }

        private async void logFpsData_Click(object sender, RoutedEventArgs e)
        {
            FrontendGlobalClass.Instance.shouldLogFpsData = logFpsData.IsChecked ?? false;
        }

        private async void GpusList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedGpuName = e.AddedItems[0];
            await LocalDatabase.Instance.writeKey("primaryGpu", selectedGpuName.ToString());
        }
    }
}
