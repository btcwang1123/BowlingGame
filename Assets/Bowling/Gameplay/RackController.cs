using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Owns racking (standard/practice/challenge leaves), sweeping fallen pins between balls, the
// moving-rack sway effect, and knockdown reads. The interesting bit of knockdown math — combining
// "is it down now" with "was it standing before this throw" and the foul flag — stays in
// RollReport.BuildKnockedMask so it's unit-testable without live Pin/Rigidbody objects; this class
// only reads the pins' current state.
public class RackController
{
    readonly Pin[] pins; // index 0 = pin 1 … index 9 = pin 10

    public RackController(Pin[] pins) => this.pins = pins;

    public void RackAll() => Rack(ChallengeLevel.Rack(1, 2, 3, 4, 5, 6, 7, 8, 9, 10), new int[0]);

    public void RackPractice(int leave)
    {
        int[] numbers = BowlingGame.PracticeLeaves[leave];
        if (numbers.Length == 0) numbers = RandomLeave();
        Rack(ChallengeLevel.Rack(numbers), new int[0]);
    }

    public void RackChallenge(ChallengeLevel challenge) => Rack(challenge.pins, challenge.targets, challenge.heavyPins);

    void Rack(bool[] present, int[] targets, bool heavy = false)
    {
        for (int i = 0; i < pins.Length; i++)
        {
            pins[i].ResetToRack();
            pins[i].SetTarget(targets.Contains(i + 1));
            pins[i].SetHeavy(heavy);
            if (!present[i]) pins[i].Remove();
        }
    }

    // A plausible leave: 1–4 pins from the back two rows, plus sometimes a middle pin.
    static int[] RandomLeave()
    {
        var pool = new List<int> { 4, 5, 6, 7, 8, 9, 10, 2, 3 };
        int count = Random.Range(1, 5);
        return pool.OrderBy(_ => Random.value).Take(count).ToArray();
    }

    public void SweepFallen()
    {
        foreach (var pin in pins) if (pin.gameObject.activeSelf && pin.IsDown) pin.Remove();
    }

    // Moving-rack challenge: the standing pins slide side to side until the ball is released.
    public void UpdateSway(ChallengeLevel challenge, float elapsedSinceLevelStart)
    {
        if (challenge.sway <= 0f) return;
        float x = challenge.sway * Mathf.Sin(2f * Mathf.PI * challenge.swaySpeed * elapsedSinceLevelStart);
        foreach (var pin in pins) if (pin.IsStanding) pin.Sway(new Vector3(x, 0f, 0f));
    }

    // Each pin's current down/up state — dumb and pure-reading; RollReport.BuildKnockedMask does the real work.
    public bool[] ComputeIsDown()
    {
        var down = new bool[pins.Length];
        for (int i = 0; i < pins.Length; i++) down[i] = pins[i].IsDown;
        return down;
    }

    // Over the foul line: pins knocked down don't count and go back up.
    public void ResetFouled(bool[] standingBefore)
    {
        for (int i = 0; i < standingBefore.Length; i++) if (standingBefore[i]) pins[i].ResetToRack();
    }
}
