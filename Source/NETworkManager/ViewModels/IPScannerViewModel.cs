﻿using NETworkManager.Models.Settings;
using System.Collections.ObjectModel;
using System.Net;
using System.Windows.Input;
using System.Windows;
using System;
using System.Collections;
using System.Threading;
using System.Collections.Generic;
using NETworkManager.Models.Network;
using System.Windows.Threading;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows.Data;
using System.Linq;
using NETworkManager.Utilities;
using Dragablz;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using NETworkManager.Controls;
using NETworkManager.Models.Export;
using NETworkManager.Views;
using NETworkManager.Models.EventSystem;
using NETworkManager.Enum;
using System.Text.RegularExpressions;

namespace NETworkManager.ViewModels
{
    public class IPScannerViewModel : ViewModelBase
    {
        #region Variables
        private readonly IDialogCoordinator _dialogCoordinator;

        private CancellationTokenSource _cancellationTokenSource;

        public readonly int TabId;
        private bool _firstLoad = true;

        private readonly DispatcherTimer _dispatcherTimer = new DispatcherTimer();
        private readonly Stopwatch _stopwatch = new Stopwatch();

        private readonly bool _isLoading;

        private string _host;
        public string Host
        {
            get => _host;
            set
            {
                if (value == _host)
                    return;

                _host = value;
                OnPropertyChanged();
            }
        }

        public ICollectionView HostHistoryView { get; }

        private bool _isIPRangeDetectionRunning;
        public bool IsIPRangeDetectionRunning
        {
            get => _isIPRangeDetectionRunning;
            set
            {
                if (value == _isIPRangeDetectionRunning)
                    return;

                _isIPRangeDetectionRunning = value;
                OnPropertyChanged();
            }
        }


        private bool _isScanRunning;
        public bool IsScanRunning
        {
            get => _isScanRunning;
            set
            {
                if (value == _isScanRunning)
                    return;

                _isScanRunning = value;
                OnPropertyChanged();
            }
        }

