// Who drives the current turn. All three are purely local today (IsLocalTurn always true,
// OnRollFinished a no-op) — the real behaviour arrives with online play.

// Single device, single active player: solo Standard, Practice, Challenge.
public class LocalMatchDriver : IMatchDriver
{
    public bool IsLocalTurn => true;
    public void OnRollFinished(RollReport r) { }
}

// Hot-seat multiplayer, still entirely on one device. Functionally identical to LocalMatchDriver
// right now — kept as its own class because it's a distinct seam the online task will eventually
// want to treat differently (e.g. per-player analytics).
public class LocalMultiplayerMatchDriver : IMatchDriver
{
    public bool IsLocalTurn => true;
    public void OnRollFinished(RollReport r) { }
}

// Inert placeholder for the future online client (spec/online.md, task T32). Not implemented, and
// nothing constructs or uses this class yet — it only exists so IMatchDriver has its three
// implementations as the spec requires.
public class OnlineMatchDriver : IMatchDriver
{
    public bool IsLocalTurn => true;
    public void OnRollFinished(RollReport r) { }
}
