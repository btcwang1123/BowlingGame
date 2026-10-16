using UnityEngine;

// Real ten-pin dimensions in metres. The foul line is at z = 0 and the lane runs along +Z.
public static class BowlingLane
{
    public const float HalfWidth = 0.525f;      // lane is 1.05 m wide
    public const float HeadPinZ = 18.29f;       // foul line to head pin
    public const float PinSpacing = 0.3048f;    // 12 in between pin centres
    public const float LaneEndZ = 19.4f;        // end of the pin deck, then the pit

    // The 10 pin spots, row by row from the head pin.
    public static Vector3[] PinSpots()
    {
        var spots = new Vector3[10];
        float rowDepth = PinSpacing * Mathf.Sqrt(3f) / 2f;
        int i = 0;
        for (int row = 0; row < 4; row++)
            for (int k = 0; k <= row; k++)
                spots[i++] = new Vector3((k - row / 2f) * PinSpacing, 0f, HeadPinZ + row * rowDepth);
        return spots;
    }
}
