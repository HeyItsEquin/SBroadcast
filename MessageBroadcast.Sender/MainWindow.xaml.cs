using MessageBroadcast.Core;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace MessageBroadcast.Sender
{
    public partial class MainWindow : Window
    {
        private readonly DeviceInfo _localDevice;
        private readonly DeviceDiscovery _discovery;
        private readonly MessageSender _sender;
        private CancellationTokenSource _cts = new();
        private readonly Dictionary<Guid, DeviceInfo> _devices = new();
        private readonly ObservableCollection<DeviceInfo> _deviceList = new();
        private readonly CollectionViewSource _deviceViewSource = new();

        private byte[]? _selectedSoundData;
        private string? _selectedSoundFormat;

        private byte[]? _selectedImageData;

        private bool _isEditingName = false;

        public MainWindow()
        {
            InitializeComponent();
            LoadAppConfig(); // Load and apply application configs
            Icon = IconLoader.LoadIcon() ?? Icon;

            // Start overlay if it isn't running
            EnsureOverlayRunning();

            _localDevice = new DeviceInfo
            {
                Id = DeviceIdentity.LoadOrCreateUuid(),
                Name = Environment.MachineName,
                Port = 41235
            };

            _deviceViewSource.Source = _deviceList;
            _deviceViewSource.SortDescriptions.Add(
                new SortDescription(nameof(DeviceInfo.SortOrder), ListSortDirection.Ascending));
            _deviceViewSource.SortDescriptions.Add(
                new SortDescription(nameof(DeviceInfo.PreferredName), ListSortDirection.Ascending));

            DeviceList.ItemsSource = _deviceViewSource.View;

            FontSizeSlider.ValueChanged += (_, _) => 
                ConfigStore.Instance.UpdateAppConfig(nameof(AppConfig.DefaultFontSize), (int)FontSizeSlider.Value);
            DisplayTimeSlider.ValueChanged += (_, _) =>
                ConfigStore.Instance.UpdateAppConfig(nameof(AppConfig.DefaultDisplaySeconds), (int)DisplayTimeSlider.Value);
            PositionCombo.SelectionChanged += (_, _) =>
                ConfigStore.Instance.UpdateAppConfig(nameof(AppConfig.DefaultPosition), (MessagePosition)PositionCombo.SelectedIndex);
            FontFamilyCombo.SelectionChanged += (_, _) =>
                ConfigStore.Instance.UpdateAppConfig(nameof(AppConfig.DefaultFontFamily), 
                (FontFamilyCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Segoe UI");
            FontColorCombo.SelectionChanged += (_, _) =>
                ConfigStore.Instance.UpdateAppConfig(nameof(AppConfig.DefaultFontColor),
                (FontColorCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "White");

            _sender = new MessageSender();

            _discovery = new DeviceDiscovery(_localDevice);
            _discovery.DeviceDiscovered += OnDeviceDiscovered;
            _discovery.Start();
        }

        // Load and apply application configs
        private void LoadAppConfig()
        {
            var config = ConfigStore.Instance.GetAppConfig();
            FontSizeSlider.Value = (double)config.DefaultFontSize;
            DisplayTimeSlider.Value = (double)config.DefaultDisplaySeconds;
            PositionCombo.SelectedIndex = (int)config.DefaultPosition;
            FontFamilyCombo.SelectedIndex = FindComboIndex(FontFamilyCombo, config.DefaultFontFamily);
            FontColorCombo.SelectedIndex = FindComboIndex(FontColorCombo, config.DefaultFontColor);
        }

        // Re-sort device list when a new device is added
        private void RefreshDeviceOrder()
        {
            _deviceViewSource.View.Refresh();
        }

        // Find index of a combo box item based on Content property
        private int FindComboIndex(ComboBox comboBox, string value)
        {
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                var item = comboBox.Items[i] as ComboBoxItem;
                if (item?.Content?.ToString() == value)
                    return i;
            }
            return 0;
        }

        private void EnsureOverlayRunning()
        {
            try
            {
                var overlayProcessName = "MessageBroadcast.Overlay";
                var existing = Process.GetProcessesByName(overlayProcessName);

                // Overlay process isn't open, start it
                if (existing.Length == 0)
                {
                    Logger.Log("[SND] Overlay process not present, starting...");

                    var overlayPath = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "MessageBroadcast.Overlay.exe");

                    if (File.Exists(overlayPath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = overlayPath,
                            UseShellExecute = false
                        });

                        Logger.Log("[SND] Started overlay process from Sender");
                    }
                    else
                    {
                        MessageBox.Show("[SND] Could not find MessageBroadcast.Overlay.exe. " +
                            "Please make sure it is in the same folder as the Sender.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[SND] Failed to start overlay process: {ex.GetType().Name} - {ex.Message}");
                
                var inner = ex.InnerException;
                if (inner != null)
                    Logger.Log($"[SND] Inner Exception: {inner.GetType().Name} - {ex.Message}");

                MessageBox.Show(
                    $"Failed to start overlay process, please check the logs.",
                    "Unhandled exception",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnDeviceDiscovered(DeviceInfo device)
        {
            Logger.Log($"[SND] OnDeviceDiscovered: {device.Name} at {device.IpAddress}");
            Dispatcher.Invoke(() =>
            {
                device.LoadConfigs();
                if (_devices.TryGetValue(device.Id, out var existing))
                {
                    // If device already in list, update it
                    existing.IpAddress = device.IpAddress; // Use latest IP
                    existing.LastSeen = device.LastSeen;

                    // Add new IP to advertised
                    foreach (var ip in device.AdvertisedIps)
                        if (!existing.AdvertisedIps.Contains(ip))
                            existing.AdvertisedIps.Add(ip);

                    // Update list entry
                    var index = _deviceList.IndexOf(existing);
                    if (index >= 0)
                    {
                        _deviceList.RemoveAt(index);
                        _deviceList.Insert(index, existing);
                    }
                }
                else
                {
                    // New device, add to list
                    _devices[device.Id] = device;
                    _deviceList.Add(device);
                }
            });
        }

        private void DeviceContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (DeviceList.SelectedItem is not DeviceInfo device)
            {
                if (sender is ContextMenu cm) cm.IsOpen = false;
                return;
            }

            FavoriteDeviceMenuItem.Header = device.IsFavorite ? "Unfavorite" : "Favorite";
            BlockDeviceMenuItem.Header = device.Blocked ? "Unblock" : "Block";

            PreferredIpMenuItem.Items.Clear();

            if (device.AdvertisedIps == null || !device.AdvertisedIps.Any())
            {
                PreferredIpMenuItem.Items.Add(new MenuItem
                {
                    Header = "No IPs available",
                    IsEnabled = false
                });
                return;
            }

            // List all advertised addresses for user to select
            foreach (var ip in device.AdvertisedIps)
            {
                var capturedIp = ip;
                var ipItem = new MenuItem
                {
                    Header = ip,
                    IsCheckable = true,
                    IsChecked = ip == device.IpAddress
                };

                ipItem.Click += (_, _) =>
                {
                    foreach (var item in PreferredIpMenuItem.Items.OfType<MenuItem>())
                        item.IsChecked = false;

                    ipItem.IsChecked = true;
                    device.IpAddress = capturedIp;

                    var config = ConfigStore.Instance.GetDeviceConfig(device.Id);
                    config.PreferredIp = capturedIp;
                    ConfigStore.Instance.SetDeviceConfig(device.Id, config);

                    var index = _deviceList.IndexOf(device);
                    if (index >= 0)
                    {
                        _deviceList.RemoveAt(index);
                        _deviceList.Insert(index, device);
                    }

                    DeviceList.SelectedItem = device;
                    Logger.Log($"[SND] Manually set IP for {device.Name} to {capturedIp}");
                };

                PreferredIpMenuItem.Items.Add(ipItem);
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceList.SelectedItem is not DeviceInfo target)
            {
                MessageBox.Show("Please select a device first.");
                return;
            }

            if (target.Blocked)
            {
                MessageBox.Show("You are unable to send messages to a user whom you've blocked.");
                return;
            }

            var text = MessageInput.Text.Trim();
            if (string.IsNullOrEmpty(text) && _selectedSoundData == null && _selectedImageData == null)
            {
                MessageBox.Show("Message must have some content.");
                return;
            }

            var selectedColor = (FontColorCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString() ?? "#FFFFFF";
            var selectedFont = (FontFamilyCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Segoe UI";

            var message = new Message
            {
                SenderId = _localDevice.Id,
                DeviceName = _localDevice.Name,
                Text = text,
                FontSize = (int)FontSizeSlider.Value,
                DisplaySeconds = (int)DisplayTimeSlider.Value,
                Position = (MessagePosition)PositionCombo.SelectedIndex,
                FontFamily = selectedFont,
                FontColor = selectedColor,
                SoundData = _selectedSoundData,
                SoundFormat = _selectedSoundFormat,
                ImageData = _selectedImageData,
                ContentType = GetMessageType()
            };

            var success = await _sender.SendMessageAsync(target, message);
            if (success)
                MessageInput.Text = string.Empty;
            else
                MessageBox.Show($"Failed to send to {target.PreferredName}. They may have gone offline.");
        }

        private MessageContentType GetMessageType()
        {
            var type = MessageContentType.None;

            if (!string.IsNullOrEmpty(MessageInput.Text.Trim())) type |= MessageContentType.Text;
            if (_selectedImageData != null) type |= MessageContentType.Image;
            if (_selectedSoundData != null) type |= MessageContentType.Sound;

            return type;
        }

        private void MessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            // Submit messages via enter like HTML form
            if (e.Key == Key.Enter)
            {
                SendButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                _cts = new CancellationTokenSource();

                // Scan for new devices
                var discovered = await _discovery.ScanOnceAsync(_cts.Token);
                var discoveredIds = discovered.Select(d => d.Id).ToHashSet();

                Dispatcher.Invoke(() =>
                {
                    // Remove any device from list not present in latest scan
                    var toRemove = _devices.Values
                        .Where(d => !discoveredIds.Contains(d.Id))
                        .ToList();

                    foreach (var device in toRemove)
                    {
                        _devices.Remove(device.Id);
                        _deviceList.Remove(device);
                        Debug.WriteLine($"[SND] Removed stale device: {device.Name}");
                    }
                });
            }
            catch (OperationCanceledException) { }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _cts.Cancel();
            _discovery.Dispose();
            Application.Current.Shutdown();
            base.OnClosed(e);
        }

        private void SetNicknameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceList.SelectedItem is not DeviceInfo device) return;

            // Inline text editing, very annoying
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                var container = DeviceList.ItemContainerGenerator
                    .ContainerFromItem(device) as ListViewItem;
                if (container == null) return;

                var nameText = FindVisualChild<TextBlock>(container, "NameText");
                var nameEdit = FindVisualChild<TextBox>(container, "NameEdit");

                if (nameText == null || nameEdit == null) return;

                _isEditingName = true;
                nameText.Visibility = Visibility.Collapsed;
                nameEdit.Visibility = Visibility.Visible;
                nameEdit.Focus();
                nameEdit.SelectAll();
            });
        }

        // Generic helper fn, don't look too hard into it
        private T? FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T element && element.Name == name)
                    return element;

                var result = FindVisualChild<T>(child, name);
                if (result != null)
                    return result;
            }

            return null;
        }

        private void NameEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_isEditingName) return;
            if (sender is not TextBox nameEdit) return;

            if (e.Key == Key.Enter)
            {
                CommitNicknameEdit(nameEdit);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelNicknameEdit(nameEdit);
                e.Handled = true;
            }
        }

        private void NameEdit_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!_isEditingName) return;
            if (sender is not TextBox nameEdit) return;
            CancelNicknameEdit(nameEdit);
        }

        private void CommitNicknameEdit(TextBox nameEdit)
        {
            if (DeviceList.SelectedItem is not DeviceInfo device) return;

            var container = DeviceList.ItemContainerGenerator
                .ContainerFromItem(device) as ListViewItem;
            if (container == null) return;

            var nameText = FindVisualChild<TextBlock>(container, "NameText");
            if (nameText == null) return;

            var nickname = string.IsNullOrWhiteSpace(nameEdit.Text)
                ? null
                : nameEdit.Text.Trim();

            device.PreferredName = nickname;

            var config = ConfigStore.Instance.GetDeviceConfig(device.Id);
            config.Nickname = nickname;
            ConfigStore.Instance.SetDeviceConfig(device.Id, config);

            nameEdit.Visibility = Visibility.Collapsed;
            nameText.Visibility = Visibility.Visible;

            _isEditingName = false;
        }

        private void CancelNicknameEdit(TextBox nameEdit)
        {
            if (DeviceList.SelectedItem is not DeviceInfo device) return;

            var container = DeviceList.ItemContainerGenerator
                .ContainerFromItem(device) as ListViewItem;
            if (container == null) return;

            var nameText = FindVisualChild<TextBlock>(container, "NameText");
            if (nameText == null) return;

            nameEdit.Text = device.PreferredName ?? device.Name;

            nameEdit.Visibility = Visibility.Collapsed;
            nameText.Visibility = Visibility.Visible;

            _isEditingName = false;
        }

        private void FavoriteDeviceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceList.SelectedItem is not DeviceInfo device) return;

            if (device.IsFavorite)
            {
                // Simply unfavorite
                device.IsFavorite = false;
            }
            else if (device.Blocked)
            {
                // Confirm unblocking
                var result = MessageBox.Show(
                    $"Favoriting {device.PreferredName} will unblock them. Do you want to continue?",
                    "Favorite Device",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                device.Blocked = false;
                device.IsFavorite = true;
            }
            else
            {
                // Simply favorite
                device.IsFavorite = true;
            }

            ConfigStore.Instance.UpdateDeviceConfig(device.Id, nameof(DeviceConfig.Blocked), device.Blocked);
            ConfigStore.Instance.UpdateDeviceConfig(device.Id, nameof(DeviceConfig.Favorite), device.IsFavorite);

            RefreshDeviceOrder();
        }

        private void BlockDeviceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceList.SelectedItem is not DeviceInfo device) return;

            if (device.Blocked)
            {
                device.Blocked = false;
            }
            else
            {
                var message = device.IsFavorite
                    ? $"Block {device.PreferredName}? They will be unfavorited and neither of you will be able to message each other."
                    : $"Block {device.PreferredName}? Neither of you will be able to message each other.";

                var result = MessageBox.Show(
                    message,
                    "Block Device",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;

                device.Blocked = true;
                device.IsFavorite = false;
            }

            ConfigStore.Instance.UpdateDeviceConfig(device.Id, nameof(DeviceConfig.Blocked), device.Blocked);
            ConfigStore.Instance.UpdateDeviceConfig(device.Id, nameof(DeviceConfig.Favorite), device.IsFavorite);

            RefreshDeviceOrder();
        }

        private void SoundBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Audio Files|*.mp3;*.wav;*.m4a|All Files|*.*",
                Title = "Select a sound file"
            };

            if (dialog.ShowDialog() != true) return;

            var fileInfo = new FileInfo(dialog.FileName);
            if (fileInfo.Length > Message.MaxLength)
            {
                MessageBox.Show($"Sound file must be under {Message.MaxLengthDisplay}MB.");
                return;
            }

            _selectedSoundData = File.ReadAllBytes(dialog.FileName);
            // Unreliable, but it works
            _selectedSoundFormat = Path.GetExtension(dialog.FileName).TrimStart('.').ToLower();
            SoundFileLabel.Text = Path.GetFileName(dialog.FileName);
        }

        private void SoundClear_Click(object sender, RoutedEventArgs e)
        {
            _selectedSoundData = null;
            _selectedSoundFormat = null;
            SoundFileLabel.Text = "No sound selected";
        }

        private void ImageBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.jfif;*.png;*.gif;*.webp|All Files|*.*",
                Title = "Select a sound file"
            };

            if (dialog.ShowDialog() != true) return;

            var fileInfo = new FileInfo(dialog.FileName);
            if (fileInfo.Length > Message.MaxLength)
            {
                MessageBox.Show($"Image file must be under {Message.MaxLengthDisplay}MB.");
                return;
            }

            _selectedImageData = File.ReadAllBytes(dialog.FileName);
            ImageFileLabel.Text = Path.GetFileName(dialog.FileName);
        }

        private void ImageClear_Click(object sender, RoutedEventArgs e)
        {
            _selectedImageData = null;
            ImageFileLabel.Text = "No image selected";
        }
    }
}