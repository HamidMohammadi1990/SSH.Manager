using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
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
    private readonly DispatcherTimer _clockTimer;
    private CancellationTokenSource? _executionCts;
    private bool _isDirty;
    private GroupItemViewModel? _watchedGroup;

    [ObservableProperty] private string _currentDate = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
    [ObservableProperty] private string _currentTime = DateTime.Now.ToString("HH:mm:ss");
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private bool _isExecuting;
    [ObservableProperty] private bool _isTestingConnections;
    [ObservableProperty] private string _defaultUsername = string.Empty;
    [ObservableProperty] private string _defaultPassword = string.Empty;
    [ObservableProperty] private int _connectionTimeoutSeconds = 30;
    [ObservableProperty] private int _commandTimeoutSeconds = 60;
    [ObservableProperty] private ServerItemViewModel? _selectedServer;
    [ObservableProperty] private GroupItemViewModel? _selectedGroup;
    [ObservableProperty] private CommandItemViewModel? _selectedCommand;
    [ObservableProperty] private string _executionSummary = string.Empty;
    [ObservableProperty] private bool _hasExecutionResults;
    [ObservableProperty] private AppTheme _currentTheme = AppTheme.Dark;

    public string ThemeToggleLabel => CurrentTheme == AppTheme.Dark ? "☀ Light" : "🌙 Dark";

    public ObservableCollection<ServerItemViewModel> Servers { get; } = new();
    public ObservableCollection<GroupItemViewModel> Groups { get; } = new();
    public ObservableCollection<GroupItemViewModel> GroupOptionsList { get; } = new();
    public ObservableCollection<OutputLineViewModel> OutputLines { get; } = new();
    public ObservableCollection<ExecutionServerViewModel> ExecutionResults { get; } = new();

    private static readonly GroupItemViewModel NoGroupOption = new() { Id = string.Empty, Name = "(No Group)" };

    public Array ConnectionTypes => Enum.GetValues(typeof(ConnectionType));

    public MainViewModel()
    {
        Groups.CollectionChanged += OnGroupsCollectionChanged;

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

    partial void OnCurrentThemeChanged(AppTheme value)
    {
        ThemeService.ApplyTheme(value);
        OnPropertyChanged(nameof(ThemeToggleLabel));
    }

    partial void OnSelectedGroupChanged(GroupItemViewModel? value)
    {
        if (_watchedGroup != null)
            _watchedGroup.PropertyChanged -= OnSelectedGroupPropertyChanged;

        _watchedGroup = value;

        if (_watchedGroup != null)
            _watchedGroup.PropertyChanged += OnSelectedGroupPropertyChanged;
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

    [RelayCommand]
    private void ToggleTheme()
    {
        CurrentTheme = CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        MarkDirty();
        StatusMessage = $"Theme switched to {CurrentTheme}";
    }

    partial void OnSelectedServerChanged(ServerItemViewModel? value)
    {
        if (value != null)
        {
            if (value.ConnectionType == ConnectionType.Ssh && value.Port == 23)
                value.Port = 22;
            if (value.ConnectionType == ConnectionType.Telnet && value.Port == 22)
                value.Port = 23;
        }
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
        CurrentTheme = data.Settings.Theme;

        Groups.Clear();
        foreach (var g in data.Groups.OrderBy(g => g.Order))
            Groups.Add(GroupItemViewModel.FromModel(g));

        RefreshGroupOptions();

        Servers.Clear();
        foreach (var s in data.Servers.OrderBy(s => s.Order))
        {
            var pwd = s.UseCustomCredentials && s.CustomPasswordEncrypted != null
                ? CredentialService.Decrypt(s.CustomPasswordEncrypted)
                : string.Empty;
            Servers.Add(ServerItemViewModel.FromModel(s, pwd));
        }

        _isDirty = false;
        StatusMessage = "Ready";
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
                Theme = CurrentTheme
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

        var result = MessageBox.Show(
            "You have unsaved changes. Would you like to save before exiting?\n\nClick 'Yes' to save, 'No' to exit without saving, or 'Cancel' to stay.",
            "Unsaved Changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

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
        var backup = MessageBox.Show(
            "Would you like to export a backup of your servers and settings before exiting?",
            "Export Backup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

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
        SelectedServer = server;
        MarkDirty();
    }

    [RelayCommand]
    private void RemoveServer()
    {
        if (SelectedServer == null) return;
        var result = MessageBox.Show(
            $"Remove server '{SelectedServer.Name}'?",
            "Confirm Remove",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        Servers.Remove(SelectedServer);
        ReorderServers();
        SelectedServer = Servers.FirstOrDefault();
        MarkDirty();
    }

    [RelayCommand]
    private void MoveServerUp()
    {
        if (SelectedServer == null) return;
        var idx = Servers.IndexOf(SelectedServer);
        if (idx <= 0) return;
        Servers.Move(idx, idx - 1);
        ReorderServers();
        MarkDirty();
    }

    [RelayCommand]
    private void MoveServerDown()
    {
        if (SelectedServer == null) return;
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
        var result = MessageBox.Show(
            $"Remove group '{SelectedGroup.Name}'? Servers in this group will become ungrouped.",
            "Confirm Remove",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
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
        if (SelectedServer == null) return;
        var cmd = new CommandItemViewModel { Text = "echo hello", Order = SelectedServer.Commands.Count };
        SelectedServer.Commands.Add(cmd);
        SelectedCommand = cmd;
        MarkDirty();
    }

    [RelayCommand]
    private void RemoveCommand()
    {
        if (SelectedServer == null || SelectedCommand == null) return;
        SelectedServer.Commands.Remove(SelectedCommand);
        ReorderCommands(SelectedServer);
        MarkDirty();
    }

    [RelayCommand]
    private void MoveCommandUp()
    {
        if (SelectedServer == null || SelectedCommand == null) return;
        var idx = SelectedServer.Commands.IndexOf(SelectedCommand);
        if (idx <= 0) return;
        SelectedServer.Commands.Move(idx, idx - 1);
        ReorderCommands(SelectedServer);
        MarkDirty();
    }

    [RelayCommand]
    private void MoveCommandDown()
    {
        if (SelectedServer == null || SelectedCommand == null) return;
        var idx = SelectedServer.Commands.IndexOf(SelectedCommand);
        if (idx < 0 || idx >= SelectedServer.Commands.Count - 1) return;
        SelectedServer.Commands.Move(idx, idx + 1);
        ReorderCommands(SelectedServer);
        MarkDirty();
    }

    private static void ReorderCommands(ServerItemViewModel server)
    {
        for (var i = 0; i < server.Commands.Count; i++)
            server.Commands[i].Order = i;
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
            MessageBox.Show("No servers with commands to execute.", "Nothing to Run",
                MessageBoxButton.OK, MessageBoxImage.Information);
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
                Theme = CurrentTheme
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
        MessageBox.Show("Export completed successfully.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ImportData()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        var result = MessageBox.Show(
            "Import will replace all current servers, groups, and settings. Continue?",
            "Confirm Import",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var data = _dataService.Import(dialog.FileName);
            ApplyImportedData(data);
            MarkDirty();
            StatusMessage = $"Imported from {Path.GetFileName(dialog.FileName)}";
            MessageBox.Show("Import completed successfully.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Import failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void BrowsePrivateKey()
    {
        if (SelectedServer == null) return;
        var dialog = new OpenFileDialog
        {
            Filter = "Key files (*.pem;*.ppk;*.key)|*.pem;*.ppk;*.key|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            SelectedServer.PrivateKeyPath = dialog.FileName;
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
            CommandTimeout = CommandTimeoutSeconds
        };

        if (dialog.ShowDialog() == true)
        {
            DefaultUsername = dialog.DefaultUsername;
            DefaultPassword = dialog.DefaultPassword;
            ConnectionTimeoutSeconds = dialog.ConnectionTimeout;
            CommandTimeoutSeconds = dialog.CommandTimeout;
            MarkDirty();
        }
    }

    public void OnServerFieldChanged() => MarkDirty();
    public void OnCommandFieldChanged() => MarkDirty();
    public void OnGroupFieldChanged() => MarkDirty();

    private AppSettings BuildSettings() => new()
    {
        DefaultUsername = DefaultUsername,
        DefaultPasswordEncrypted = string.IsNullOrEmpty(DefaultPassword)
            ? null : CredentialService.Encrypt(DefaultPassword),
        ConnectionTimeoutSeconds = ConnectionTimeoutSeconds,
        CommandTimeoutSeconds = CommandTimeoutSeconds
    };

    private void ApplyImportedData(AppData data)
    {
        DefaultUsername = data.Settings.DefaultUsername;
        DefaultPassword = data.Settings.DefaultPasswordEncrypted != null
            ? CredentialService.Decrypt(data.Settings.DefaultPasswordEncrypted) : string.Empty;
        ConnectionTimeoutSeconds = data.Settings.ConnectionTimeoutSeconds;
        CommandTimeoutSeconds = data.Settings.CommandTimeoutSeconds;
        CurrentTheme = data.Settings.Theme;

        Groups.Clear();
        foreach (var g in data.Groups.OrderBy(g => g.Order))
            Groups.Add(GroupItemViewModel.FromModel(g));

        RefreshGroupOptions();

        Servers.Clear();
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
            AddOutput($"▶ [{result.GroupName}] {result.ServerName} — started", "Info");
            ExecutionResults.Add(new ExecutionServerViewModel
            {
                ServerName = result.ServerName,
                GroupName = result.GroupName,
                Status = ExecutionStatus.Running,
                IsExpanded = true
            });
            HasExecutionResults = true;
        });
    }

    private void OnCommandCompleted(CommandExecutionResult result, string serverName)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var serverVm = ExecutionResults.LastOrDefault(s => s.ServerName == serverName);
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
            AddOutput($"{icon} [{serverName}] {result.CommandText} ({result.Duration.TotalSeconds:F2}s)",
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
                var vm = ExecutionResults.FirstOrDefault(s => s.ServerName == serverResult.ServerName);
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
