using System;
using IOPath = System.IO.Path;
using System.IO;
using Durell.Graphics.Art;

// usage: Durell.ArtLab OUT-DIR [name-prefix...]  - renders every piece in ArtCatalog to OUT-DIR/<name>.png
string outDir = IOPath.GetFullPath(args.Length > 0 ? args[0] : "artifacts/art");
Directory.CreateDirectory(outDir);
foreach (var (name, make) in ArtCatalog.All)
{
    if (args.Length > 1 && Array.FindIndex(args, 1, a => name.StartsWith(a, StringComparison.Ordinal)) < 0) continue;
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var c = make();
    c.SavePng(IOPath.Combine(outDir, name + ".png"));
    Console.WriteLine($"{name,-32} {c.Width}x{c.Height}  {sw.ElapsedMilliseconds} ms");
}
