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
    private CancellationTokenSource? _loadCts;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ServerDetailsReport? _report;
    [ObservableProperty] private string _windowTitle = "Server Details";
    [ObservableProperty] private string _statusMessage = string.Empty;

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

    public void PrepareForDisplay()
    {
        string? enc = null;
        if (_server.UseCustomCredentials && !string.IsNullOrEmpty(_server.CustomPassword))
            enc = CredentialService.Encrypt(_server.CustomPassword);

        Report = _service.CreatePlaceholderReport(_server.ToModel(enc), _groupName, _server.Commands.Count);
        IsLoading = true;
        StatusMessage = "Connecting and collecting system information...";
    }

    public async Task LoadAsync()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        IsLoading = true;
        StatusMessage = "Connecting and collecting system information...";

        try
        {
            string? enc = null;
            if (_server.UseCustomCredentials && !string.IsNullOrEmpty(_server.CustomPassword))
                enc = CredentialService.Encrypt(_server.CustomPassword);

            var model = _server.ToModel(enc);
            Report = await _service.CollectAsync(
                model, _settings, _groupName, _server.Commands.Count, token);

            StatusMessage = Report.IsSuccess
                ? $"Updated at {Report.CollectedAt:HH:mm:ss}"
                : Report.ErrorMessage ?? "Collection failed";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Collection cancelled";
        }
        finally
        {
            IsLoading = false;
            _loadCts?.Dispose();
            _loadCts = null;
        }
    }

    public void CancelLoading() => _loadCts?.Cancel();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading) return;
        await LoadAsync();
    }
}
