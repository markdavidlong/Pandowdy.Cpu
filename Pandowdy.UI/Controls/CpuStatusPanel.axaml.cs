// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using ReactiveUI.Avalonia;
using Pandowdy.UI.ViewModels;

namespace Pandowdy.UI.Controls;

/// <summary>
/// CPU status panel control displaying real-time processor state.
/// </summary>
/// <remarks>
/// Inherits from <see cref="ReactiveUserControl{TViewModel}"/> to enable ReactiveUI's
/// activation lifecycle, which triggers the ViewModel's <c>WhenActivated</c> subscriptions.
/// </remarks>
public partial class CpuStatusPanel : ReactiveUserControl<CpuStatusPanelViewModel>
{
    public CpuStatusPanel()
    {
        InitializeComponent();
    }
}
