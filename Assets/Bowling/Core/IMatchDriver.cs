// Seam for who may act on the current turn and where a finished roll gets reported.
// Today every implementation (in Bowling.Gameplay) is purely local; this is where an online
// opponent's turn, or sending the roll to a server, will eventually hook in.
public interface IMatchDriver
{
    bool IsLocalTurn { get; }
    void OnRollFinished(RollReport r);
}
