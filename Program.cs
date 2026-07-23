if (args.Length > 0 && args[0] == "harness")
{
    // Headless AI test harness: run the match simulation without any rendering.
    NoPasaranFC.Harness.HarnessRunner.Run(args[1..]);
    return;
}

using var game = new NoPasaranFC.Game1();
game.Run();
