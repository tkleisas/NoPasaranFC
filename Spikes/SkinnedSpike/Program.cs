namespace SkinnedSpike;

public static class Program
{
    public static void Main(string[] args)
    {
        string glbPath = args.Length > 0 ? args[0] : "Fox.glb";
        if (!System.IO.File.Exists(glbPath))
        {
            // Fall back to the output directory (where the csproj copies the asset).
            string nextToExe = System.IO.Path.Combine(AppContext.BaseDirectory, System.IO.Path.GetFileName(glbPath));
            if (System.IO.File.Exists(nextToExe)) glbPath = nextToExe;
        }
        using var game = new SpikeGame(glbPath);
        game.Run();
    }
}
