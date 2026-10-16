using System.Collections.Generic;
using UnityEngine;

public enum ObstacleKind { Block, Mover, Spinner, Bump }

// One obstacle on the lane. Positions are lane coordinates: x across (0 = centre), z from the foul line.
public struct ObstacleDef
{
    public ObstacleKind kind;
    public float x, z;
    public Vector3 size;     // Block/Mover: width, height, depth. Spinner: length. Bump: width, height, radius.
    public float amplitude;  // Mover: sideways travel (m)
    public float speed;      // Mover: cycles per second. Spinner: degrees per second.
    public float phase;      // Mover: 0–1 of a cycle. Spinner: starting angle.

    public static ObstacleDef Block(float x, float z, float width, float depth = 0.3f) =>
        new() { kind = ObstacleKind.Block, x = x, z = z, size = new Vector3(width, 0.35f, depth) };

    public static ObstacleDef Mover(float z, float width, float amplitude, float cyclesPerSecond, float phase = 0f) =>
        new() { kind = ObstacleKind.Mover, z = z, size = new Vector3(width, 0.35f, 0.25f), amplitude = amplitude, speed = cyclesPerSecond, phase = phase };

    public static ObstacleDef Spinner(float x, float z, float length, float degreesPerSecond, float startAngle = 0f) =>
        new() { kind = ObstacleKind.Spinner, x = x, z = z, size = new Vector3(length, 0.25f, 0.06f), speed = degreesPerSecond, phase = startAngle };

    public static ObstacleDef Bump(float z, float height = 0.03f) =>
        new() { kind = ObstacleKind.Bump, z = z, size = new Vector3(BowlingLane.HalfWidth * 2f, height, 0.3f) };
}

public enum ChallengeGoal
{
    ClearAll,      // knock down every pin within the balls allowed
    KnockTargets,  // knock down every gold pin (others don't matter)
    MinPins,       // knock down at least minPins with a single ball (the rack is reset each ball)
}

public class ChallengeLevel
{
    public string nameZh, nameEn, descZh, descEn;
    public bool[] pins = Rack(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
    public int[] targets = new int[0];      // pin numbers (1–10) that glow gold
    public ObstacleDef[] obstacles = new ObstacleDef[0];
    public ChallengeGoal goal = ChallengeGoal.ClearAll;
    public int minPins;
    public int balls = 2;
    public int threeStarBalls = 1, twoStarBalls = 2; // finish within this many balls for 3★ / 2★
    public BallType ballType = BallType.Normal;
    public bool heavyPins;
    public float sway, swaySpeed;           // moving rack: sideways travel (m) and cycles per second

    public string Name => GameSettings.Language == "zh" ? nameZh : nameEn;
    public string Description => GameSettings.Language == "zh" ? descZh : descEn;

    public int StarsFor(int ballsUsed) => ballsUsed <= threeStarBalls ? 3 : ballsUsed <= twoStarBalls ? 2 : 1;

    // Pin numbers 1–10 → which spots are filled.
    public static bool[] Rack(params int[] numbers)
    {
        var r = new bool[10];
        foreach (int n in numbers) r[n - 1] = true;
        return r;
    }
}

// The challenge list. Every level is checked for being possible (and not trivial) by Bowling > Challenge Check.
public static class ChallengeLevels
{
    static readonly bool[] Full = ChallengeLevel.Rack(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);

