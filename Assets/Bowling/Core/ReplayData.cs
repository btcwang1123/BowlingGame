using System.Collections.Generic;
using System.IO;
using UnityEngine;

// A recorded instant replay: the ball plus the 10 pins (11 objects, ball first), one pos+rot
// sample every 1/30 s, quantized to 16 bits per component. Small enough to eventually ship over
// the network — this is cosmetic playback data, not physics state, so the quantization error
// (~0.03 mm position, ~0.006° rotation) is far below what anyone could notice.
public class ReplayData
{
    public const int ObjectCount = 11;
    public const float FrameInterval = 1f / 30f;

    static readonly Vector2 XRange = new(-2f, 2f), YRange = new(-1.5f, 2f), ZRange = new(-2f, 22f);
    const float ComponentsPerObject = 6f; // pos.x,y,z + rot.x,y,z

    readonly ushort[] data; // frameCount * ObjectCount * 6 ushorts

    public int FrameCount { get; }

    ReplayData(ushort[] data, int frameCount)
    {
        this.data = data;
        FrameCount = frameCount;
    }

    // Build from raw per-frame samples (ball first, then pins 1..10, each frame length ObjectCount).
    public static ReplayData FromFrames(IReadOnlyList<(Vector3 pos, Quaternion rot)[]> frames)
    {
        var data = new ushort[frames.Count * ObjectCount * (int)ComponentsPerObject];
        int i = 0;
        foreach (var frame in frames)
            foreach (var (pos, rot) in frame)
            {
                Vector3 euler = rot.eulerAngles;
                data[i++] = Quantize(pos.x, XRange);
                data[i++] = Quantize(pos.y, YRange);
                data[i++] = Quantize(pos.z, ZRange);
                data[i++] = QuantizeAngle(euler.x);
                data[i++] = QuantizeAngle(euler.y);
                data[i++] = QuantizeAngle(euler.z);
            }
        return new ReplayData(data, frames.Count);
    }

    // Read back one frame as (position, rotation) per object, same 11-object order.
    public (Vector3 pos, Quaternion rot)[] GetFrame(int index)
    {
        var frame = new (Vector3 pos, Quaternion rot)[ObjectCount];
        int baseIdx = index * ObjectCount * (int)ComponentsPerObject;
        for (int i = 0; i < ObjectCount; i++)
        {
            int o = baseIdx + i * (int)ComponentsPerObject;
            Vector3 pos = new(Dequantize(data[o], XRange), Dequantize(data[o + 1], YRange), Dequantize(data[o + 2], ZRange));
            Quaternion rot = Quaternion.Euler(DequantizeAngle(data[o + 3]), DequantizeAngle(data[o + 4]), DequantizeAngle(data[o + 5]));
            frame[i] = (pos, rot);
        }
        return frame;
    }

    static ushort Quantize(float v, Vector2 range) =>
        (ushort)Mathf.RoundToInt(Mathf.InverseLerp(range.x, range.y, Mathf.Clamp(v, range.x, range.y)) * 65535f);
    static float Dequantize(ushort q, Vector2 range) => Mathf.Lerp(range.x, range.y, q / 65535f);

    // Both ends of the range are the same angle, so this wraps correctly.
    static ushort QuantizeAngle(float deg) => (ushort)Mathf.RoundToInt(Mathf.Repeat(deg, 360f) / 360f * 65535f);
    static float DequantizeAngle(ushort q) => q / 65535f * 360f;

    public byte[] Serialize()
    {
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        w.Write(FrameCount);
        foreach (ushort u in data) w.Write(u);
        return stream.ToArray();
    }

    public static ReplayData Deserialize(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var r = new BinaryReader(stream);
        int frameCount = r.ReadInt32();
        long expectedBytes = 4L + frameCount * ObjectCount * (long)ComponentsPerObject * 2L;
        if (frameCount < 0 || expectedBytes != bytes.Length)
            throw new InvalidDataException($"ReplayData.Deserialize: frameCount {frameCount} doesn't match {bytes.Length} bytes.");
        var data = new ushort[frameCount * ObjectCount * (int)ComponentsPerObject];
        for (int i = 0; i < data.Length; i++) data[i] = r.ReadUInt16();
        return new ReplayData(data, frameCount);
    }
}
