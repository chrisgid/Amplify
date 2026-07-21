// Derived from CopyButton in the Microsoft WinUI Gallery.
// Copyright (c) Microsoft Corporation. Licensed under the MIT License.
// https://github.com/microsoft/WinUI-Gallery/blob/29f62479d5c046a0b854a5868e5a7cd484572d87/WinUIGallery/Controls/CopyButton.xaml.cs
//
// Modified for Amplify: the upstream screen-reader announcement went through the Gallery's
// UIHelper, which Amplify does not have, so it is inlined here against the automation peer;
// CopiedMessage defaults to empty and is supplied via x:Uid so the string stays localizable; and
// the upstream constructor setting DefaultStyleKey is dropped (see below).
// See THIRD-PARTY-NOTICES.md.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace Amplify.App.Controls;

/// <summary>
/// A button that plays a "copied" confirmation animation when clicked: the content shrinks away, a
/// checkmark pops in and holds, then the content returns. It does not touch the clipboard itself —
/// the copy is the caller's job, via <see cref="ButtonBase.Command"/> or a <c>Click</c> handler —
/// so this composes with the MVVM commands used elsewhere in the app.
/// </summary>
/// <remarks>
/// The animation lives in the control template (<c>CopyButton.xaml</c>) rather than in view-model
/// state, so the view-model only performs the copy. That template comes from the unkeyed style in
/// <c>CopyButton.xaml</c>, which <c>App.xaml</c> merges into the app's resources — hence no
/// <c>DefaultStyleKey</c> here: an implicit style overrides the inherited <see cref="Button"/>
/// default style on its own, and setting a key without a <c>Themes/Generic.xaml</c> to resolve it
/// against (as upstream does) would achieve nothing.
/// </remarks>
public sealed class CopyButton : Button
{
    /// <summary>Identifies the <see cref="CopiedMessage"/> dependency property.</summary>
    public static readonly DependencyProperty CopiedMessageProperty = DependencyProperty.Register(
        nameof(CopiedMessage),
        typeof(string),
        typeof(CopyButton),
        new PropertyMetadata(string.Empty));

    /// <summary>
    /// Announced to screen readers when the copy completes; the animation is purely visual, so
    /// without this the confirmation is invisible to assistive technology. Set it via <c>x:Uid</c>
    /// (resource key <c>&lt;uid&gt;.CopiedMessage</c>) to keep it localized.
    /// </summary>
    public string CopiedMessage
    {
        get => (string)GetValue(CopiedMessageProperty);
        set => SetValue(CopiedMessageProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        // Detach first: OnApplyTemplate can run more than once (a re-template re-applies it), and
        // without this the handler — and so the animation — would be attached twice over.
        Click -= OnCopyButtonClick;
        base.OnApplyTemplate();
        Click += OnCopyButtonClick;
    }

    private void OnCopyButtonClick(object sender, RoutedEventArgs e)
    {
        if (GetTemplateChild("CopyToClipboardSuccessAnimation") is not Storyboard storyboard)
        {
            return;
        }

        storyboard.Begin();
        Announce();
    }

    // Raises a UIA notification on this element so a screen reader speaks the confirmation. The peer
    // is null when no assistive technology is attached, in which case there is nothing to announce.
    private void Announce()
    {
        if (string.IsNullOrEmpty(CopiedMessage))
        {
            return;
        }

        AutomationPeer? peer = FrameworkElementAutomationPeer.FromElement(this);
        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.ActionCompleted,
            AutomationNotificationProcessing.ImportantMostRecent,
            CopiedMessage,
            "AmplifyCopiedToClipboard");
    }
}
