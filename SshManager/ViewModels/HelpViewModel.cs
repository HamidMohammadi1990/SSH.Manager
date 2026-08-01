using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SshManager.Help;

namespace SshManager.ViewModels;

public partial class HelpBlockViewModel : ObservableObject
{
    public HelpBlockKind Kind { get; }
    public string Text { get; }
    public IReadOnlyList<string> Items { get; }

    public HelpBlockViewModel(HelpBlock block, bool isPersian)
    {
        Kind = block.Kind;
        Text = isPersian ? block.TextFa : block.TextEn;
        Items = isPersian ? block.ItemsFa : block.ItemsEn;
    }
}

public partial class HelpSectionViewModel : ObservableObject
{
    public string Id { get; }
    public string Icon { get; }
    public string Title { get; }

    public HelpSectionViewModel(HelpSection section, bool isPersian)
    {
        Id = section.Id;
        Icon = section.Icon;
        Title = isPersian ? section.TitleFa : section.TitleEn;
    }
}

public partial class HelpViewModel : ObservableObject
{
    [ObservableProperty] private HelpLanguage _language = HelpLanguage.English;
    [ObservableProperty] private HelpSectionViewModel? _selectedSection;

    public ObservableCollection<HelpSectionViewModel> Sections { get; } = new();
    public ObservableCollection<HelpBlockViewModel> Blocks { get; } = new();

    public bool IsPersian => Language == HelpLanguage.Persian;
    public bool IsEnglish => Language == HelpLanguage.English;
    public string WindowTitle => IsPersian ? "راهنمای SSH Manager" : "SSH Manager User Guide";
    public string LanguageToggleLabel => IsPersian ? "English" : "فارسی";
    public System.Windows.FlowDirection ContentFlowDirection =>
        IsPersian ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight;

    public HelpViewModel()
    {
        RefreshSections(selectFirst: true);
    }

    partial void OnSelectedSectionChanged(HelpSectionViewModel? value) => RefreshBlocks();

    partial void OnLanguageChanged(HelpLanguage value)
    {
        OnPropertyChanged(nameof(IsPersian));
        OnPropertyChanged(nameof(IsEnglish));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(LanguageToggleLabel));
        OnPropertyChanged(nameof(ContentFlowDirection));

        var selectedId = SelectedSection?.Id;
        RefreshSections(selectFirst: selectedId == null);
        if (selectedId != null)
            SelectedSection = Sections.FirstOrDefault(s => s.Id == selectedId) ?? Sections.FirstOrDefault();
        else
            RefreshBlocks();
    }

    [RelayCommand]
    private void ToggleLanguage() =>
        Language = Language == HelpLanguage.English ? HelpLanguage.Persian : HelpLanguage.English;

    [RelayCommand]
    private void SetEnglish() => Language = HelpLanguage.English;

    [RelayCommand]
    private void SetPersian() => Language = HelpLanguage.Persian;

    private void RefreshSections(bool selectFirst)
    {
        var selectedId = SelectedSection?.Id;
        Sections.Clear();
        foreach (var section in HelpContent.Sections)
            Sections.Add(new HelpSectionViewModel(section, IsPersian));

        if (selectFirst || selectedId == null)
            SelectedSection = Sections.FirstOrDefault();
        else
            SelectedSection = Sections.FirstOrDefault(s => s.Id == selectedId) ?? Sections.FirstOrDefault();
    }

    private void RefreshBlocks()
    {
        Blocks.Clear();
        if (SelectedSection == null)
            return;

        var section = HelpContent.Sections.FirstOrDefault(s => s.Id == SelectedSection.Id);
        if (section == null)
            return;

        foreach (var block in section.Blocks)
            Blocks.Add(new HelpBlockViewModel(block, IsPersian));
    }
}
