using Microsoft.UI.Xaml.Controls;
using Parpa.Services;
using Parpa.ViewModels;

namespace Parpa.Views;

public sealed partial class DropView : UserControl
{
    public DropView() => InitializeComponent();

    /// <summary>Archivo → Bandeja, texto → nota, enlace → Portapapeles.</summary>
    public void Show(DropKind kind)
    {
        (DropGlyph.Glyph, DropLabel.Text) = kind switch
        {
            DropKind.File => ("bandeja", Loc.Get("drop_file")),
            DropKind.Text => ("notas", Loc.Get("drop_text")),
            _ => ("copias", Loc.Get("drop_link")),
        };
    }
}
