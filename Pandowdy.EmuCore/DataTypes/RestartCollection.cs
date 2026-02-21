// // Copyright 2026 Mark D. Long
// // Licensed under the Apache License, Version 2.0
// // See LICENSE file for details
//
//

using System.Diagnostics;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.DataTypes;

public class RestartCollection(IEnumerable<IRestartable> restartables)
{
    private readonly IEnumerable<IRestartable> _restartables = restartables;

    public void RestartAll()
    {
        Debug.WriteLine($"Calling ResetAll() on ResetCollection ({_restartables.Count()} item(s))");

        foreach (var r in _restartables)
        {
            Debug.WriteLine($" ... resetting {r}");
            r.Restart();
        }
    }
}
