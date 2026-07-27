if (args.Length > 0 && args[0] == "harness")
{
    // Headless AI test harness: run the match simulation without any rendering.
    NoPasaranFC.Harness.HarnessRunner.Run(args[1..]);
    return;
}

// Desktop-only: start straight into the player/kit editor screen
NoPasaranFC.Game1.StartInEditor = System.Array.Exists(args, a => a == "--editor");

using var game = new NoPasaranFC.Game1();
game.Run();
