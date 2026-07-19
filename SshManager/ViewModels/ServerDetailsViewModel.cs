using System.Windows.Threading;
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
    private readonly Dispatcher _uiDispatcher;
    private CancellationTokenSource? _loadCts;
    private bool _isClosed;

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private ServerDetailsReport? _report;
    [ObservableProperty] private string _windowTitle = "Server Details";
    [ObservableProperty] private string _statusMessage = "در حال بارگذاری اطلاعات سرور...";

    public ServerDetailsViewModel(
        ServerItemViewModel server,
        AppSettings settings,
        string groupName)
    {
        _server = server;
        _settings = settings;
        _groupName = groupName;
        _service = new ServerDetailsService();
        _uiDispatcher = Dispatcher.CurrentDispatcher;
        WindowTitle = $"Server Details — {server.Name}";
        Report = _service.CreatePlaceholderReport(_server.ToModel(), _groupName, _server.Commands.Count);
    }

    public async Task LoadAsync()
    {
        if (_isClosed) return;

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        await RunOnUiAsync(() =>
        {
            IsLoading = true;
            StatusMessage = "در حال اتصال و جمع‌آوری اطلاعات...";
        });

        try
        {
            var model = await Task.Run(() => BuildServerModel(), token).ConfigureAwait(false);
            if (_isClosed || token.IsCancellationRequested) return;

            var report = await _service.CollectAsync(
                model, _settings, _groupName, _server.Commands.Count, token).ConfigureAwait(false);
            if (_isClosed || token.IsCancellationRequested) return;

            await RunOnUiAsync(() =>
            {
                Report = report;
                StatusMessage = report.IsSuccess
                    ? $"بروزرسانی: {report.CollectedAt:HH:mm:ss}"
                    : report.ErrorMessage ?? "جمع‌آوری اطلاعات ناموفق بود";
            });
        }
        catch (OperationCanceledException)
        {
            await RunOnUiAsync(() => StatusMessage = "بارگذاری لغو شد");
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() =>
            {
                StatusMessage = ex.Message;
                if (Report != null)
                {
                    Report.ErrorMessage = ex.Message;
                    Report.IsSuccess = false;
                }
            });
        }
        finally
        {
            await RunOnUiAsync(() => IsLoading = false);
            _loadCts?.Dispose();
            _loadCts = null;
        }
    }

    private Task RunOnUiAsync(Action action)
    {
        if (_isClosed) return Task.CompletedTask;

        if (_uiDispatcher.CheckAccess())
        {
            if (!_isClosed) action();
            return Task.CompletedTask;
        }

        return _uiDispatcher.InvokeAsync(() =>
        {
            if (!_isClosed) action();
        }).Task;
    }

    private ServerProfile BuildServerModel()
    {
        string? enc = null;
        if (_server.UseCustomCredentials && !string.IsNullOrEmpty(_server.CustomPassword))
            enc = CredentialService.Encrypt(_server.CustomPassword);
        return _server.ToModel(enc);
    }

    public void CancelLoading()
    {
        _loadCts?.Cancel();
        _service.CancelActive();
    }

    public void OnDialogClosed()
    {
        if (_isClosed) return;
        _isClosed = true;
        CancelLoading();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading || _isClosed) return;
        await LoadAsync();
    }
}
