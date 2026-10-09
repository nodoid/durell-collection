using System;
using Durell.Machine;

namespace Durell.Programs;

/// <summary>
/// One game of the collection as the app sees it: frames of a 240 x 224 Oric picture
/// (<see cref="OricVideo.Index"/> format), sound, the Oric keyboard and the game's memory.
/// </summary>
internal abstract class GameProgram
{
    public abstract byte[] Index { get; }
    public abstract byte[] Memory { get; }
    public long Frame { get; protected set; }
    /// <summary>Set when the program could not go on (it is restarted by the play screen).</summary>
    public bool Faulted { get; protected set; }

    public abstract void Reset();
    public abstract void RunFrame();
    public abstract void RenderAudio(Span<float> stereo, bool enhanced);
    public abstract void SkipAudio();
    public abstract void ClearKeys();
    public abstract void SetKey(OricKey key, bool down);

    /// <summary>Told when the program reaches a probe address (see games.json).</summary>
    public Action<GameProgram, int>? Probe { get; set; }
}

/// <summary>A translated Oric program running with its hardware.</summary>
internal sealed class OricProgram : GameProgram
{
    private readonly OricMachine _m;

    public OricProgram(string name)
    {
        _m = ProgramCatalog.Create(name);
        _m.Cpu.ProbeHandler = a => Probe?.Invoke(this, a);
    }

    public OricMachine Machine => _m;
    public override byte[] Index => _m.Video.Index;
    public override byte[] Memory => _m.M;

    public override void Reset()
    {
        _m.Reset();
        Frame = 0;
        Faulted = false;
    }

    public override void RunFrame()
    {
        if (Faulted) return;
        try
        {
            _m.RunFrame();
            Frame++;
        }
        catch (Cpu6502.UntranslatedCodeException ex)
        {
            System.Diagnostics.Debug.WriteLine("Durell: " + ex.Message);
            Faulted = true;
        }
    }

    public override void RenderAudio(Span<float> stereo, bool enhanced)
    {
        if (Faulted) stereo.Clear();
        else _m.RenderAudio(stereo, enhanced);
    }

    public override void SkipAudio() => _m.SkipAudio();
    public override void ClearKeys() => _m.Bus.ClearKeys();
    public override void SetKey(OricKey key, bool down) => _m.Bus.SetKey(key, down);
}
