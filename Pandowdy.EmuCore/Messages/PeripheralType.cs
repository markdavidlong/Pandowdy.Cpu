// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Messages;

/// <summary>
/// Identifies the type of a peripheral device attached to an expansion card.
/// </summary>
/// <remarks>
/// Some combinations may seem unusual (e.g., a printer on a disk controller card),
/// but the receiving code can choose to ignore or handle discrepancies as appropriate.
/// </remarks>
public enum PeripheralType
{
    /// <summary>Unknown or unspecified device type.</summary>
    Unknown = 0,

    /// <summary>5.25" floppy disk drive (Disk II, etc.).</summary>
    Floppy525 = 1,

    /// <summary>3.5" floppy disk drive (UniDisk 3.5, etc.).</summary>
    Floppy35 = 2,

    /// <summary>Hard disk drive (ProFile, SCSI, etc.).</summary>
    HardDrive = 3,

    /// <summary>RAM disk or memory-based storage.</summary>
    RamDisk = 4,

    /// <summary>Printer device.</summary>
    Printer = 10,

    /// <summary>Modem or serial communication device.</summary>
    Modem = 11,

    /// <summary>Generic serial port.</summary>
    SerialPort = 12,

    /// <summary>Clock/calendar device.</summary>
    Clock = 20,

    /// <summary>Audio/sound device (Mockingboard, etc.).</summary>
    Audio = 30,
}
