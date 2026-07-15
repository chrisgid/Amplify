using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace Amplify.App.Controls;

/// <summary>
/// A reusable section header: a bold <see cref="Title"/> with an optional <see cref="Subtitle"/>
/// directly beneath it (no gap beyond line height), followed by a consistent gap before the
/// section's first card. Set <see cref="Title"/> via <c>x:Uid</c> (resource key <c>&lt;uid&gt;.Title</c>)
/// to localize it; supply the subtitle as the control's content (the <see cref="ContentPropertyAttribute"/>
/// default), leaving it empty for a title-only header.
/// </summary>
[ContentProperty(Name = nameof(Subtitle))]
public sealed partial class SectionHeader : UserControl
{
    /// <summary>Identifies the <see cref="Title"/> dependency property.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(SectionHeader),
        new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="Subtitle"/> dependency property.</summary>
    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle),
        typeof(object),
        typeof(SectionHeader),
        new PropertyMetadata(null));

    public SectionHeader() => InitializeComponent();

    /// <summary>The bold section title.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Optional content shown directly beneath the title (e.g. a status or description line).</summary>
    public object? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }
}
