using Content.Goobstation.Shared.DoomArcade;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Goobstation.Client.DoomArcade;

public sealed class DoomArcadeBui : BoundUserInterface
{
    private DoomArcadeMenu? _menu;

    public DoomArcadeBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<DoomArcadeMenu>();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && _menu != null)
        {
            var score = _menu.GetScore();
            if (score > 0)
                SendMessage(new DoomArcadeScoreMessage(score));

            _menu.Close();
        }
    }
}
