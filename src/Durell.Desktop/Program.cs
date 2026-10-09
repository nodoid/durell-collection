using Durell;

internal static class Program
{
    /// <param name="args"><c>--no-sound</c> starts muted (without changing the saved setting).</param>
    private static void Main(string[] args)
    {
        using var game = new DurellGame { StartMuted = System.Array.IndexOf(args, "--no-sound") >= 0 };
        game.Run();
    }
}
