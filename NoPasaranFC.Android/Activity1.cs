using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;

namespace NoPasaranFC.Android;

[Activity(
    Label = "@string/app_name",
    MainLauncher = true,
    Icon = "@drawable/icon",
    AlwaysRetainTaskState = true,
    LaunchMode = LaunchMode.SingleInstance,
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize
)]
public class Activity1 : AndroidGameActivity
{
    private Game1 _game;

    protected override void OnCreate(Bundle bundle)
    {
        base.OnCreate(bundle);

        // Set fullscreen immersive mode
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat)
        {
            Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
                SystemUiFlags.LayoutStable |
                SystemUiFlags.LayoutHideNavigation |
                SystemUiFlags.LayoutFullscreen |
                SystemUiFlags.HideNavigation |
                SystemUiFlags.Fullscreen |
                SystemUiFlags.ImmersiveSticky
            );
        }

        // Initialize platform helper with Android context before creating game
        PlatformHelper.SetAndroidContext(this);

        _game = new Game1();
        SetContentView((View)_game.Services.GetService(typeof(View)));
        _game.Run();
    }
}
