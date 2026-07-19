using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace SshManager.ViewModels;

public partial class ServerTabViewModel : ObservableObject
{
    private readonly PropertyChangedEventHandler _nameChangedHandler;
    private bool _isDetached;

    public ServerItemViewModel Server { get; }

    public ServerTabViewModel(ServerItemViewModel server)
    {
        Server = server;
        _nameChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(ServerItemViewModel.Name))
                OnPropertyChanged(nameof(Title));
        };
        server.PropertyChanged += _nameChangedHandler;
    }

    public string Title => string.IsNullOrWhiteSpace(Server.Name) ? "New Server" : Server.Name;

    public void Detach()
    {
        if (_isDetached) return;
        _isDetached = true;
        Server.PropertyChanged -= _nameChangedHandler;
    }
}
