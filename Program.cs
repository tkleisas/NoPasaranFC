if (args.Length > 0 && args[0] == "harness")
{
    // Headless AI test harness: run the match simulation without any rendering.
    NoPasaranFC.Harness.HarnessRunner.Run(args[1..]);
    return;
}

if (args.Length > 0 && args[0] == "server")
{
    // Headless multiplayer match server (also available as NoPasaranFC.Server).
    NoPasaranFC.Networking.ServerHost.Run(args[1..]);
    return;
}

// Wayland sessions can expose both a native Wayland socket and XWayland.
// MonoGame/SDL may pick X11 by default; on some drivers its present loop then
// blocks after the first frame (invisible window while audio keeps playing).
// SDL reads the video driver at process start, so setting the variable after
// the runtime has started is too late: re-exec once with the driver set.
// Users can opt out by setting SDL_VIDEODRIVER themselves (e.g. x11).
if (System.OperatingSystem.IsLinux() &&
    string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SDL_VIDEODRIVER")) &&
    !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")) &&
    !string.IsNullOrEmpty(System.Environment.ProcessPath))
{
    try
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = System.Environment.ProcessPath,
            UseShellExecute = false,
            WorkingDirectory = System.Environment.CurrentDirectory,
        };

        var commandLine = System.Environment.GetCommandLineArgs();
        bool dotnetHost = System.IO.Path.GetFileNameWithoutExtension(startInfo.FileName)
            .Equals("dotnet", System.StringComparison.OrdinalIgnoreCase);
        for (int i = dotnetHost ? 0 : 1; i < commandLine.Length; i++)
            startInfo.ArgumentList.Add(commandLine[i]);

        startInfo.Environment["SDL_VIDEODRIVER"] = "wayland";
        // Prefer libdecor client-side decorations when available, so windowed
        // mode has a real title bar and can be moved/resized.
        if (string.IsNullOrEmpty(startInfo.Environment["SDL_VIDEO_WAYLAND_ALLOW_LIBDECOR"]))
            startInfo.Environment["SDL_VIDEO_WAYLAND_ALLOW_LIBDECOR"] = "1";

        using var gameProcess = System.Diagnostics.Process.Start(startInfo);
        if (gameProcess != null)
        {
            gameProcess.WaitForExit();
            return;
        }
    }
    catch
    {
        // Fall back to an in-process launch (may still work on some drivers).
    }
}

// Desktop-only: start straight into the player/kit editor screen
NoPasaranFC.Game1.StartInEditor = System.Array.Exists(args, a => a == "--editor");

using var game = new NoPasaranFC.Game1();
game.Run();
