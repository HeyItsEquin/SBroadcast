using MessageBroadcast.Core;
using MessageBroadcast.Sender.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace MessageBroadcast.Sender
{
    public class PercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var percentage = Double.Parse(parameter.ToString()!);
            return (double)value * percentage;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var percentage = Double.Parse(parameter.ToString()!);
            return (double)value / percentage;
        }
    }

    public partial class MainWindow : Window
    {
        private readonly DeviceInfo _localDevice;
        private readonly DeviceDiscovery _discovery;
        private readonly MessageSender _sender;
        private CancellationTokenSource _cts = new();
        private readonly Dictionary<Guid, DeviceInfo> _devices = new();
        private ObservableCollection<GroupInfo> _groups = new();
        private readonly CollectionViewSource _groupsViewSource = new();
        private readonly ObservableCollection<DeviceInfo> _deviceList = new();
        private readonly CollectionViewSource _deviceViewSource = new();

        private byte[]? _selectedSoundData;
        private string? _selectedSoundFormat;

        private byte[]? _selectedImageData;

        private byte[]? _selectedVideoData;
        private string? _selectedVideoFormat;

        public MainWindow()
        {
            InitializeComponent();
            LoadAppConfig(); // Load and apply application configs
            Icon = IconLoader.LoadIcon() ?? Icon;

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

            _groups = new ObservableCollection<GroupInfo>(ConfigStore.Instance.GetGroups());

            _groupsViewSource.Source = _groups;
            _groupsViewSource.SortDescriptions.Add(
                new SortDescription(nameof(GroupInfo.GroupName), ListSortDirection.Ascending));

            GroupsList.ItemsSource = _groupsViewSource.View;

            // Event handlers for updating config
            FontSizeSlider.ValueChanged += (_, _) => 
                ConfigStore.Instance.UpdateAppConfig(nameof(AppConfig.DefaultFontSize), (int)FontSizeSlider.Value);
            DisplayTimeSlider.ValueChanged += (_, _) =>
                ConfigStore.Instance.UpdateAppConfig(nameof(AppConfig.DefaultDisplaySeconds), DisplayTimeSlider.Value);
            FadeoutTimeSlider.ValueChanged += (_, _) =>
                ConfigStore.Instance.UpdateAppConfig(nameof(AppConfig.FadeoutTimeSeconds), FadeoutTimeSlider.Value);
            PositionCombo.SelectionChanged += (_, _) =>
                ConfigStore.Instance.UpdateAppConfig(nameof(AppConfig.DefaultPosition), (MessagePosition)PositionCombo.SelectedIndex);
            FontFamilyCombo.SelectionChanged += (_, _) =>
                ConfigStore.Instance.UpdateAppConfig(nameof(AppConfig.DefaultFontFamily), 
                (FontFamilyCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Segoe UI");
            FontColorCombo.SelectionChanged += (_, _) =>
                ConfigStore.Instance.UpdateAppConfig(nameof(AppConfig.DefaultFontColor),
                (FontColorCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "White");
            ImagePositionCombo.SelectionChanged += (_, _) =>
                ConfigStore.Instance.UpdateAppConfig(nameof(AppConfig.DefaultImagePosition), (MessagePosition)ImagePositionCombo.SelectedIndex);
            AnchorTextCheckbox.Checked += (_, _) =>
                ConfigStore.Instance.UpdateAppConfig(nameof(AppConfig.AnchorTextToImage), AnchorTextCheckbox.IsChecked ?? false);

            _sender = new MessageSender();

            _discovery = new DeviceDiscovery(_localDevice);
            _discovery.DeviceDiscovered += OnDeviceDiscovered;
            _discovery.Start();
        }

        protected override async void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            await Task.Delay(250);
            // Setup device list on startup
            await QueryNewDevices();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftShift))
            {
                Application.Current.Shutdown();
                base.OnClosing(e);
            }
            else
            {
                e.Cancel = true;
                Hide();
                base.OnClosing(e);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _cts.Cancel();
            _discovery.Dispose();
            Application.Current.Shutdown();
            base.OnClosed(e);
        }

        private async Task QueryNewDevices()
        {
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
        }

        // Load and apply application configs
        private void LoadAppConfig()
        {
            var config = ConfigStore.Instance.GetAppConfig();
            FontSizeSlider.Value = (double)config.DefaultFontSize;
            DisplayTimeSlider.Value = config.DefaultDisplaySeconds;
            FadeoutTimeSlider.Value = config.FadeoutTimeSeconds;
            PositionCombo.SelectedIndex = (int)config.DefaultPosition;
            FontFamilyCombo.SelectedIndex = FindComboIndex(FontFamilyCombo, config.DefaultFontFamily);
            FontColorCombo.SelectedIndex = FindComboIndex(FontColorCombo, config.DefaultFontColor);
            ImagePositionCombo.SelectedIndex = (int)config.DefaultImagePosition;
            AnchorTextCheckbox.IsChecked = config.AnchorTextToImage;
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

        private MessageContentType GetMessageType()
        {
            var type = MessageContentType.None;

            // Use bitwise ORs because it's a flag enum
            if (!string.IsNullOrEmpty(MessageInput.Text.Trim())) type |= MessageContentType.Text;
            if (_selectedImageData != null) type |= MessageContentType.Image;
            if (_selectedSoundData != null) type |= MessageContentType.Sound;
            if (_selectedVideoData != null) type |= MessageContentType.Video;

            return type;
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

            AddToGroupMenuItem.Items.Clear();
            var createNewGroup = new MenuItem
            {
                Header = "Create New...",
                IsEnabled = true
            };
            createNewGroup.Click += CreateNewGroupItem_Click;
            AddToGroupMenuItem.Items.Add(createNewGroup); // Always allow the user to create a new group
            AddToGroupMenuItem.Items.Add(new Separator());

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
            
            // List all groups that can be added to
            foreach (var group in _groups)
            {
                var groupItem = new MenuItem
                {
                    Header = group.GroupName,
                    IsCheckable = true,
                    IsChecked = group.GroupMembers.Contains(device),
                };

                groupItem.Click += (sender, e) =>
                {
                    if (group.GroupMembers.Contains(device))
                    {
                        e.Handled = true;
                        return;
                    }

                    group.GroupMembers.Add(device);
                    e.Handled = true;
                };
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var target = DeviceList.SelectedItem as DeviceInfo;
            var group = GroupsList.SelectedItem as GroupInfo;

            if (target == null && group == null)
            {
                MessageBox.Show("Please select a target first.");
                return;
            }

            if (target != null && target.Blocked)
            {
                MessageBox.Show("You are unable to send messages to a user whom you've blocked.");
                return;
            }

            var text = MessageInput.Text.Trim();
            if (string.IsNullOrEmpty(text) && _selectedSoundData == null && _selectedImageData == null && _selectedVideoData == null)
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
                DisplaySeconds = DisplayTimeSlider.Value,
                FadeoutTimeSeconds = FadeoutTimeSlider.Value,
                Position = (MessagePosition)PositionCombo.SelectedIndex,
                ImagePosition = (MessagePosition)ImagePositionCombo.SelectedIndex,
                AnchorTextToImage = AnchorTextCheckbox.IsChecked ?? false,
                FontFamily = selectedFont,
                FontColor = selectedColor,
                SoundData = _selectedSoundData,
                SoundFormat = _selectedSoundFormat,
                ImageData = _selectedImageData,
                VideoData = _selectedVideoData,
                HideVideoWhenDone = HideVideoCheckbox.IsChecked ?? false,
                UseVideoLengthAsDisplayTime = VideoLengthAsDisplayTimeCheckbox.IsChecked ?? false,
                MuteVideo = MuteVideoCheckbox.IsChecked ?? false,
                VideoFormat = _selectedVideoFormat,
                ContentType = GetMessageType()
            };

            if (group != null)
            {
                foreach (var dvc in group.GroupMembers)
                {
                    var success = await _sender.SendMessageAsync(dvc, message);
                    if (success)
                        MessageInput.Text = string.Empty;
                    else
                        MessageBox.Show($"Failed to send to {dvc.PreferredName}. For some reason");
                }
            }
            else
            {
                var success = await _sender.SendMessageAsync(target!, message);
                if (success)
                    MessageInput.Text = string.Empty;
                else
                    MessageBox.Show($"Failed to send to {target!.PreferredName}. They may have gone offline.");
            }
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
            await QueryNewDevices();
            Mouse.OverrideCursor = null;
        }

        // Generic helper fn, don't look too hard into it
        private T? FindVisualChild<T>(DependencyObject parent) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T element)
                    return element;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }

            return null;
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
                Title = "Select an image file"
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

        private void VideoBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.mp4;*.mov;*.mkv;*.|All Files|*.*",
                Title = "Select a video file"
            };

            if (dialog.ShowDialog() != true) return;

            var fileInfo = new FileInfo(dialog.FileName);
            if (fileInfo.Length > Message.MaxLength)
            {
                MessageBox.Show($"Video file must be under {Message.MaxLength}MB.");
                return;
            }

            _selectedVideoData = File.ReadAllBytes(dialog.FileName);
            _selectedVideoFormat = Path.GetExtension(dialog.FileName);
            VideoFileLabel.Text = Path.GetFileName(dialog.FileName);
        }

        private void VideoClear_Click(object sender, RoutedEventArgs e)
        {
            _selectedVideoData = null;
            _selectedVideoFormat = null;
            VideoFileLabel.Text = "No video selected";
        }

        private void ImageClear_Click(object sender, RoutedEventArgs e)
        {
            _selectedImageData = null;
            ImageFileLabel.Text = "No image selected";
        }

        private void DisplayTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DisplayTimeSlider.Value <= 5)
            {
                DisplayTimeSlider.TickFrequency = 0.1;
            }
            else
            {
                // Snap to nearest whole number when past threshold
                DisplayTimeSlider.Value = Math.Round(e.NewValue, MidpointRounding.AwayFromZero); // Use conventional rounding
                DisplayTimeSlider.TickFrequency = 1.0;
            }
        }

        private void SetNicknameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceList.SelectedItem is not DeviceInfo device) return;

            var container = DeviceList.ItemContainerGenerator
                .ContainerFromItem(device) as ListViewItem;
            if (container == null) return;

            var editableTextBlock = FindVisualChild<EditableTextBlock>(container);
            editableTextBlock?.BeginEdit();
        }

        private void EditableTextBlock_TextCommitted(object sender, string e)
        {
            if (sender is EditableTextBlock control &&
                control.DataContext is DeviceInfo device)
            {
                var config = ConfigStore.Instance.GetDeviceConfig(device.Id);
                config.Nickname = e;
                ConfigStore.Instance.SetDeviceConfig(device.Id, config);
            }
        }

        private void GroupNameEdit_TextCommitted(object sender, string e)
        {
            if (sender is EditableTextBlock control &&
                control.DataContext is GroupInfo group)
            {
                if (_groups.Any(g => g.GroupName == e))
                {
                    MessageBox.Show("A group with that name already exists");
                    return;
                }
                foreach (var g in _groups.Where(g => g.GroupName == group.GroupName))
                {
                    g.GroupName = e;
                }
                ConfigStore.Instance.SetGroups(new List<GroupInfo>(_groups));
            }
        }

        private void CreateNewGroupItem_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceList.SelectedItem is not DeviceInfo dvc) return;

            var prompt = new CreateGroup();
            var result = prompt.ShowDialog() ?? false;

            if (!result) return;

            var group = prompt.group!;
            group.GroupMembers.Add(dvc);

            _groups.Add(group);
            ConfigStore.Instance.SetGroups(new List<GroupInfo>(_groups));

            e.Handled = true;
            return;
        }
    }
}