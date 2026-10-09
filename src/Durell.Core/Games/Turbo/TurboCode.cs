using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace Durell.Games.Turbo;

/// <summary>
/// The Oric port of Turbo Esprit, translated to C# (see TurboCode.g.cs).  The
/// port reads its menus, dashboard, city maps and tables from raw disk
/// sectors with a small resident reader at $0530; here that reader is native
/// and serves the same sectors from turbo.disk.
/// </summary>
internal sealed partial class TurboCode
{
    private const int SectorsPerTrack = 17;
    private static byte[]? _disk;
    private static int _firstTrack;

    public TurboCode()
    {
        WriteLimit = 0x10000;       // the ROM is switched off: RAM up to $FFFF
        if (_disk == null)
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("turbo.disk")!;
            using var z = new ZLibStream(s, CompressionMode.Decompress);
            using var ms = new MemoryStream();
            z.CopyTo(ms);
            var all = ms.ToArray();
            _firstTrack = all[0] | all[1] << 8;
            _disk = all.AsSpan(2).ToArray();
        }
    }

    /// <summary>DREAD: D_CNT sectors from D_TRK/D_SEC to D_DEST (whole pages), then RTS.</summary>
    private int DiskRead()
    {
        int trk = M[0x05F0], sec = M[0x05F1], cnt = M[0x05F2];
        int dest = M[0x05F3] | M[0x05F4] << 8;
        while (cnt-- > 0)
        {
            int o = ((trk - _firstTrack) * SectorsPerTrack + sec - 1) * 256;
            for (int i = 0; i < 256; i++)
            {
                int a = (dest + i) & 0xFFFF;
                M[a] = o >= 0 && o + i < _disk!.Length ? _disk[o + i] : (byte)0;
            }
            dest += 256;
            if (++sec == SectorsPerTrack + 1)
            {
                sec = 1;
                trk++;
            }
        }
        M[0x05F0] = (byte)trk;
        M[0x05F1] = (byte)sec;
        M[0x05F2] = 0;
        Cy += 2000;                 // a little time, as a disk read would take
        int lo = Pop();
        return ((Pop() << 8 | lo) + 1) & 0xFFFF;
    }
}
