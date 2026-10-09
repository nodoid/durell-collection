using Android.App;
using Android.Content.PM;
using Android.Hardware;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Microsoft.Xna.Framework;

namespace Durell.Android;

[Activity(
    Label = "Durell Collection",
    MainLauncher = true,
    Icon = "@drawable/icon",
    Theme = "@style/Theme.Splash",
    AlwaysRetainTaskState = true,
    LaunchMode = LaunchMode.SingleInstance,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden |
                           ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize |
                           ConfigChanges.UiMode)]
public class MainActivity : Microsoft.Xna.Framework.AndroidGameActivity, ISensorEventListener
{
    private const float StandardGravity = 9.80665f;
    private DurellGame? _game;
    private SensorManager? _sensors;
    private Sensor? _accelerometer;
    private volatile bool _haveReading;
    private float _ax, _ay, _az;

    protected override void OnCreate(Bundle? bundle)
    {
        base.OnCreate(bundle);
        _sensors = (SensorManager?)GetSystemService(SensorService);
        _accelerometer = _sensors?.GetDefaultSensor(SensorType.Accelerometer);
        _game = new DurellGame
        {
            // keep the buttons clear of a camera cut-out
            SafeInsetsProvider = () =>
            {
                var cut = Window?.DecorView?.RootWindowInsets?.DisplayCutout;
                return cut == null ? (0, 0, 0, 0) : (cut.SafeInsetLeft, cut.SafeInsetTop, cut.SafeInsetRight, cut.SafeInsetBottom);
            },
            TiltProvider = Gravity,
        };
        var view = (View)_game.Services.GetService(typeof(View))!;
        SetContentView(view);
        _game.Run();
    }

    /// <summary>Tilt: the accelerometer measures the push against gravity in the device's natural
    /// axes; this is gravity itself, in g, in the screen's axes.</summary>
    private Vector3? Gravity()
    {
        if (!_haveReading) return null;
        float x = -_ax / StandardGravity, y = -_ay / StandardGravity, z = -_az / StandardGravity;
        return Display?.Rotation switch
        {
            SurfaceOrientation.Rotation90 => new Vector3(-y, x, z),
            SurfaceOrientation.Rotation180 => new Vector3(-x, -y, z),
            SurfaceOrientation.Rotation270 => new Vector3(y, -x, z),
            _ => new Vector3(x, y, z),
        };
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (_accelerometer != null) _sensors?.RegisterListener(this, _accelerometer, SensorDelay.Game);
    }

    protected override void OnPause()
    {
        _sensors?.UnregisterListener(this);
        _haveReading = false;
        base.OnPause();
    }

    public void OnSensorChanged(SensorEvent? e)
    {
        var v = e?.Values;
        if (v == null || v.Count < 3) return;
        _ax = v[0];
        _ay = v[1];
        _az = v[2];
        _haveReading = true;
    }

    public void OnAccuracyChanged(Sensor? sensor, [GeneratedEnum] SensorStatus accuracy)
    {
    }
}