    public static readonly IReadOnlyList<ChallengeLevel> All = new List<ChallengeLevel>
    {
        new() { nameZh = "暖身", nameEn = "Warm-up", descZh = "兩球內打倒全部球瓶。", descEn = "Knock down every pin in two balls." },
        new() { nameZh = "左角落", nameEn = "Left corner", descZh = "補掉 7 號瓶。", descEn = "Pick up the 7 pin.",
            pins = ChallengeLevel.Rack(7), balls = 3, twoStarBalls = 2 },
        new() { nameZh = "右角落", nameEn = "Right corner", descZh = "補掉 10 號瓶。", descEn = "Pick up the 10 pin.",
            pins = ChallengeLevel.Rack(10), balls = 3 },
        new() { nameZh = "水桶", nameEn = "The bucket", descZh = "補掉 2-4-5-8。", descEn = "Pick up the 2-4-5-8 leave.",
            pins = ChallengeLevel.Rack(2, 4, 5, 8), balls = 3 },
        new() { nameZh = "中央的牆", nameEn = "Wall in the middle", descZh = "球道中間有一道牆，繞過它。", descEn = "A wall blocks the middle. Go around it.",
            obstacles = new[] { ObstacleDef.Block(0f, 6f, 0.16f) } },
        new() { nameZh = "窄道", nameEn = "Narrow lane", descZh = "球道變窄了，瞄準中間。", descEn = "The lane narrows. Aim true.",
            obstacles = new[] { ObstacleDef.Block(-0.37f, 9f, 0.32f, 4f), ObstacleDef.Block(0.37f, 9f, 0.32f, 4f) } },
        new() { nameZh = "移動牆", nameEn = "Moving wall", descZh = "抓準時機穿過移動的牆。", descEn = "Time your throw past the moving wall.",
            obstacles = new[] { ObstacleDef.Mover(10f, 0.4f, 0.3f, 0.3f) } },
        new() { nameZh = "金色角落", nameEn = "Golden corners", descZh = "打倒兩支金色球瓶（7 號和 10 號）。", descEn = "Knock down both gold pins (7 and 10).",
            targets = new[] { 7, 10 }, goal = ChallengeGoal.KnockTargets, balls = 3, threeStarBalls = 1, twoStarBalls = 2 },
        new() { nameZh = "旋轉擋板", nameEn = "Spinner", descZh = "旋轉的擋板守在球瓶前。", descEn = "A spinning bar guards the pins.",
            obstacles = new[] { ObstacleDef.Spinner(0f, 12f, 0.6f, 80f) } },
        new() { nameZh = "顛簸球道", nameEn = "Bumpy lane", descZh = "球道上有三個小丘。", descEn = "Three bumps on the lane.",
            obstacles = new[] { ObstacleDef.Bump(5f, 0.02f), ObstacleDef.Bump(8.5f, 0.02f), ObstacleDef.Bump(12f, 0.02f) } },
        new() { nameZh = "雙重移動牆", nameEn = "Double movers", descZh = "兩道牆往相反方向移動。", descEn = "Two walls moving in opposite directions.",
            obstacles = new[] { ObstacleDef.Mover(7f, 0.35f, 0.3f, 0.35f), ObstacleDef.Mover(12f, 0.35f, 0.3f, 0.35f, 0.5f) }, balls = 3 },
        new() { nameZh = "全力一擊", nameEn = "Power shot", descZh = "一球打倒至少 9 支（每球重新排瓶）。", descEn = "Knock down 9+ pins with one ball (re-racked each ball).",
            goal = ChallengeGoal.MinPins, minPins = 9, balls = 3, twoStarBalls = 2 },
        new() { nameZh = "迷宮", nameEn = "Maze", descZh = "左右交錯的牆，找出路線。", descEn = "Staggered walls. Find the line.",
            obstacles = new[] { ObstacleDef.Block(-0.3f, 5f, 0.24f), ObstacleDef.Block(0.3f, 9f, 0.24f), ObstacleDef.Block(-0.3f, 13f, 0.24f) } },
        new() { nameZh = "四角分瓶", nameEn = "Big four", descZh = "補掉 4-6-7-10。", descEn = "Pick up the 4-6-7-10 split.",
            pins = ChallengeLevel.Rack(4, 6, 7, 10), balls = 4, threeStarBalls = 2, twoStarBalls = 3 },
        new() { nameZh = "重量級球瓶", nameEn = "Heavy pins", descZh = "球瓶變成兩倍半重，用力一點！", descEn = "The pins weigh 2.5× as much. Hit them hard!",
            heavyPins = true },
        new() { nameZh = "炸彈球", nameEn = "Bomb ball", descZh = "這顆球碰到球瓶就會爆炸，一球炸掉 4-6-7-10。", descEn = "This ball explodes at the pins. Blast the 4-6-7-10 in one.",
            pins = ChallengeLevel.Rack(4, 6, 7, 10), ballType = BallType.Bomb },
        new() { nameZh = "移動球瓶", nameEn = "Moving rack", descZh = "整排球瓶左右移動，出手那一刻才會停下。", descEn = "The whole rack slides side to side and stops the moment you let go.",
            sway = 0.22f, swaySpeed = 0.35f },
        new() { nameZh = "重量級對決", nameEn = "Heavyweight", descZh = "超重球對重球瓶，還要穿過移動牆。", descEn = "Heavy ball vs heavy pins, past a moving wall.",
            ballType = BallType.Heavy, heavyPins = true, obstacles = new[] { ObstacleDef.Mover(10f, 0.35f, 0.3f, 0.3f) } },
        new() { nameZh = "終極挑戰", nameEn = "Final challenge", descZh = "穿過移動牆和旋轉擋板，打倒金色球瓶。", descEn = "Past the mover and the spinner, take down the gold pins.",
            targets = new[] { 1, 5 }, goal = ChallengeGoal.KnockTargets, balls = 3,
            obstacles = new[] { ObstacleDef.Mover(8f, 0.35f, 0.28f, 0.3f), ObstacleDef.Spinner(0f, 13f, 0.6f, -90f) } },
    };
}
