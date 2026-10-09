using Durell.Graphics;

namespace Durell.Screens;

public abstract class Screen
{
    protected Screen(DurellGame game) => Game = game;

    protected DurellGame Game { get; }

    public virtual void Enter() { }
    public virtual void Leave() { }
    public abstract void Update(float dt);
    public abstract void Draw(Gfx g);
    /// <summary>The app lost focus or is going to the background.</summary>
    public virtual void OnDeactivated() { }
    public virtual void OnActivated() { }
}
