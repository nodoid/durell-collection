using System;
using Durell.Audio;
using Durell.Games;
using Durell.Graphics;
using Durell.Input;
using Durell.Persistence;
using Durell.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell;

/// <summary>
/// The Durell Collection: Durell Software's Oric games - Harrier Attack, Scuba Dive, Star Fighter,
/// Galaxy and Lunar Lander - plus the Oric conversion of Turbo Esprit, each running its original
/// program (translated to native code), in an ENHANCED look and sound or as it was ORIGINALLY.
/// <para>Everything is laid out in virtual units: the screen is 360 units tall and 480 to 860 wide,
/// drawn straight to the back buffer at the display's resolution.</para>
/// </summary>
public sealed class DurellGame : Microsoft.Xna.Framework.Game
{
    public const float MinVirtualWidth = 480, MaxVirtualWidth = 860;

    private readonly GraphicsDeviceManager _graphics;
    private readonly SaveStore _store;
    private Gfx _gfx = null!;
    private Screen _screen = null!;
    private float _scale = 1, _virtualWidth = 640;
    private Vector2 _offset;
    private bool _resizing;
    private Point _windowedSize = new(1280, 800);
    private float _saveTimer;

    /// <param name="saveDirectory">Override for the save folder (tests, capture).</param>
    public DurellGame(string? saveDirectory = null) : this(saveDirectory, null, null)
    {
    }

    internal DurellGame(string? saveDirectory, ICaptureDirector? director, bool? mobileLayout)
    {
        Director = director;
        _graphics = new GraphicsDeviceManager(this) { GraphicsProfile = GraphicsProfile.HiDef, PreferMultiSampling = false };
        _store = new SaveStore(saveDirectory ?? SaveStore.DefaultDirectory());
        bool nativeMobile = OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();
        IsMobile = mobileLayout ?? nativeMobile;
        Input.IsMobileLayout = IsMobile;
        if (nativeMobile)
        {
            _graphics.IsFullScreen = true;
            _graphics.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;
        }
        else
        {
            _graphics.PreferredBackBufferWidth = _windowedSize.X;
            _graphics.PreferredBackBufferHeight = _windowedSize.Y;
            _graphics.HardwareModeSwitch = false;
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += OnClientSizeChanged;
            IsMouseVisible = true;
        }
        Window.Title = "The Durell Collection";
        IsFixedTimeStep = false;
        _graphics.SynchronizeWithVerticalRetrace = true;
    }

    public bool IsMobile { get; }
    /// <summary>The capture tool renders at an exact size into an offscreen target.</summary>
    internal Point? CaptureSize { get; init; }
    private RenderTarget2D? _captureTarget;
    internal RenderTarget2D? CaptureTarget => _captureTarget;
    internal ICaptureDirector? Director { get; }
    internal Screen CurrentScreen => _screen;
    /// <summary>Insets (device pixels) kept clear of notches and the home indicator.</summary>
    public Func<(int Left, int Top, int Right, int Bottom)>? SafeInsetsProvider { get; init; }
    /// <summary>Gravity in screen axes (x right, y up, z out of the screen; in g) from the accelerometer.</summary>
    public Func<Vector3?>? TiltProvider { get => Input.Tilt.Provider; init => Input.Tilt.Provider = value; }
    /// <summary>Starts with the sound off for this session only (desktop <c>--no-sound</c>).</summary>
    public bool StartMuted { get; init; }
    /// <summary>iOS apps must not quit themselves.</summary>
    public bool CanQuit => !IsMobile;

    public InputState Input { get; } = new();
    public AudioOut Audio { get; } = new();
    public SaveData Save { get; private set; } = new();
    public bool Enhanced => Save.Enhanced;
    public float Clock { get; private set; }
    internal Gfx Gfx => _gfx;
    internal FrameRenderer Renderer { get; private set; } = null!;
    internal Previews Previews { get; private set; } = null!;
    public float VirtualWidth => _virtualWidth;
    internal Vector2 GfxOffset => _offset;

