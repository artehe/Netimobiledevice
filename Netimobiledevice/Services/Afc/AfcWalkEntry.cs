using System.Collections.Generic;

namespace Netimobiledevice.Services.Afc;

/// <summary>
/// One step of <see cref="AfcService.WalkAsync"/>. Mutate <see cref="DirNames"/> to prune the descent.
/// </summary>
public sealed record AfcWalkEntry(
    string DirPath,
    List<string> DirNames,
    List<string> FileNames
);
