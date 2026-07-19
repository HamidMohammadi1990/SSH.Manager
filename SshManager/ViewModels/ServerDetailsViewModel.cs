using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SshManager.Models;
using SshManager.Services;

namespace SshManager.ViewModels;

public partial class ServerDetailsViewModel : ObservableObject
{
    private readonly ServerItemViewModel _server;
    private readonly AppSettings _settings;
    private readonly string _groupName;
    private readonly ServerDetailsService _service;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ServerDetailsReport? _report;
    [ObservableProperty] private string _windowTitle = "Server Details";

    public ServerDetailsViewModel(
        ServerItemViewModel server,
        AppSettings settings,
        string groupName,
        ServerDetailsService? service = null)
    {
        _server = server;
        _settings = settings;
        _groupName = groupName;
        _service = service ?? new ServerDetailsService();
        WindowTitle = $"Server Details — {server.Name}";
    }

    public async Task LoadAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        try
        {
            string? enc = null;
            if (_server.UseCustomCredentials && !string.IsNullOrEmpty(_server.CustomPassword))
                enc = CredentialService.Encrypt(_server.CustomPassword);

            var model = _server.ToModel(enc);
            Report = await _service.CollectAsync(model, _settings, _groupName, _server.Commands.Count);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public static string FormatPercent(double? value) =>
        value.HasValue ? $"{value.Value:0.#}%" : "—";
}
