using System.Windows;
using System.Windows.Controls;
using SshManager.ViewModels;

namespace SshManager.Views;

public class HelpBlockTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HeadingTemplate { get; set; }
    public DataTemplate? ParagraphTemplate { get; set; }
    public DataTemplate? BulletsTemplate { get; set; }
    public DataTemplate? CodeTemplate { get; set; }
    public DataTemplate? NoteTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not HelpBlockViewModel block)
            return base.SelectTemplate(item, container);

        return block.Kind switch
        {
            Help.HelpBlockKind.Heading => HeadingTemplate,
            Help.HelpBlockKind.Paragraph => ParagraphTemplate,
            Help.HelpBlockKind.Bullets => BulletsTemplate,
            Help.HelpBlockKind.Code => CodeTemplate,
            Help.HelpBlockKind.Note => NoteTemplate,
            _ => ParagraphTemplate
        };
    }
}
