using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SshManager.Models;
using SshManager.Services;

namespace SshManager.ViewModels;

public partial class ServerItemViewModel : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString();
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _host = string.Empty;
    [ObservableProperty] private int _port = 22;
    [ObservableProperty] private ConnectionType _connectionType = ConnectionType.Ssh;
    [ObservableProperty] private string _groupId = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private DateTime _createdAt = DateTime.Now;
    [ObservableProperty] private int _order;
    [ObservableProperty] private bool _useCustomCredentials;
    [ObservableProperty] private string? _customUsername;
    [ObservableProperty] private string _customPassword = string.Empty;
    [ObservableProperty] private string? _privateKeyPath;
    [ObservableProperty] private ConnectionStatus _connectionStatus = ConnectionStatus.Unknown;

    partial void OnConnectionTypeChanged(ConnectionType value)
    {
        Port = value switch
        {
            ConnectionType.Ssh when Port == 23 => 22,
            ConnectionType.Telnet when Port == 22 => 23,
            _ => Port
        };
    }

    public ObservableCollection<CommandItemViewModel> Commands { get; } = new();
    public ObservableCollection<TargetItemViewModel> Targets { get; } = new();

    public ServerProfile ToModel(string? encryptedPassword = null)
    {
        return new ServerProfile
        {
            Id = Id,
            Name = Name,
            Host = Host,
            Port = Port,
            ConnectionType = ConnectionType,
            GroupId = string.IsNullOrEmpty(GroupId) ? null : GroupId,
            Description = Description,
            CreatedAt = CreatedAt,
            Order = Order,
            UseCustomCredentials = UseCustomCredentials,
            CustomUsername = CustomUsername,
            CustomPasswordEncrypted = encryptedPassword,
            PrivateKeyPath = PrivateKeyPath,
            Targets = Targets
                .Select(t => t.Host.Trim())
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Commands = Commands.Select((c, i) => c.ToModel(i)).ToList()
        };
    }

    public static ServerItemViewModel FromModel(ServerProfile model, string? decryptedPassword = null)
    {
        var vm = new ServerItemViewModel
        {
            Id = model.Id,
            Name = model.Name,
            Host = model.Host,
            Port = model.Port,
            ConnectionType = model.ConnectionType,
            GroupId = model.GroupId ?? string.Empty,
            Description = model.Description,
            CreatedAt = model.CreatedAt,
            Order = model.Order,
            UseCustomCredentials = model.UseCustomCredentials,
            CustomUsername = model.CustomUsername,
            CustomPassword = decryptedPassword ?? string.Empty,
            PrivateKeyPath = model.PrivateKeyPath
        };

        foreach (var cmd in model.Commands.OrderBy(c => c.Order))
            vm.Commands.Add(CommandItemViewModel.FromModel(cmd));

        foreach (var target in model.Targets)
            vm.Targets.Add(new TargetItemViewModel { Host = target });

        return vm;
    }
}

public partial class TargetItemViewModel : ObservableObject
{
    [ObservableProperty] private string _host = string.Empty;
}

public partial class CommandItemViewModel : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString();
    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private int _order;

    public CommandItem ToModel(int order) => new()
    {
        Id = Id,
        Text = Text,
        Order = order
    };

    public static CommandItemViewModel FromModel(CommandItem model) => new()
    {
        Id = model.Id,
        Text = model.Text,
        Order = model.Order
    };
}

public partial class GroupItemViewModel : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString();
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private int _order;

    public ServerGroup ToModel() => new() { Id = Id, Name = Name, Order = Order };

    public static GroupItemViewModel FromModel(ServerGroup model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Order = model.Order
    };
}

public partial class OutputLineViewModel : ObservableObject
{
    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private string _category = "Info";
    [ObservableProperty] private DateTime _timestamp = DateTime.Now;
}

public partial class ExecutionServerViewModel : ObservableObject
{
    [ObservableProperty] private string _serverName = string.Empty;
    [ObservableProperty] private string _targetHost = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _groupName = string.Empty;
    [ObservableProperty] private ExecutionStatus _status = ExecutionStatus.Pending;
    [ObservableProperty] private TimeSpan _duration;
    [ObservableProperty] private bool _isExpanded = true;

    public ObservableCollection<ExecutionCommandViewModel> Commands { get; } = new();
}

public partial class ExecutionCommandViewModel : ObservableObject
{
    [ObservableProperty] private string _commandText = string.Empty;
    [ObservableProperty] private ExecutionStatus _status = ExecutionStatus.Pending;
    [ObservableProperty] private string _output = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private TimeSpan _duration;
    [ObservableProperty] private bool _isExpanded;
}
