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

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private ServerDetailsReport? _report;
    [ObservableProperty] private string _windowTitle = "Server Details";
    [ObservableProperty] private string _statusMessage = "در حال بارگذاری اطلاعات سرور...";

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
        Report = _service.CreatePlaceholderReport(_server.ToModel(), _groupName, _server.Commands.Count);
    }

    public async Task LoadAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        IsLoading = true;
        StatusMessage = "در حال اتصال و جمع‌آوری اطلاعات...";

        try
        {
            var report = await Task.Run(async () =>
            {
                token.ThrowIfCancellationRequested();

                string? enc = null;
                if (_server.UseCustomCredentials && !string.IsNullOrEmpty(_server.CustomPassword))
                    enc = CredentialService.Encrypt(_server.CustomPassword);

                var model = _server.ToModel(enc);
                return await _service.CollectAsync(
                    model, _settings, _groupName, _server.Commands.Count, token).ConfigureAwait(false);
            }, token).ConfigureAwait(true);

            Report = report;
            StatusMessage = report.IsSuccess
                ? $"بروزرسانی: {report.CollectedAt:HH:mm:ss}"
                : report.ErrorMessage ?? "جمع‌آوری اطلاعات ناموفق بود";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "بارگذاری لغو شد";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            if (Report != null)
            {
                Report.ErrorMessage = ex.Message;
                Report.IsSuccess = false;
            }
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