    protected override void Initialize()
    {
        if (Director != null)
        {
            _graphics.SynchronizeWithVerticalRetrace = false;
            _graphics.ApplyChanges();
        }
        Diag("initialize");
        Save = _store.Load();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        Diag($"load content {GraphicsDevice.PresentationParameters.BackBufferWidth}x{GraphicsDevice.PresentationParameters.BackBufferHeight}");
        var font = new BitmapFont(GraphicsDevice);
        _gfx = new Gfx(GraphicsDevice, font);
        Kit.Scenery = new Scenery(GraphicsDevice);
        Renderer = new FrameRenderer(GraphicsDevice);
        Previews = new Previews(this);
        if (Director == null) Audio.Start();
        ApplySound();
        // phones and tablets: a splash screen while the games get ready (slow there); computers go straight to the menu
        ChangeScreen(IsMobile && Director == null ? new SplashScreen(this) : new MenuScreen(this));
    }

    protected override void UnloadContent()
    {
        Audio.Dispose();
        base.UnloadContent();
    }

    public void ChangeScreen(Screen screen)
    {
        _screen?.Leave();
        _screen = screen;
        _screen.Enter();
    }

    public void SetEnhanced(bool enhanced)
    {
        Save.Look = enhanced ? "Enhanced" : "Original";
        PersistSave();
    }

    public void SetTilt(bool on)
    {
        Save.Tilt = on;
        Input.Tilt.Centre();
        PersistSave();
    }

    /// <summary>Tilt steers this game: the device has an accelerometer, it's switched on and the game uses it.</summary>
    internal bool TiltPlays(GameInfo info) =>
        Save.Tilt && Input.Tilt.Available && (info.Controls.TiltX || info.Controls.TiltY || info.Controls.TiltPower);

    private bool _soundTouched;

    /// <summary>The sound is playing (the saved setting, unless this session started muted).</summary>
    public bool SoundOn => Save.Sound && !(StartMuted && !_soundTouched);

    public void SetSound(bool on)
    {
        _soundTouched = true;
        Save.Sound = on;
        if (on && Save.Volume < 0.05f) Save.Volume = 0.5f;
        ApplySound();
        PersistSave();
    }

    /// <summary>Raises or lowers the volume a step (0 to 100% in tens); 0 is sound off.</summary>
    public void StepVolume(int steps)
    {
        _soundTouched = true;
        int level = Math.Clamp(VolumeLevel + steps, 0, 10);
        Save.Volume = level / 10f;
        Save.Sound = level > 0;
        ApplySound();
        Audio.Beep();
        SaveSoon();
    }

    /// <summary>0 (off) to 10 (full).</summary>
    public int VolumeLevel => SoundOn ? (int)MathF.Round(Save.Volume * 10) : 0;

    /// <summary>"SOUND: 80%" / "SOUND: OFF".</summary>
    public string SoundLabel => VolumeLevel == 0 ? "SOUND: OFF" : $"SOUND: {VolumeLevel * 10}%";

    private void ApplySound()
    {
        Audio.Muted = !SoundOn;
        Audio.Volume = Save.Volume;
    }

    public void PersistSave() => _store.Save(Save);

    /// <summary>Saves soon (coalesces bursts of changes, e.g. a game updating its high-score table).</summary>
    public void SaveSoon() => _saveTimer = _saveTimer <= 0 ? 0.5f : _saveTimer;

    protected override void Update(GameTime gameTime)
    {
        if (!_diagUpdate) { _diagUpdate = true; Diag("first update"); }
        long nowTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_lastTicks != 0 && _slowLogs < 40)
        {
            double ms = (nowTicks - _lastTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (ms > 150)
            {
                _slowLogs++;
                Diag($"slow frame {ms:F0} ms ({_screen?.GetType().Name})");
            }
        }
        _lastTicks = nowTicks;
        Director?.BeforeUpdate(this);
        float dt = Director?.FixedDelta ?? (float)gameTime.ElapsedGameTime.TotalSeconds;
        dt = MathF.Min(dt, 0.1f);
        Clock += dt;
        UpdateLayout();
        KeepTouchInPixels();
        Input.Update(ScreenToVirtual);
        if (Input.Taps.Count > 0 && _diagTaps < 12)
        {
            _diagTaps++;
            Diag($"tap at virtual {Input.Taps[0].Position} (screen {_screen.GetType().Name})");
        }
        if (!IsMobile && Input.ToggleFullScreen) ToggleFullScreen();
        _screen.Update(dt);
        if (_saveTimer > 0 && (_saveTimer -= dt) <= 0) PersistSave();
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (!_diagDraw) { _diagDraw = true; Diag("first draw"); }
        UpdateLayout();
        if (CaptureSize is Point cs)
        {
            if (_captureTarget == null || _captureTarget.Width != cs.X || _captureTarget.Height != cs.Y)
            {
                _captureTarget?.Dispose();
                _captureTarget = new RenderTarget2D(GraphicsDevice, cs.X, cs.Y, false, SurfaceFormat.Color, DepthFormat.Depth24, 0, RenderTargetUsage.PreserveContents);
            }
            GraphicsDevice.SetRenderTarget(_captureTarget);
        }
        else GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        _gfx.Time = Clock;
        _gfx.BeginFrame(_virtualWidth, _scale, _offset);
        _screen.Draw(_gfx);
        _gfx.End();
        if (_captureTarget != null)
        {
            Director?.AfterDraw(this);
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(Color.Black);
            var pp = GraphicsDevice.PresentationParameters;
            float k = MathF.Min(pp.BackBufferWidth / (float)_captureTarget.Width, pp.BackBufferHeight / (float)_captureTarget.Height);
            _gfx.Batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
            _gfx.Batch.Draw(_captureTarget, new Rectangle(0, 0, (int)(_captureTarget.Width * k), (int)(_captureTarget.Height * k)), Color.White);
            _gfx.Batch.End();
        }
        else Director?.AfterDraw(this);
        SelfShot();
        base.Draw(gameTime);
    }

