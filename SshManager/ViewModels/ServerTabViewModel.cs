using CommunityToolkit.Mvvm.ComponentModel;
using SshManager.Models;

namespace SshManager.ViewModels;

public partial class ServerTabViewModel : ObservableObject
{
    public ServerItemViewModel Server { get; }

    public ServerTabViewModel(ServerItemViewModel server)
    {
        Server = server;
        server.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ServerItemViewModel.Name))
                OnPropertyChanged(nameof(Title));
        };
    }

    public string Title => string.IsNullOrWhiteSpace(Server.Name) ? "New Server" : Server.Name;
}
