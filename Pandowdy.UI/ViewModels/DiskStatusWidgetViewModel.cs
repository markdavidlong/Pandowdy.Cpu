using System;
using Avalonia.Media;
using Pandowdy.EmuCore.Services;
using ReactiveUI;

namespace Pandowdy.UI.ViewModels;

/// <summary>
/// ViewModel for a single disk drive status widget.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Observable Properties:</strong> All properties are reactive and update
/// automatically when the underlying <see cref="DiskDriveStatusSnapshot"/> changes.
/// </para>
/// <para>
/// <strong>Color Coding:</strong> Uses red for read-only indicators to provide
/// visual feedback about write-protection status.
/// </para>
/// </remarks>
public class DiskStatusWidgetViewModel(DiskDriveStatusSnapshot initialSnapshot) : ReactiveObject
{
    private DiskDriveStatusSnapshot _snapshot = initialSnapshot;

    /// <summary>
    /// Updates the widget with a new snapshot.
    /// </summary>
    public void UpdateSnapshot(DiskDriveStatusSnapshot snapshot)
    {
        _snapshot = snapshot;
        this.RaisePropertyChanged(nameof(DiskId));
        this.RaisePropertyChanged(nameof(Filename));
        this.RaisePropertyChanged(nameof(DiskImagePathTooltip));
        this.RaisePropertyChanged(nameof(FilenameForeground));
        this.RaisePropertyChanged(nameof(TrackSectorText));
        this.RaisePropertyChanged(nameof(TrackSectorForeground));
        this.RaisePropertyChanged(nameof(PhaseText));
        this.RaisePropertyChanged(nameof(MotorText));
    }

    /// <summary>
    /// Gets the disk identifier (e.g., "S6D1").
    /// </summary>
    public string DiskId => _snapshot.DiskId;

    /// <summary>
    /// Gets the filename without path, or "(empty)" if no disk.
    /// </summary>
    public string Filename =>
        string.IsNullOrEmpty(_snapshot.DiskImageFilename) ? "(empty)" : _snapshot.DiskImageFilename;

    /// <summary>
    /// Gets the full disk image path for tooltip display.
    /// </summary>
    public string DiskImagePathTooltip =>
        string.IsNullOrEmpty(_snapshot.DiskImagePath) ? "No disk inserted" : _snapshot.DiskImagePath;

    /// <summary>
    /// Gets the foreground color for filename (red if read-only).
    /// </summary>
    public IBrush FilenameForeground =>
        _snapshot.IsReadOnly ? Brushes.Red : Brushes.White;

    /// <summary>
    /// Gets the track/sector display text.
    /// </summary>
    /// <remarks>
    /// Format: "T#.## S##" or "T-- S--" if no disk.
    /// </remarks>
    public string TrackSectorText
    {
        get
        {
            if (string.IsNullOrEmpty(_snapshot.DiskImageFilename))
            {
                return "T-- S--";
            }

            string trackStr = $"T:{_snapshot.Track:F2}";
            string sectorStr = _snapshot.Sector >= 0 ? $"S:{_snapshot.Sector:D2}" : "S:--";
            return $"{trackStr} {sectorStr}";
        }
    }

    /// <summary>
    /// Gets the foreground color for track/sector (red if read-only).
    /// </summary>
    public IBrush TrackSectorForeground =>
        _snapshot.IsReadOnly ? Brushes.Red : Brushes.White;

    /// <summary>
    /// Gets the phase display text.
    /// </summary>
    /// <remarks>
    /// Format: "----" (all off) to "++++" (all on).
    /// Each character represents one phase (0-3).
    /// </remarks>
    public string PhaseText
    {
        get
        {
            char p0 = (_snapshot.PhaseState & 0b0001) != 0 ? '+' : '-';
            char p1 = (_snapshot.PhaseState & 0b0010) != 0 ? '+' : '-';
            char p2 = (_snapshot.PhaseState & 0b0100) != 0 ? '+' : '-';
            char p3 = (_snapshot.PhaseState & 0b1000) != 0 ? '+' : '-';
            return $"Ï•:{p0}{p1}{p2}{p3}";
        }
    }

    /// <summary>
    /// Gets the motor status display text.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>"âŒš" = Motor-off scheduled (delayed)</item>
    /// <item>"âš¡" = Motor running</item>
    /// </list>
    /// </remarks>
    public string MotorText
    {
        get
        {
            var status = "";
            if (_snapshot.MotorOffScheduled)
            {
                status += "âŒš";
            }
            if (_snapshot.MotorOn)
            {
                status += "âš¡";
            }
            return status;
        }
    }
}