    // Device testing: launched with DURELL_SELFSHOT=seconds[,seconds...], the app saves its own screen
    // to Documents/selfshot-N.png at those times (no effect otherwise).
    private float[]? _shotTimes;
    private string? _selfPlay;
    private int _shotNext;

    /// <summary>Device testing: notes start-up stages in Documents/diag.txt when Documents/selfshot.txt exists.</summary>
    public static void Diag(string stage)
    {
        try
        {
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string trigger = System.IO.Path.Combine(dir, "selfshot.txt");
            if (!System.IO.File.Exists(trigger) || new System.IO.FileInfo(trigger).Length == 0) return;
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "diag.txt"), $"{DateTime.Now:HH:mm:ss.fff} {stage}\n");
        }
        catch (Exception)
        {
        }
    }

    private bool _diagUpdate, _diagDraw;
    private int _diagTaps, _slowLogs;
    private long _lastTicks;

    private void SelfShot()
    {
        if (_shotTimes == null)
        {
            var v = Environment.GetEnvironmentVariable("DURELL_SELFSHOT");
            try
            {
                // or a file Documents/selfshot.txt holding the times (copied onto a test device)
                string file = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "selfshot.txt");
                if (string.IsNullOrEmpty(v) && System.IO.File.Exists(file)) v = System.IO.File.ReadAllText(file).Trim();
                // an optional second line "play=GAME": start that game (at its preview moment) after 2 s
                if (v != null && v.Contains('\n'))
                {
                    var lines = v.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    v = lines[0];
                    foreach (var l in lines)
                        if (l.StartsWith("play=")) _selfPlay = l[5..];
                }
            }
            catch (Exception)
            {
            }
            _shotTimes = string.IsNullOrEmpty(v) ? Array.Empty<float>() : Array.ConvertAll(v.Split(','), x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture));
        }
        if (_selfPlay != null && Clock > 2)
        {
            var info = Catalog.Get(_selfPlay);
            _selfPlay = null;
            var ps = new PlayScreen(this, info) { ShowHints = false };
            var p = ps.Program;
            for (int f = 0; f < info.PreviewFrames; f++)
            {
                p.ClearKeys();
                foreach (var (a, b, ks) in info.PreviewKeys)
                    if (f >= a && f <= b) foreach (var k in ks) p.SetKey(k, true);
                p.RunFrame();
                p.SkipAudio();
            }
            p.ClearKeys();
            ChangeScreen(ps);
            Diag("playing " + info.Id);
        }
        if (_shotNext >= _shotTimes.Length || Clock < _shotTimes[_shotNext]) return;
        try
        {
            var pp = GraphicsDevice.PresentationParameters;
            var data = new Color[pp.BackBufferWidth * pp.BackBufferHeight];
            GraphicsDevice.GetBackBufferData(data);
            using var tex = new Texture2D(GraphicsDevice, pp.BackBufferWidth, pp.BackBufferHeight);
            tex.SetData(data);
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            using var f = System.IO.File.Create(System.IO.Path.Combine(dir, $"selfshot-{_shotNext}.png"));
            tex.SaveAsPng(f, pp.BackBufferWidth, pp.BackBufferHeight);
        }
        catch (Exception e)
        {
            try
            {
                string dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, $"selfshot-{_shotNext}.txt"), e.ToString());
            }
            catch (Exception)
            {
            }
        }
        _shotNext++;
    }

    /// <summary>
    /// Touches must come in back-buffer pixels, which is what the layout maps; on iOS MonoGame otherwise
    /// reports them in the window's points (a third of the pixels on a 3x phone), so every tap missed.
    /// </summary>
    private void KeepTouchInPixels()
    {
        if (!IsMobile) return;
        var pp = GraphicsDevice.PresentationParameters;
        if (Microsoft.Xna.Framework.Input.Touch.TouchPanel.DisplayWidth != pp.BackBufferWidth ||
            Microsoft.Xna.Framework.Input.Touch.TouchPanel.DisplayHeight != pp.BackBufferHeight)
        {
            Diag($"touch display {Microsoft.Xna.Framework.Input.Touch.TouchPanel.DisplayWidth}x{Microsoft.Xna.Framework.Input.Touch.TouchPanel.DisplayHeight} -> {pp.BackBufferWidth}x{pp.BackBufferHeight}, window {Window.ClientBounds}");
            Microsoft.Xna.Framework.Input.Touch.TouchPanel.DisplayWidth = pp.BackBufferWidth;
            Microsoft.Xna.Framework.Input.Touch.TouchPanel.DisplayHeight = pp.BackBufferHeight;
        }
    }

    private void UpdateLayout()
    {
        var pp = GraphicsDevice.PresentationParameters;
        int w = Math.Max(1, pp.BackBufferWidth), h = Math.Max(1, pp.BackBufferHeight);
        if (CaptureSize is Point cs)
        {
            w = cs.X;
            h = cs.Y;
        }
        float vw = Math.Clamp(Gfx.Height * w / h, MinVirtualWidth, MaxVirtualWidth);
        _scale = MathF.Min(w / vw, h / Gfx.Height);
        _virtualWidth = w / _scale;
        if (_virtualWidth > MaxVirtualWidth) _virtualWidth = MaxVirtualWidth;
        float vh = h / _scale;
        _offset = new Vector2((w - _virtualWidth * _scale) / 2, (h - Gfx.Height * _scale) / 2);
        if (vh < Gfx.Height - 0.5f) _offset.Y = 0;
        var inset = SafeInsetsProvider?.Invoke() ?? (0, 0, 0, 0);
        float l = inset.Left / _scale, t = inset.Top / _scale, r = inset.Right / _scale, b = inset.Bottom / _scale;
        if (_gfx != null) _gfx.Safe = new RectangleF(l, t, _virtualWidth - l - r, Gfx.Height - t - b);
    }

    private Vector2 ScreenToVirtual(Vector2 p) => (p - _offset) / _scale;

    private void ToggleFullScreen()
    {
        if (!_graphics.IsFullScreen)
        {
            _windowedSize = new Point(Window.ClientBounds.Width, Window.ClientBounds.Height);
            var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            _graphics.PreferredBackBufferWidth = mode.Width;
            _graphics.PreferredBackBufferHeight = mode.Height;
            _graphics.IsFullScreen = true;
        }
        else
        {
            _graphics.IsFullScreen = false;
            _graphics.PreferredBackBufferWidth = _windowedSize.X;
            _graphics.PreferredBackBufferHeight = _windowedSize.Y;
        }
        _resizing = true;
        _graphics.ApplyChanges();
        _resizing = false;
    }

    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        if (_resizing || _graphics.IsFullScreen) return;
        var b = Window.ClientBounds;
        if (b.Width <= 0 || b.Height <= 0) return;
        _resizing = true;
        _graphics.PreferredBackBufferWidth = b.Width;
        _graphics.PreferredBackBufferHeight = b.Height;
        _graphics.ApplyChanges();
        _resizing = false;
    }

    protected override void OnDeactivated(object sender, EventArgs args)
    {
        _screen?.OnDeactivated();
        PersistSave();
        base.OnDeactivated(sender, args);
    }

    protected override void OnActivated(object sender, EventArgs args)
    {
        _screen?.OnActivated();
        base.OnActivated(sender, args);
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        _screen?.OnDeactivated();
        PersistSave();
        base.OnExiting(sender, args);
    }
}

/// <summary>Drives the real app for store screenshots (tools/Durell.Capture). Never set in shipping builds.</summary>
internal interface ICaptureDirector
{
    float FixedDelta { get; }
    void BeforeUpdate(DurellGame game);
    void AfterDraw(DurellGame game);
}
