using System.Reflection;
using CoreMotion;
using Foundation;
using Microsoft.Xna.Framework;
using UIKit;

namespace Durell.iOS;

/// <summary>
/// The app: starts the motion sensor; the game itself starts when iOS connects the window scene
/// (<see cref="SceneDelegate"/>) - current iOS refuses to run apps that don't use scenes.
/// </summary>
[Register("AppDelegate")]
internal sealed class Program : UIApplicationDelegate
{
    internal static readonly CMMotionManager Motion = new();

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        if (Motion.AccelerometerAvailable)
        {
            Motion.AccelerometerUpdateInterval = 1 / 60.0;
            Motion.StartAccelerometerUpdates();
        }
        return true;
    }

    public override UISceneConfiguration GetConfiguration(UIApplication application, UISceneSession session, UISceneConnectionOptions options) =>
        new("Default", session.Role) { DelegateType = typeof(SceneDelegate) };

    private static void Main(string[] args) => UIApplication.Main(args, null, typeof(Program));
}

/// <summary>Creates the game in the app's window scene and puts MonoGame's window into it.</summary>
[Register("SceneDelegate")]
internal sealed class SceneDelegate : UIResponder, IUIWindowSceneDelegate
{
    private static DurellGame? _game;

    [Export("window")]
    public UIWindow? Window { get; set; }

    [Export("scene:willConnectToSession:options:")]
    public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        if (scene is not UIWindowScene windowScene || _game != null) return;
        UIWindow? window = null;
        _game = new DurellGame
        {
            // Keep buttons clear of the notch / Dynamic Island and the home indicator.
            SafeInsetsProvider = () =>
            {
                var i = window?.SafeAreaInsets ?? UIEdgeInsets.Zero;
                double s = windowScene.Screen.NativeScale;
                return ((int)(i.Left * s), (int)(i.Top * s), (int)(i.Right * s), (int)(i.Bottom * s));
            },
            // Tilt: gravity turned from the device's portrait axes into the screen's.
            TiltProvider = () =>
            {
                var d = Program.Motion.AccelerometerData;
                if (d == null) return null;
                float x = (float)d.Acceleration.X, y = (float)d.Acceleration.Y, z = (float)d.Acceleration.Z;
                return windowScene.InterfaceOrientation switch
                {
                    UIInterfaceOrientation.LandscapeRight => new Vector3(-y, x, z),
                    UIInterfaceOrientation.LandscapeLeft => new Vector3(y, -x, z),
                    UIInterfaceOrientation.PortraitUpsideDown => new Vector3(-x, -y, z),
                    _ => new Vector3(x, y, z),
                };
            },
        };
        DurellGame.Diag("game created");
        window = GameWindow(_game);
        DurellGame.Diag(window == null ? "no MonoGame window found" : $"window {window.Frame}");
        if (window != null)
        {
            window.WindowScene = windowScene;
            window.Frame = windowScene.CoordinateSpace.Bounds;
            Window = window;
        }
        _game.Run();
        DurellGame.Diag("run returned");
    }

    /// <summary>MonoGame's own UIWindow (its iOS platform keeps it in a private field).</summary>
    private static UIWindow? GameWindow(Game game)
    {
        if (game.Services.GetService(typeof(UIWindow)) is UIWindow w) return w;
        var platform = typeof(Game).GetField("Platform", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(game)
                       ?? typeof(Game).GetProperty("Platform", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(game);
        return platform?.GetType().GetField("_mainWindow", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(platform) as UIWindow;
    }
}
