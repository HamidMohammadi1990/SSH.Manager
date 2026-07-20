using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SshManager.Models;
using SshManager.Services;
using SshManager.Views;

namespace SshManager.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly JsonDataService _dataService = new();
    private readonly ConnectionTestService _connectionTestService = new();
    private readonly ExecutionService _executionService = new();
    private readonly BatchExecutionService _batchExecutionService = new();
    private readonly DispatcherTimer _clockTimer;
    private CancellationTokenSource? _executionCts;
    private BatchJob? _loadedBatchJob;
    private bool _isDirty;
    private GroupItemViewModel? _watchedGroup;
    private bool _isTabSync;
    private bool _suppressTabOnSelect;

    [ObservableProperty] private string _currentDate = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
    [ObservableProperty] private string _currentTime = DateTime.Now.ToString("HH:mm:ss");
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private bool _isExecuting;
    [ObservableProperty] private bool _isTestingConnections;
    [ObservableProperty] private string _defaultUsername = string.Empty;
    [ObservableProperty] private string _defaultPassword = string.Empty;
    [ObservableProperty] private int _connectionTimeoutSeconds = 30;
    [ObservableProperty] private int _commandTimeoutSeconds = 60;
    [ObservableProperty] private int _batchStepDelayMs = 500;
    [ObservableProperty] private ServerItemViewModel? _selectedServer;
    [ObservableProperty] private ServerTabViewModel? _selectedTab;
    [ObservableProperty] private GroupItemViewModel? _selectedGroup;
    [ObservableProperty] private string _serversListTitle = "Servers";
    [ObservableProperty] private CommandItemViewModel? _selectedCommand;
    [ObservableProperty] private TargetItemViewModel? _selectedTarget;
    [ObservableProperty] private string _executionSummary = string.Empty;
    [ObservableProperty] private bool _hasExecutionResults;
    [ObservableProperty] private string _loadedBatchSummary = string.Empty;
    [ObservableProperty] private ConnectionType _batchConnectionType = ConnectionType.Telnet;

    public bool HasLoadedBatch => _loadedBatchJob != null;
    public int BatchPort => BatchConnectionType == ConnectionType.Ssh ? 22 : 23;
    public string BatchPortLabel => $"Port {BatchPort}";

    public bool IsBatchTelnet
    {
        get => BatchConnectionType == ConnectionType.Telnet;
        set { if (value) BatchConnectionType = ConnectionType.Telnet; }
    }

    public bool IsBatchSsh
    {
        get => BatchConnectionType == ConnectionType.Ssh;
        set { if (value) BatchConnectionType = ConnectionType.Ssh; }
    }

    partial void OnBatchConnectionTypeChanged(ConnectionType value)
    {
        OnPropertyChanged(nameof(BatchPort));
        OnPropertyChanged(nameof(BatchPortLabel));
        OnPropertyChanged(nameof(IsBatchTelnet));
        OnPropertyChanged(nameof(IsBatchSsh));
    }

    public ObservableCollection<ServerItemViewModel> Servers { get; } = new();
    public ICollectionView ServersView { get; }
    public ObservableCollection<GroupItemViewModel> Groups { get; } = new();
    public ObservableCollection<GroupItemViewModel> GroupOptionsList { get; } = new();
    public ObservableCollection<ServerTabViewModel> OpenServerTabs { get; } = new();
    public ObservableCollection<OutputLineViewModel> OutputLines { get; } = new();
    public ObservableCollection<ExecutionServerViewModel> ExecutionResults { get; } = new();

    private static readonly GroupItemViewModel NoGroupOption = new() { Id = string.Empty, Name = "(No Group)" };

    public Array ConnectionTypes => Enum.GetValues(typeof(ConnectionType));

    public bool HasOpenTabs => OpenServerTabs.Count > 0;

    public MainViewModel()
    {
        ServersView = CollectionViewSource.GetDefaultView(Servers);
        ServersView.Filter = FilterServerBySelectedGroup;

        Groups.CollectionChanged += OnGroupsCollectionChanged;
        Servers.CollectionChanged += OnServersCollectionChanged;
        OpenServerTabs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasOpenTabs));

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) =>
        {
            CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            CurrentDate = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
        };
        _clockTimer.Start();

        _executionService.OutputReceived += line =>
        {
            Application.Current.Dispatcher.Invoke(() => AddOutput(line, "Output"));
        };

        LoadData();
    }

    partial void OnSelectedGroupChanged(GroupItemViewModel? value)
    {
        if (_watchedGroup != null)
            _watchedGroup.PropertyChanged -= OnSelectedGroupPropertyChanged;

        _watchedGroup = value;

        if (_watchedGroup != null)
            _watchedGroup.PropertyChanged += OnSelectedGroupPropertyChanged;

        RefreshServerFilter();
    }

    private void OnServersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (ServerItemViewModel server in e.NewItems)
                server.PropertyChanged += OnServerPropertyChanged;
        }

        if (e.OldItems != null)
        {
            foreach (ServerItemViewModel server in e.OldItems)
                server.PropertyChanged -= OnServerPropertyChanged;
        }

        RefreshServerFilter();
    }

    private void OnServerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ServerItemViewModel.GroupId))
            RefreshServerFilter();
    }

    private bool FilterServerBySelectedGroup(object item)
    {
        if (item is not ServerItemViewModel server)
            return false;

        if (SelectedGroup == null)
            return true;

        return string.Equals(server.GroupId, SelectedGroup.Id, StringComparison.Ordinal);
    }

    private void RefreshServerFilter()
    {
        ServersView.Refresh();
        UpdateServersListTitle();
        EnsureSelectedServerInFilter();
    }

    private void UpdateServersListTitle()
    {
        if (SelectedGroup == null)
        {
            ServersListTitle = $"Servers ({Servers.Count})";
            return;
        }

        var count = Servers.Count(s => s.GroupId == SelectedGroup.Id);
        ServersListTitle = $"Servers — {SelectedGroup.Name} ({count})";
    }

    private void EnsureSelectedServerInFilter()
    {
        if (SelectedServer == null)
            return;

        if (ServersView.Contains(SelectedServer))
            return;

        SelectedServer = ServersView.Cast<ServerItemViewModel>().FirstOrDefault();
    }

    private void OnSelectedGroupPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GroupItemViewModel.Name))
        {
            RefreshGroupOptions();
            MarkDirty();
        }
    }

    private void OnGroupsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (GroupItemViewModel group in e.NewItems)
                group.PropertyChanged += OnGroupItemPropertyChanged;
        }

        if (e.OldItems != null)
        {
            foreach (GroupItemViewModel group in e.OldItems)
                group.PropertyChanged -= OnGroupItemPropertyChanged;
        }

        RefreshGroupOptions();
    }

    private void OnGroupItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GroupItemViewModel.Name))
            RefreshGroupOptions();
    }

    private void RefreshGroupOptions()
    {
        GroupOptionsList.Clear();
        GroupOptionsList.Add(NoGroupOption);
        foreach (var group in Groups.OrderBy(g => g.Order))
            GroupOptionsList.Add(group);
    }

    partial void OnSelectedTabChanged(ServerTabViewModel? value)
    {
        if (_isTabSync || value == null) return;
        if (ReferenceEquals(SelectedServer, value.Server)) return;

        _isTabSync = true;
        try
        {
            SelectedServer = value.Server;
        }
        finally
        {
            _isTabSync = false;
        }
    }

    partial void OnSelectedServerChanged(ServerItemViewModel? value)
    {
        if (value != null && !_isTabSync)
        {
            if (_suppressTabOnSelect)
                _suppressTabOnSelect = false;
            else
                OpenOrSelectTab(value);
        }

        if (value == null) return;

        if (value.ConnectionType == ConnectionType.Ssh && value.Port == 23)
            value.Port = 22;
        if (value.ConnectionType == ConnectionType.Telnet && value.Port == 22)
            value.Port = 23;
    }

    private ServerItemViewModel? ActiveServer => SelectedTab?.Server ?? SelectedServer;

    private void OpenOrSelectTab(ServerItemViewModel server)
    {
        if (_isTabSync) return;

        _isTabSync = true;
        try
        {
            var existing = OpenServerTabs.FirstOrDefault(t => t.Server.Id == server.Id);
            if (existing == null)
            {
                existing = new ServerTabViewModel(server);
                OpenServerTabs.Add(existing);
            }

            SelectedTab = existing;
            if (!ReferenceEquals(SelectedServer, server))
                SelectedServer = server;
        }
        finally
        {
            _isTabSync = false;
        }
    }

    private void CloseTabForServer(ServerItemViewModel server)
    {
        var tab = OpenServerTabs.FirstOrDefault(t => t.Server.Id == server.Id);
        if (tab == null) return;

        tab.Detach();
        OpenServerTabs.Remove(tab);
        if (SelectedTab == tab)
            SelectedTab = OpenServerTabs.LastOrDefault();
    }

    [RelayCommand]
    private void CloseTab(ServerTabViewModel? tab)
    {
        if (tab == null) return;
        tab.Detach();
        OpenServerTabs.Remove(tab);
        if (SelectedTab == tab)
            SelectedTab = OpenServerTabs.LastOrDefault();
    }

    private void MarkDirty()
    {
        _isDirty = true;
        StatusMessage = "Unsaved changes";
    }

    private void LoadData()
    {
        var data = _dataService.Load();
        DefaultUsername = data.Settings.DefaultUsername;
        DefaultPassword = data.Settings.DefaultPasswordEncrypted != null
            ? CredentialService.Decrypt(data.Settings.DefaultPasswordEncrypted)
            : string.Empty;
        ConnectionTimeoutSeconds = data.Settings.ConnectionTimeoutSeconds;
        CommandTimeoutSeconds = data.Settings.CommandTimeoutSeconds;
        BatchStepDelayMs = data.Settings.BatchStepDelayMs > 0 ? data.Settings.BatchStepDelayMs : 500;

        Groups.Clear();
        foreach (var g in data.Groups.OrderBy(g => g.Order))
            Groups.Add(GroupItemViewModel.FromModel(g));

        RefreshGroupOptions();

        Servers.Clear();
        OpenServerTabs.Clear();
        SelectedTab = null;
        foreach (var s in data.Servers.OrderBy(s => s.Order))
        {
            var pwd = s.UseCustomCredentials && s.CustomPasswordEncrypted != null
                ? CredentialService.Decrypt(s.CustomPasswordEncrypted)
                : string.Empty;
            Servers.Add(ServerItemViewModel.FromModel(s, pwd));
        }

        _isDirty = false;
        StatusMessage = "Ready";
        RefreshServerFilter();
    }

    public void SaveData()
    {
        var data = new AppData
        {
            Settings = new AppSettings
            {
                DefaultUsername = DefaultUsername,
                DefaultPasswordEncrypted = string.IsNullOrEmpty(DefaultPassword)
                    ? null
                    : CredentialService.Encrypt(DefaultPassword),
                ConnectionTimeoutSeconds = ConnectionTimeoutSeconds,
                CommandTimeoutSeconds = CommandTimeoutSeconds,
                BatchStepDelayMs = BatchStepDelayMs,
                Theme = AppTheme.Dark
            },
            Groups = Groups.Select(g => g.ToModel()).OrderBy(g => g.Order).ToList(),
            Servers = Servers.Select(s =>
            {
                string? encPwd = null;
                if (s.UseCustomCredentials && !string.IsNullOrEmpty(s.CustomPassword))
                    encPwd = CredentialService.Encrypt(s.CustomPassword);
                return s.ToModel(encPwd);
            }).OrderBy(s => s.Order).ToList()
        };

        _dataService.Save(data);
        _isDirty = false;
        StatusMessage = "Saved successfully";
    }

    public bool HasUnsavedChanges => _isDirty;

    public bool ConfirmSaveOnExit()
    {
        if (!_isDirty) return true;

        var result = DialogService.ShowYesNoCancel(
            "You have unsaved changes. Would you like to save before exiting?\n\nClick 'Yes' to save, 'No' to exit without saving, or 'Cancel' to stay.",
            "Unsaved Changes");

        return result switch
        {
            MessageBoxResult.Yes => SaveAndReturn(),
            MessageBoxResult.No => ConfirmBackup(),
            _ => false
        };
    }

    private bool SaveAndReturn()
    {
        SaveData();
        return true;
    }

    private bool ConfirmBackup()
    {
        var backup = DialogService.ShowYesNo(
            "Would you like to export a backup of your servers and settings before exiting?",
            "Export Backup");

        if (backup == MessageBoxResult.Yes)
            ExportData();

        return true;
    }

    [RelayCommand]
    private void Save() => SaveData();

    [RelayCommand]
    private void AddServer()
    {
        var server = new ServerItemViewModel
        {
            Name = "New Server",
            Host = "192.168.1.1",
            Port = 22,
            Order = Servers.Count,
            CreatedAt = DateTime.Now,
            GroupId = SelectedGroup?.Id ?? string.Empty
        };
        Servers.Add(server);
        OpenOrSelectTab(server);
        MarkDirty();
    }

    [RelayCommand]
    private void ImportServerFromFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Server profile files (*.sshserver;*.sshsrv;*.txt)|*.sshserver;*.sshsrv;*.txt|All files (*.*)|*.*",
            DefaultExt = "sshserver"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var profile = ServerProfileFileParser.ParseFile(dialog.FileName);
            var groupId = SelectedGroup?.Id ?? string.Empty;

            var existing = Servers.FirstOrDefault(s =>
                s.Name.Equals(profile.ServerName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                var replace = DialogService.ShowYesNo(
                    $"A server named '{profile.ServerName}' already exists. Replace it?",
                    "Duplicate Server Name",
                    DialogKind.Warning);
                if (replace != MessageBoxResult.Yes) return;

                CloseTabForServer(existing);
                Servers.Remove(existing);
            }

            var server = new ServerItemViewModel
            {
                Name = profile.ServerName,
                Host = profile.Host,
                Port = profile.Port,
                ConnectionType = profile.ConnectionType,
                Description = profile.Description,
                GroupId = groupId,
                CreatedAt = DateTime.Now,
                Order = Servers.Count,
                UseCustomCredentials = profile.HasCredentials,
                CustomUsername = string.IsNullOrWhiteSpace(profile.Username) ? null : profile.Username,
                CustomPassword = profile.Password
            };

            for (var i = 0; i < profile.Steps.Count; i++)
            {
                server.Commands.Add(new CommandItemViewModel
                {
                    Text = profile.Steps[i],
                    Order = i
                });
            }

            foreach (var target in profile.Targets)
            {
                server.Targets.Add(new TargetItemViewModel { Host = target });
            }

            if (string.IsNullOrWhiteSpace(server.Host) && server.Targets.Count > 0)
                server.Host = server.Targets[0].Host;

            Servers.Add(server);
            ReorderServers();
            SelectedServer = server;
            OpenOrSelectTab(server);
            SaveData();

            StatusMessage = $"Imported and saved server '{server.Name}' ({profile.Steps.Count} command(s))";
            DialogService.ShowInfo(
                $"Server '{server.Name}' imported and saved.\n" +
                $"Host: {server.Host}:{server.Port} ({server.ConnectionType})\n" +
                $"Targets: {server.Targets.Count}\n" +
                $"Commands: {server.Commands.Count}",
                "Import Server");
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Import Server Failed");
        }
    }

    [RelayCommand]
    private void RemoveServer(ServerItemViewModel? server = null)
    {
        var target = server ?? SelectedServer;
        if (target == null) return;
        var result = DialogService.ShowYesNo(
            $"Remove server '{target.Name}'?",
            "Confirm Remove",
            DialogKind.Warning);
        if (result != MessageBoxResult.Yes) return;

        Servers.Remove(target);
        CloseTabForServer(target);
        ReorderServers();
        SelectedServer = ServersView.Cast<ServerItemViewModel>().FirstOrDefault();
        MarkDirty();
    }

    private ServerDetailsDialog? _detailsDialog;

    public void OpenServerDetails(ServerItemViewModel server)
    {
        try
        {
            if (_detailsDialog != null)
            {
                _detailsDialog.Close();
                _detailsDialog = null;
            }

            var groupName = Groups.FirstOrDefault(g => g.Id == server.GroupId)?.Name ?? "(No Group)";
            var settings = BuildSettings();
            var vm = new ServerDetailsViewModel(server, settings, groupName);
            var dialog = new ServerDetailsDialog
            {
                Owner = Application.Current.MainWindow,
                DataContext = vm
            };

            dialog.Closed += (_, _) =>
            {
                if (ReferenceEquals(_detailsDialog, dialog))
                    _detailsDialog = null;
            };

            _detailsDialog = dialog;
            dialog.Show();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Server Details");
        }
    }

    [RelayCommand]
    private void MoveServerUp()
    {
        if (ActiveServer == null) return;
        var idx = Servers.IndexOf(SelectedServer);
        if (idx <= 0) return;
        Servers.Move(idx, idx - 1);
        ReorderServers();
        MarkDirty();
    }

    [RelayCommand]
    private void MoveServerDown()
    {
        if (ActiveServer == null) return;
        var idx = Servers.IndexOf(SelectedServer);
        if (idx < 0 || idx >= Servers.Count - 1) return;
        Servers.Move(idx, idx + 1);
        ReorderServers();
        MarkDirty();
    }

    private void ReorderServers()
    {
        for (var i = 0; i < Servers.Count; i++)
            Servers[i].Order = i;
    }

    [RelayCommand]
    private void AddGroup()
    {
        var group = new GroupItemViewModel
        {
            Name = $"Group {Groups.Count + 1}",
            Order = Groups.Count
        };
        Groups.Add(group);
        SelectedGroup = group;
        RefreshGroupOptions();
        MarkDirty();
        StatusMessage = "Group added — edit name below, then click Save";
    }

    [RelayCommand]
    private void RemoveGroup()
    {
        if (SelectedGroup == null) return;
        var result = DialogService.ShowYesNo(
            $"Remove group '{SelectedGroup.Name}'? Servers in this group will become ungrouped.",
            "Confirm Remove",
            DialogKind.Warning);
        if (result != MessageBoxResult.Yes) return;

        var groupId = SelectedGroup.Id;
        foreach (var s in Servers.Where(s => s.GroupId == groupId))
            s.GroupId = string.Empty;

        Groups.Remove(SelectedGroup);
        ReorderGroups();
        SelectedGroup = Groups.FirstOrDefault();
        RefreshGroupOptions();
        MarkDirty();
    }

    [RelayCommand]
    private void AddCommand()
    {
        var server = ActiveServer;
        if (server == null) return;
        var cmd = new CommandItemViewModel { Text = "echo hello", Order = server.Commands.Count };
        server.Commands.Add(cmd);
        SelectedCommand = cmd;
        MarkDirty();
    }

    [RelayCommand]
    private void RemoveCommand()
    {
        var server = ActiveServer;
        if (server == null || SelectedCommand == null) return;
        server.Commands.Remove(SelectedCommand);
        ReorderCommands(server);
        MarkDirty();
    }

    [RelayCommand]
    private void MoveCommandUp()
    {
        var server = ActiveServer;
        if (server == null || SelectedCommand == null) return;
        var idx = server.Commands.IndexOf(SelectedCommand);
        if (idx <= 0) return;
        server.Commands.Move(idx, idx - 1);
        ReorderCommands(server);
        MarkDirty();
    }

    [RelayCommand]
    private void MoveCommandDown()
    {
        var server = ActiveServer;
        if (server == null || SelectedCommand == null) return;
        var idx = server.Commands.IndexOf(SelectedCommand);
        if (idx < 0 || idx >= server.Commands.Count - 1) return;
        server.Commands.Move(idx, idx + 1);
        ReorderCommands(server);
        MarkDirty();
    }

    private static void ReorderCommands(ServerItemViewModel server)
    {
        for (var i = 0; i < server.Commands.Count; i++)
            server.Commands[i].Order = i;
    }

    [RelayCommand]
    private void AddTarget()
    {
        var server = ActiveServer;
        if (server == null) return;
        var target = new TargetItemViewModel { Host = string.Empty };
        server.Targets.Add(target);
        SelectedTarget = target;
        MarkDirty();
    }

    [RelayCommand]
    private void RemoveTarget()
    {
        var server = ActiveServer;
        if (server == null || SelectedTarget == null) return;
        server.Targets.Remove(SelectedTarget);
        MarkDirty();
    }

    [RelayCommand]
    private void MoveTargetUp()
    {
        var server = ActiveServer;
        if (server == null || SelectedTarget == null) return;
        var idx = server.Targets.IndexOf(SelectedTarget);
        if (idx <= 0) return;
        server.Targets.Move(idx, idx - 1);
        MarkDirty();
    }

    [RelayCommand]
    private void MoveTargetDown()
    {
        var server = ActiveServer;
        if (server == null || SelectedTarget == null) return;
        var idx = server.Targets.IndexOf(SelectedTarget);
        if (idx < 0 || idx >= server.Targets.Count - 1) return;
        server.Targets.Move(idx, idx + 1);
        MarkDirty();
    }

    private void ReorderGroups()
    {
        for (var i = 0; i < Groups.Count; i++)
            Groups[i].Order = i;
    }

    [RelayCommand]
    private async Task TestAllConnectionsAsync()
    {
        if (IsTestingConnections || IsExecuting) return;
        IsTestingConnections = true;
        StatusMessage = "Testing connections...";

        var settings = BuildSettings();

        foreach (var server in Servers)
        {
            server.ConnectionStatus = ConnectionStatus.Testing;
            var isOnline = await _connectionTestService.TestConnectionAsync(server.ToModel(
                server.UseCustomCredentials && !string.IsNullOrEmpty(server.CustomPassword)
                    ? CredentialService.Encrypt(server.CustomPassword)
                    : null), settings);
            server.ConnectionStatus = isOnline ? ConnectionStatus.Online : ConnectionStatus.Offline;
        }

        var online = Servers.Count(s => s.ConnectionStatus == ConnectionStatus.Online);
        StatusMessage = $"Connection test complete: {online}/{Servers.Count} online";
        IsTestingConnections = false;
    }

    [RelayCommand]
    private async Task ExecuteAllAsync()
    {
        if (IsExecuting) return;

        var serversWithCommands = Servers.Where(s => s.Commands.Count > 0).ToList();
        if (serversWithCommands.Count == 0)
        {
            DialogService.ShowInfo("No servers with commands to execute.", "Nothing to Run");
            return;
        }

        IsExecuting = true;
        OutputLines.Clear();
        ExecutionResults.Clear();
        HasExecutionResults = false;
        ExecutionSummary = string.Empty;
        _executionCts = new CancellationTokenSource();

        AddOutput("=== Execution started ===", "Info");
        StatusMessage = "Executing commands...";

        var settings = BuildSettings();
        var serverModels = Servers.Select(s =>
        {
            string? enc = null;
            if (s.UseCustomCredentials && !string.IsNullOrEmpty(s.CustomPassword))
                enc = CredentialService.Encrypt(s.CustomPassword);
            return s.ToModel(enc);
        }).ToList();

        var groupModels = Groups.Select(g => g.ToModel()).ToList();

        _executionService.ServerStarted += OnServerStarted;
        _executionService.CommandCompleted += OnCommandCompleted;
        _executionService.SessionCompleted += OnSessionCompleted;

        try
        {
            await _executionService.ExecuteAllAsync(serverModels, groupModels, settings, _executionCts.Token);
        }
        catch (OperationCanceledException)
        {
            AddOutput("=== Execution cancelled ===", "Warning");
            StatusMessage = "Execution cancelled";
        }
        catch (Exception ex)
        {
            AddOutput($"Fatal error: {ex.Message}", "Error");
            StatusMessage = "Execution failed";
        }
        finally
        {
            _executionService.ServerStarted -= OnServerStarted;
            _executionService.CommandCompleted -= OnCommandCompleted;
            _executionService.SessionCompleted -= OnSessionCompleted;
            IsExecuting = false;
            _executionCts?.Dispose();
            _executionCts = null;
        }
    }

    [RelayCommand]
    private void CancelExecution()
    {
        _executionCts?.Cancel();
        StatusMessage = "Cancelling...";
    }

    [RelayCommand]
    private void ClearOutput()
    {
        OutputLines.Clear();
        ExecutionResults.Clear();
        HasExecutionResults = false;
        ExecutionSummary = string.Empty;
    }

    [RelayCommand]
    private void LoadBatch()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Batch files (*.sshbatch;*.txt)|*.sshbatch;*.txt|All files (*.*)|*.*",
            DefaultExt = "sshbatch"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            _loadedBatchJob = BatchJobParser.ParseFile(dialog.FileName);
            BatchConnectionType = _loadedBatchJob.Defaults.ConnectionType;
            LoadedBatchSummary = $"{Path.GetFileName(dialog.FileName)} — {_loadedBatchJob.Targets.Count} target(s), {_loadedBatchJob.Steps.Count} step(s)";
            OnPropertyChanged(nameof(HasLoadedBatch));
            ExecuteBatchCommand.NotifyCanExecuteChanged();
            StatusMessage = $"Batch loaded: {_loadedBatchJob.Summary}";
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Batch Load Failed");
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteBatch))]
    private async Task ExecuteBatchAsync()
    {
        if (IsExecuting || _loadedBatchJob == null) return;

        IsExecuting = true;
        OutputLines.Clear();
        ExecutionResults.Clear();
        HasExecutionResults = false;
        ExecutionSummary = string.Empty;
        _executionCts = new CancellationTokenSource();

        var job = PrepareBatchJobForExecution(_loadedBatchJob);
        AddOutput($"=== Batch execution started: {job.Targets.Count} target(s), {job.Steps.Count} step(s), {job.Defaults.ConnectionType} port {job.Defaults.Port} ===", "Info");
        StatusMessage = "Running batch job...";

        _batchExecutionService.ServerStarted += OnServerStarted;
        _batchExecutionService.StepCompleted += OnCommandCompleted;
        _batchExecutionService.SessionCompleted += OnSessionCompleted;
        _batchExecutionService.OutputReceived += OnBatchOutputReceived;

        try
        {
            await _batchExecutionService.ExecuteAsync(job, BuildSettings(), _executionCts.Token);
        }
        catch (OperationCanceledException)
        {
            AddOutput("=== Batch execution cancelled ===", "Warning");
            StatusMessage = "Batch execution cancelled";
        }
        catch (Exception ex)
        {
            AddOutput($"Fatal error: {ex.Message}", "Error");
            StatusMessage = "Batch execution failed";
        }
        finally
        {
            _batchExecutionService.ServerStarted -= OnServerStarted;
            _batchExecutionService.StepCompleted -= OnCommandCompleted;
            _batchExecutionService.SessionCompleted -= OnSessionCompleted;
            _batchExecutionService.OutputReceived -= OnBatchOutputReceived;
            IsExecuting = false;
            _executionCts?.Dispose();
            _executionCts = null;
            ExecuteBatchCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanExecuteBatch() => _loadedBatchJob != null && !IsExecuting;

    private BatchJob PrepareBatchJobForExecution(BatchJob source)
    {
        return new BatchJob
        {
            SourceFile = source.SourceFile,
            Credential = source.Credential,
            Targets = source.Targets,
            Steps = source.Steps,
            Defaults = new BatchDefaults
            {
                ConnectionType = BatchConnectionType,
                Port = BatchPort,
                StepDelayOverrideMs = source.Defaults.StepDelayOverrideMs
            }
        };
    }

    partial void OnIsExecutingChanged(bool value) => ExecuteBatchCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void ExportData()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json",
            FileName = $"ssh-manager-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        };

        if (dialog.ShowDialog() != true) return;

        var data = new AppData
        {
            Settings = new AppSettings
            {
                DefaultUsername = DefaultUsername,
                DefaultPasswordEncrypted = string.IsNullOrEmpty(DefaultPassword)
                    ? null : CredentialService.Encrypt(DefaultPassword),
                ConnectionTimeoutSeconds = ConnectionTimeoutSeconds,
                CommandTimeoutSeconds = CommandTimeoutSeconds,
                BatchStepDelayMs = BatchStepDelayMs,
                Theme = AppTheme.Dark
            },
            Groups = Groups.Select(g => g.ToModel()).ToList(),
            Servers = Servers.Select(s =>
            {
                string? enc = null;
                if (s.UseCustomCredentials && !string.IsNullOrEmpty(s.CustomPassword))
                    enc = CredentialService.Encrypt(s.CustomPassword);
                return s.ToModel(enc);
            }).ToList()
        };

        _dataService.Export(data, dialog.FileName);
        StatusMessage = $"Exported to {Path.GetFileName(dialog.FileName)}";
        DialogService.ShowInfo("Export completed successfully.", "Export");
    }

    [RelayCommand]
    private void ImportData()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        var result = DialogService.ShowYesNo(
            "Import will replace all current servers, groups, and settings. Continue?",
            "Confirm Import",
            DialogKind.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var data = _dataService.Import(dialog.FileName);
            ApplyImportedData(data);
            MarkDirty();
            StatusMessage = $"Imported from {Path.GetFileName(dialog.FileName)}";
            DialogService.ShowInfo("Import completed successfully.", "Import");
        }
        catch (Exception ex)
        {
            DialogService.ShowError($"Import failed: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    private void BrowsePrivateKey()
    {
        var server = ActiveServer;
        if (server == null) return;
        var dialog = new OpenFileDialog
        {
            Filter = "Key files (*.pem;*.ppk;*.key)|*.pem;*.ppk;*.key|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            server.PrivateKeyPath = dialog.FileName;
            MarkDirty();
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var dialog = new SettingsDialog
        {
            Owner = Application.Current.MainWindow,
            DefaultUsername = DefaultUsername,
            DefaultPassword = DefaultPassword,
            ConnectionTimeout = ConnectionTimeoutSeconds,
            CommandTimeout = CommandTimeoutSeconds,
            BatchStepDelay = BatchStepDelayMs
        };

        if (dialog.ShowDialog() == true)
        {
            DefaultUsername = dialog.DefaultUsername;
            DefaultPassword = dialog.DefaultPassword;
            ConnectionTimeoutSeconds = dialog.ConnectionTimeout;
            CommandTimeoutSeconds = dialog.CommandTimeout;
            BatchStepDelayMs = dialog.BatchStepDelay;
            MarkDirty();
        }
    }

    public void OnServerFieldChanged() => MarkDirty();
    public void OnCommandFieldChanged() => MarkDirty();
    public void OnGroupFieldChanged() => MarkDirty();

    public void BeginContextMenuSelection() => _suppressTabOnSelect = true;

    public void EnsureServerEditorOpen(ServerItemViewModel server)
    {
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (!Servers.Contains(server)) return;

            if (!ReferenceEquals(SelectedServer, server))
                SelectedServer = server;
            else
                OpenOrSelectTab(server);
        });
    }

    private AppSettings BuildSettings() => new()
    {
        DefaultUsername = DefaultUsername,
        DefaultPasswordEncrypted = string.IsNullOrEmpty(DefaultPassword)
            ? null : CredentialService.Encrypt(DefaultPassword),
        ConnectionTimeoutSeconds = ConnectionTimeoutSeconds,
        CommandTimeoutSeconds = CommandTimeoutSeconds,
        BatchStepDelayMs = BatchStepDelayMs
    };

    private void ApplyImportedData(AppData data)
    {
        DefaultUsername = data.Settings.DefaultUsername;
        DefaultPassword = data.Settings.DefaultPasswordEncrypted != null
            ? CredentialService.Decrypt(data.Settings.DefaultPasswordEncrypted) : string.Empty;
        ConnectionTimeoutSeconds = data.Settings.ConnectionTimeoutSeconds;
        CommandTimeoutSeconds = data.Settings.CommandTimeoutSeconds;
        BatchStepDelayMs = data.Settings.BatchStepDelayMs > 0 ? data.Settings.BatchStepDelayMs : 500;

        Groups.Clear();
        foreach (var g in data.Groups.OrderBy(g => g.Order))
            Groups.Add(GroupItemViewModel.FromModel(g));

        RefreshGroupOptions();

        Servers.Clear();
        OpenServerTabs.Clear();
        SelectedTab = null;
        foreach (var s in data.Servers.OrderBy(s => s.Order))
        {
            var pwd = s.UseCustomCredentials && s.CustomPasswordEncrypted != null
                ? CredentialService.Decrypt(s.CustomPasswordEncrypted) : string.Empty;
            Servers.Add(ServerItemViewModel.FromModel(s, pwd));
        }
    }

    private void OnServerStarted(ServerExecutionResult result)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var displayName = ServerTargetResolver.GetExecutionDisplayName(result.ServerName, result.TargetHost);
            AddOutput($"▶ [{result.GroupName}] {displayName} — started", "Info");
            ExecutionResults.Add(new ExecutionServerViewModel
            {
                ServerName = result.ServerName,
                TargetHost = result.TargetHost,
                DisplayName = displayName,
                GroupName = result.GroupName,
                Status = ExecutionStatus.Running,
                IsExpanded = true
            });
            HasExecutionResults = true;
        });
    }

    private void OnCommandCompleted(CommandExecutionResult result, string displayName)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var serverVm = ExecutionResults.LastOrDefault(s => s.DisplayName == displayName);
            if (serverVm != null)
            {
                serverVm.Commands.Add(new ExecutionCommandViewModel
                {
                    CommandText = result.CommandText,
                    Status = result.Status,
                    Output = result.Output,
                    ErrorMessage = result.ErrorMessage,
                    Duration = result.Duration,
                    IsExpanded = result.Status == ExecutionStatus.Failed
                });
            }

            var icon = result.Status == ExecutionStatus.Success ? "✓" : "✗";
            AddOutput($"{icon} [{displayName}] {result.CommandText} ({result.Duration.TotalSeconds:F2}s)",
                result.Status == ExecutionStatus.Success ? "Success" : "Error");

            if (!string.IsNullOrEmpty(result.ErrorMessage))
                AddOutput($"  Error: {result.ErrorMessage}", "Error");
        });
    }

    private void OnSessionCompleted(ExecutionSession session)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var serverResult in session.Servers)
            {
                var displayName = ServerTargetResolver.GetExecutionDisplayName(
                    serverResult.ServerName, serverResult.TargetHost);
                var vm = ExecutionResults.FirstOrDefault(s => s.DisplayName == displayName);
                if (vm != null)
                {
                    vm.Status = serverResult.Status;
                    vm.Duration = serverResult.Duration;
                }
            }

            ExecutionSummary = $"Completed in {session.TotalDuration.TotalSeconds:F2}s | " +
                               $"Servers: {session.SuccessCount} OK, {session.FailedCount} failed | " +
                               $"Commands: {session.SuccessfulCommands}/{session.TotalCommands} OK";

            AddOutput($"=== {ExecutionSummary} ===", "Info");
            StatusMessage = "Execution completed";
        });
    }

    private void OnBatchOutputReceived(string line)
    {
        Application.Current.Dispatcher.Invoke(() => AddOutput(line, "Output"));
    }

    private void AddOutput(string text, string category)
    {
        OutputLines.Add(new OutputLineViewModel
        {
            Text = text,
            Category = category,
            Timestamp = DateTime.Now
        });
    }
}