        private bool _cancelScan;
        public bool CancelScan
        {
            get => _cancelScan;
            set
            {
                if (value == _cancelScan)
                    return;

                _cancelScan = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<HostInfo> _hostResults = new ObservableCollection<HostInfo>();
        public ObservableCollection<HostInfo> HostResults
        {
            get => _hostResults;
            set
            {
                if (value != null && value == _hostResults)
                    return;

                _hostResults = value;
            }
        }

        public ICollectionView HostResultsView { get; }

        private HostInfo _selectedHostResult;
        public HostInfo SelectedHostResult
        {
            get => _selectedHostResult;
            set
            {
                if (value == _selectedHostResult)
                    return;

                _selectedHostResult = value;
                OnPropertyChanged();
            }
        }

        private IList _selectedHostResults = new ArrayList();
        public IList SelectedHostResults
        {
            get => _selectedHostResults;
            set
            {
                if (Equals(value, _selectedHostResults))
                    return;

                _selectedHostResults = value;
                OnPropertyChanged();
            }
        }

        public bool ResolveHostname => SettingsManager.Current.IPScanner_ResolveHostname;

        public bool ResolveMACAddress => SettingsManager.Current.IPScanner_ResolveMACAddress;

        private int _ipAddressesToScan;
        public int IPAddressesToScan
        {
            get => _ipAddressesToScan;
            set
            {
                if (value == _ipAddressesToScan)
                    return;

                _ipAddressesToScan = value;
                OnPropertyChanged();
            }
        }

        private int _ipAddressesScanned;
        public int IPAddressesScanned
        {
            get => _ipAddressesScanned;
            set
            {
                if (value == _ipAddressesScanned)
                    return;

                _ipAddressesScanned = value;
                OnPropertyChanged();
            }
        }

        private int _hostsFound;
        public int HostsFound
        {
            get => _hostsFound;
            set
            {
                if (value == _hostsFound)
                    return;

                _hostsFound = value;
                OnPropertyChanged();
            }
        }

        private bool _preparingScan;
        public bool PreparingScan
        {
            get => _preparingScan;
            set
            {
                if (value == _preparingScan)
                    return;

                _preparingScan = value;
                OnPropertyChanged();
            }
        }

        private bool _displayStatusMessage;
        public bool DisplayStatusMessage
        {
            get => _displayStatusMessage;
            set
            {
                if (value == _displayStatusMessage)
                    return;

                _displayStatusMessage = value;
                OnPropertyChanged();
            }
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (value == _statusMessage)
                    return;

                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        private DateTime? _startTime;
        public DateTime? StartTime
        {
            get => _startTime;
            set
            {
                if (value == _startTime)
                    return;

                _startTime = value;
                OnPropertyChanged();
            }
        }

        private TimeSpan _duration;
        public TimeSpan Duration
        {
            get => _duration;
            set
            {
                if (value == _duration)
                    return;

                _duration = value;
                OnPropertyChanged();
            }
        }

        private DateTime? _endTime;
        public DateTime? EndTime
        {
            get => _endTime;
            set
            {
                if (value == _endTime)
                    return;

                _endTime = value;
                OnPropertyChanged();
            }
        }

        public IEnumerable<CustomCommandInfo> CustomCommands => SettingsManager.Current.IPScanner_CustomCommands;

        private bool _expandStatistics;
        public bool ExpandStatistics
        {
            get => _expandStatistics;
            set
            {
                if (value == _expandStatistics)
                    return;

                if (!_isLoading)
                    SettingsManager.Current.IPScanner_ExpandStatistics = value;

                _expandStatistics = value;
                OnPropertyChanged();
            }
        }

        public bool ShowStatistics => SettingsManager.Current.IPScanner_ShowStatistics;
        #endregion

        #region Constructor, load settings, shutdown
        public IPScannerViewModel(IDialogCoordinator instance, int tabId, string hostOrIPRange)
        {
            _isLoading = true;

            _dialogCoordinator = instance;

            TabId = tabId;
            Host = hostOrIPRange;

            // Host history
            HostHistoryView = CollectionViewSource.GetDefaultView(SettingsManager.Current.IPScanner_HostHistory);

            // Result view
            HostResultsView = CollectionViewSource.GetDefaultView(HostResults);
            HostResultsView.SortDescriptions.Add(new SortDescription(nameof(HostInfo.PingInfo) + "." + nameof(PingInfo.IPAddressInt32), ListSortDirection.Ascending));

            // Add default custom commands...
            if (SettingsManager.Current.IPScanner_CustomCommands.Count == 0)
                SettingsManager.Current.IPScanner_CustomCommands = new ObservableCollection<CustomCommandInfo>(Utilities.CustomCommand.DefaultList());

            LoadSettings();

            // Detect if settings have changed...
            SettingsManager.Current.PropertyChanged += SettingsManager_PropertyChanged;

            _isLoading = false;
        }

        public void OnLoaded()
        {
            if (!_firstLoad)
                return;

            if (!string.IsNullOrEmpty(Host))
                StartScan();

            _firstLoad = false;
        }

        private void LoadSettings()
        {
            ExpandStatistics = SettingsManager.Current.IPScanner_ExpandStatistics;
        }
        #endregion

        #region ICommands & Actions

        public ICommand ScanCommand => new RelayCommand(p => ScanAction(), Scan_CanExecute);

        private bool Scan_CanExecute(object paramter)
        {
            return Application.Current.MainWindow != null && !((MetroWindow)Application.Current.MainWindow).IsAnyDialogOpen;
        }

        private void ScanAction()
        {
            Scan();
        }

        public ICommand DetectIPRangeCommand => new RelayCommand(p => DetectIPRangeAction());

        private void DetectIPRangeAction()
        {
            DetectIPRange();
        }

        public ICommand RedirectDataToApplicationCommand => new RelayCommand(RedirectDataToApplicationAction);

        private void RedirectDataToApplicationAction(object name)
        {
            if (!(name is string appName))
                return;

            if (!System.Enum.TryParse(appName, out ApplicationViewManager.Name applicationName))
                return;

            var host = !string.IsNullOrEmpty(SelectedHostResult.Hostname) ? SelectedHostResult.Hostname : SelectedHostResult.PingInfo.IPAddress.ToString();

            EventSystem.RedirectDataToApplication(applicationName, host);
        }

        public ICommand PerformDNSLookupIPAddressCommand => new RelayCommand(p => PerformDNSLookupIPAddressAction());

        private void PerformDNSLookupIPAddressAction()
        {
            EventSystem.RedirectDataToApplication(ApplicationViewManager.Name.DNSLookup, SelectedHostResult.PingInfo.IPAddress.ToString());
        }

        public ICommand PerformDNSLookupHostnameCommand => new RelayCommand(p => PerformDNSLookupHostnameAction());

        private void PerformDNSLookupHostnameAction()
        {
            EventSystem.RedirectDataToApplication(ApplicationViewManager.Name.DNSLookup, SelectedHostResult.Hostname);
        }

        public ICommand CustomCommandCommand => new RelayCommand(CustomCommandAction);

        private void CustomCommandAction(object guid)
        {
            CustomCommand(guid);
        }

        public ICommand AddProfileSelectedHostCommand => new RelayCommand(p => AddProfileSelectedHostAction());
        private async void AddProfileSelectedHostAction()
        {
            ProfileInfo profileInfo = new ProfileInfo()
            {
                Name = string.IsNullOrEmpty(SelectedHostResult.Hostname) ? SelectedHostResult.PingInfo.IPAddress.ToString() : SelectedHostResult.Hostname.TrimEnd('.'),
                Host = SelectedHostResult.PingInfo.IPAddress.ToString()
            };

            var customDialog = new CustomDialog
            {
                Title = Resources.Localization.Strings.AddProfile
            };

            var profileViewModel = new ProfileViewModel(instance =>
            {
                _dialogCoordinator.HideMetroDialogAsync(this, customDialog);

                ProfileManager.AddProfile(instance);
            }, instance =>
            {
                _dialogCoordinator.HideMetroDialogAsync(this, customDialog);
            }, ProfileManager.GetGroups(), ProfileEditMode.Add, profileInfo);

            customDialog.Content = new ProfileDialog
            {
                DataContext = profileViewModel
            };

            await _dialogCoordinator.ShowMetroDialogAsync(this, customDialog);
        }

        public ICommand CopySelectedIPAddressCommand => new RelayCommand(p => CopySelectedIPAddressAction());

        private void CopySelectedIPAddressAction()
        {
            CommonMethods.SetClipboard(SelectedHostResult.PingInfo.IPAddress.ToString());
        }

        public ICommand CopySelectedHostnameCommand => new RelayCommand(p => CopySelectedHostnameAction());

        private void CopySelectedHostnameAction()
        {
            CommonMethods.SetClipboard(SelectedHostResult.Hostname);
        }

        public ICommand CopySelectedMACAddressCommand => new RelayCommand(p => CopySelectedMACAddressAction());

        private void CopySelectedMACAddressAction()
        {
            CommonMethods.SetClipboard(MACAddressHelper.GetDefaultFormat(SelectedHostResult.MACAddress.ToString()));
        }

        public ICommand CopySelectedVendorCommand => new RelayCommand(p => CopySelectedVendorAction());

        private void CopySelectedVendorAction()
        {
            CommonMethods.SetClipboard(SelectedHostResult.Vendor);
        }

        public ICommand CopySelectedBytesCommand => new RelayCommand(p => CopySelectedBytesAction());

        private void CopySelectedBytesAction()
        {
            CommonMethods.SetClipboard(SelectedHostResult.PingInfo.Bytes.ToString());
        }

        public ICommand CopySelectedTimeCommand => new RelayCommand(p => CopySelectedTimeAction());

        private void CopySelectedTimeAction()
        {
            CommonMethods.SetClipboard(SelectedHostResult.PingInfo.Time.ToString());
        }

        public ICommand CopySelectedTTLCommand => new RelayCommand(p => CopySelectedTTLAction());

        private void CopySelectedTTLAction()
        {
            CommonMethods.SetClipboard(SelectedHostResult.PingInfo.TTL.ToString());
        }

        public ICommand CopySelectedStatusCommand => new RelayCommand(p => CopySelectedStatusAction());

        private void CopySelectedStatusAction()
        {
            CommonMethods.SetClipboard(LocalizationManager.TranslateIPStatus(SelectedHostResult.PingInfo.Status));
        }

        public ICommand ExportCommand => new RelayCommand(p => ExportAction());

        private void ExportAction()
        {
            Export();
        }
        #endregion

        #region Methods
        private void Scan()
        {
            if (IsScanRunning)
                StopScan();
            else
                StartScan();
        }

        private async void StartScan()
        {
            DisplayStatusMessage = false;
            IsScanRunning = true;
            PreparingScan = true;

            // Measure the time
            StartTime = DateTime.Now;
            _stopwatch.Start();
            _dispatcherTimer.Tick += DispatcherTimer_Tick;
            _dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            _dispatcherTimer.Start();
            EndTime = null;

            HostResults.Clear();
            HostsFound = 0;

            // Change the tab title (not nice, but it works)
            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive);

            if (window != null)
            {
                foreach (var tabablzControl in VisualTreeHelper.FindVisualChildren<TabablzControl>(window))
                {
                    tabablzControl.Items.OfType<DragablzTabItem>().First(x => x.Id == TabId).Header = Host;
                }
            }

            _cancellationTokenSource = new CancellationTokenSource();

            // Resolve hostnames
            List<string> ipRanges;

            try
            {
                ipRanges = await HostRangeHelper.ResolveHostnamesInIPRangesAsync(Host.Replace(" ", "").Split(';'), _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                UserHasCanceled(this, EventArgs.Empty);
                return;
            }
            catch (AggregateException exceptions) // DNS error (could not resolve hostname...)
            {
                DnsResolveFailed(exceptions);
                return;
            }

            // Create ip addresses 
            IPAddress[] ipAddresses;

            try
            {
                // Create a list of all ip addresses
                ipAddresses = await HostRangeHelper.CreateIPAddressesFromIPRangesAsync(ipRanges.ToArray(), _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                UserHasCanceled(this, EventArgs.Empty);
                return;
            }

            IPAddressesToScan = ipAddresses.Length;
            IPAddressesScanned = 0;

            PreparingScan = false;

            // Add host(s) to the history
            AddHostToHistory(Host);

            var ipScanner = new IPScanner
            {
                Threads = SettingsManager.Current.IPScanner_Threads,
                ICMPTimeout = SettingsManager.Current.IPScanner_ICMPTimeout,
                ICMPBuffer = new byte[SettingsManager.Current.IPScanner_ICMPBuffer],
                ICMPAttempts = SettingsManager.Current.IPScanner_ICMPAttempts,
                ResolveHostname = SettingsManager.Current.IPScanner_ResolveHostname,
                UseCustomDNSServer = SettingsManager.Current.IPScanner_UseCustomDNSServer,
                DNSUseTCPOnly = SettingsManager.Current.IPScanner_DNSUseTCPOnly,
                DNSRecursion = SettingsManager.Current.IPScanner_DNSRecursion,
                DNSUseCache = SettingsManager.Current.IPScanner_DNSUseCache,
                DNSTimeout = TimeSpan.FromSeconds(SettingsManager.Current.IPScanner_DNSTimeout),
                DNSRetries = SettingsManager.Current.IPScanner_DNSRetries,
                DNSShowErrorMessage = SettingsManager.Current.IPScanner_DNSShowErrorMessage,
                ResolveMACAddress = SettingsManager.Current.IPScanner_ResolveMACAddress,
                ShowScanResultForAllIPAddresses = SettingsManager.Current.IPScanner_ShowScanResultForAllIPAddresses
            };

            // Set custom dns server...
            if (ipScanner.ResolveHostname && ipScanner.UseCustomDNSServer)
            {
                ipScanner.CustomDNSServer = IPAddress.Parse(SettingsManager.Current.IPScanner_CustomDNSServer);
                ipScanner.CustomDNSPort = SettingsManager.Current.IPScanner_CustomDNSPort;
            }

            ipScanner.HostFound += HostFound;
            ipScanner.ScanComplete += ScanComplete;
            ipScanner.ProgressChanged += ProgressChanged;
            ipScanner.UserHasCanceled += UserHasCanceled;

            ipScanner.ScanAsync(ipAddresses, _cancellationTokenSource.Token);
        }

        private void StopScan()
        {
            CancelScan = true;
            _cancellationTokenSource.Cancel();
        }

        private void ScanFinished()
        {
            // Stop timer and stopwatch
            _stopwatch.Stop();
            _dispatcherTimer.Stop();

            Duration = _stopwatch.Elapsed;
            EndTime = DateTime.Now;

            _stopwatch.Reset();

            CancelScan = false;
            IsScanRunning = false;
        }

        private async void DetectIPRange()
        {
            IsIPRangeDetectionRunning = true;

            var localIP = await NetworkInterface.DetectLocalIPAddressBasedOnRoutingAsync(IPAddress.Parse("1.1.1.1"));

            // Could not detect local ip address
            if (localIP == null)
                await _dialogCoordinator.ShowMessageAsync(this, Resources.Localization.Strings.Error, Resources.Localization.Strings.CouldNotDetectLocalIPAddressMessage, MessageDialogStyle.Affirmative, AppearanceManager.MetroDialog);

            var subnetmaskDetected = false;

            // Get subnetmask, based on ip address
            foreach (var networkInterface in await NetworkInterface.GetNetworkInterfacesAsync())
            {
                if (networkInterface.IPv4Address.Contains(localIP))
                {
                    subnetmaskDetected = true;

                    Host = $"{localIP}/{Subnetmask.ConvertSubnetmaskToCidr(networkInterface.Subnetmask.First())}";

                    // Fix: If the user clears the textbox and then clicks again on the button, the textbox remains empty...
                    OnPropertyChanged(nameof(Host));

                    break;
                }
            }

            if (!subnetmaskDetected)
                await _dialogCoordinator.ShowMessageAsync(this, Resources.Localization.Strings.Error, Resources.Localization.Strings.CouldNotDetectSubnetmask, MessageDialogStyle.Affirmative, AppearanceManager.MetroDialog);

            IsIPRangeDetectionRunning = false;
        }

        private async void CustomCommand(object guid)
        {
            if (guid is Guid id)
            {
                CustomCommandInfo info = (CustomCommandInfo)CustomCommands.FirstOrDefault(x => x.ID == id).Clone();

                if (info == null)
                    return; // ToDo: Log and error message

                // Replace vars
                string hostname = !string.IsNullOrEmpty(SelectedHostResult.Hostname) ? SelectedHostResult.Hostname.TrimEnd('.') : "";
                string ipAddress = SelectedHostResult.PingInfo.IPAddress.ToString();

                info.FilePath = Regex.Replace(info.FilePath, "\\$\\$hostname\\$\\$", hostname, RegexOptions.IgnoreCase);
                info.FilePath = Regex.Replace(info.FilePath, "\\$\\$ipaddress\\$\\$", ipAddress, RegexOptions.IgnoreCase);
                if (!string.IsNullOrEmpty(info.Arguments))
                {
                    info.Arguments = Regex.Replace(info.Arguments, "\\$\\$hostname\\$\\$", hostname, RegexOptions.IgnoreCase);
                    info.Arguments = Regex.Replace(info.Arguments, "\\$\\$ipaddress\\$\\$", ipAddress, RegexOptions.IgnoreCase);
                }

                try
                {
                    Utilities.CustomCommand.Run(info);
                }
                catch (Exception ex)
                {
                    await _dialogCoordinator.ShowMessageAsync(this, Resources.Localization.Strings.ResourceManager.GetString("Error", LocalizationManager.Culture), ex.Message, MessageDialogStyle.Affirmative, AppearanceManager.MetroDialog);
                }
            }
        }

        private void AddHostToHistory(string ipRange)
        {
            // Create the new list
            var list = ListHelper.Modify(SettingsManager.Current.IPScanner_HostHistory.ToList(), ipRange, SettingsManager.Current.General_HistoryListEntries);

            // Clear the old items
            SettingsManager.Current.IPScanner_HostHistory.Clear();
            OnPropertyChanged(nameof(Host)); // Raise property changed again, after the collection has been cleared

            // Fill with the new items
            list.ForEach(x => SettingsManager.Current.IPScanner_HostHistory.Add(x));
        }

        private async void Export()
        {
            var customDialog = new CustomDialog
            {
                Title = Resources.Localization.Strings.Export
            };

            var exportViewModel = new ExportViewModel(async instance =>
            {
                await _dialogCoordinator.HideMetroDialogAsync(this, customDialog);

                try
                {
                    ExportManager.Export(instance.FilePath, instance.FileType, instance.ExportAll ? HostResults : new ObservableCollection<HostInfo>(SelectedHostResults.Cast<HostInfo>().ToArray()));
                }
                catch (Exception ex)
                {
                    var settings = AppearanceManager.MetroDialog;
                    settings.AffirmativeButtonText = Resources.Localization.Strings.OK;

                    await _dialogCoordinator.ShowMessageAsync(this, Resources.Localization.Strings.Error, Resources.Localization.Strings.AnErrorOccurredWhileExportingTheData + Environment.NewLine + Environment.NewLine + ex.Message, MessageDialogStyle.Affirmative, settings);
                }

                SettingsManager.Current.IPScanner_ExportFileType = instance.FileType;
                SettingsManager.Current.IPScanner_ExportFilePath = instance.FilePath;
            }, instance => { _dialogCoordinator.HideMetroDialogAsync(this, customDialog); }, SettingsManager.Current.IPScanner_ExportFileType, SettingsManager.Current.IPScanner_ExportFilePath);

            customDialog.Content = new ExportDialog
            {
                DataContext = exportViewModel
            };

            await _dialogCoordinator.ShowMetroDialogAsync(this, customDialog);
        }

        public void OnClose()
        {
            // Stop scan
            if (IsScanRunning)
                StopScan();
        }

        #endregion

        #region Events
        private void HostFound(object sender, HostFoundArgs e)
        {
            var ipScannerHostInfo = HostInfo.Parse(e);

            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                lock (HostResults)
                    HostResults.Add(ipScannerHostInfo);
            }));

            HostsFound++;
        }

        private void ScanComplete(object sender, EventArgs e)
        {
            ScanFinished();
        }

        private void ProgressChanged(object sender, ProgressChangedArgs e)
        {
            IPAddressesScanned = e.Value;
        }

        private void DnsResolveFailed(AggregateException e)
        {
            StatusMessage = $"{Resources.Localization.Strings.TheFollowingHostnamesCouldNotBeResolved} {string.Join(", ", e.Flatten().InnerExceptions.Select(x => x.Message))}";
            DisplayStatusMessage = true;

            ScanFinished();
        }

        private void UserHasCanceled(object sender, EventArgs e)
        {
            StatusMessage = Resources.Localization.Strings.CanceledByUserMessage;
            DisplayStatusMessage = true;

            ScanFinished();
        }

        private void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            Duration = _stopwatch.Elapsed;
        }

        private void SettingsManager_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SettingsInfo.IPScanner_ResolveMACAddress):
                    OnPropertyChanged(nameof(ResolveMACAddress));
                    break;
                case nameof(SettingsInfo.IPScanner_ResolveHostname):
                    OnPropertyChanged(nameof(ResolveHostname));
                    break;
                case nameof(SettingsInfo.IPScanner_ShowStatistics):
                    OnPropertyChanged(nameof(ShowStatistics));
                    break;
            }
        }
        #endregion
    }
}