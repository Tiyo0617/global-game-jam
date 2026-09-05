using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace GGJ;

/// <summary>
/// ⚠️ 临时工具：程序合成 3 首 8-bit 休闲风循环 BGM 的 WAV 文件（占位音乐）。
/// 输出到 audio/bgm/。运行：godot --headless --script res://Tests/GenBgm.cs
/// </summary>
public partial class GenBgm : SceneTree
{
    private const int SampleRate = 44100;
    private bool _ran;

    // 中文卡名场景无关，这里纯英文输出避免乱码
    public override bool _Process(double delta)
    {
        if (_ran) return true;
        _ran = true;

        Directory.CreateDirectory("audio/bgm");

        // 曲 A：轻快主菜单（120 BPM，C 大调五声，方波主奏）
        Gen(
            "audio/bgm/bgm_menu_a.wav", 120,
            MelodyA(), new[] { 48, 48, 43, 43, 45, 45, 41, 41 },
            Square, Triangle, hats: true);

        // 曲 B：舒缓版（90 BPM，三角波主奏，无鼓，更柔和）
        Gen(
            "audio/bgm/bgm_menu_b.wav", 90,
            MelodyA(), new[] { 48, 45, 43, 41 },
            Triangle, Sine, hats: false);

        // 曲 C：活泼版（140 BPM，A 小调五声，跳音旋律）
        Gen(
            "audio/bgm/bgm_menu_c.wav", 140,
            MelodyC(), new[] { 45, 45, 48, 48 },
            Square, Triangle, hats: true);

        GD.Print("[GenBgm] done: audio/bgm/bgm_menu_a/b/c.wav");
        return true;
    }

    // ==================== 旋律数据（八分音符粒度，(MIDI音高, 时值)），0 = 休止 ====================

    private static (int, int)[] MelodyA()
    {
        // C 大调五声（C D E G A），8 小节 × 2 遍
        var a = new List<(int, int)>
        {
            (72,1),(76,1),(79,1),(76,1),(72,2),(67,2),
            (74,1),(76,1),(79,2),(76,1),(74,1),(72,2),
            (69,1),(72,1),(76,2),(79,2),(76,2),
            (74,2),(72,2),(67,4),
            (72,1),(76,1),(79,1),(81,1),(79,2),(76,2),
            (74,1),(76,1),(79,2),(81,2),(79,2),
            (76,1),(74,1),(72,1),(69,1),(67,2),(72,2),
            (74,2),(72,4),
        };
        var all = new List<(int, int)>(a);
        all.AddRange(a);   // ×2 = 16 小节
        return all.ToArray();
    }

    private static (int, int)[] MelodyC()
    {
        // A 小调五声（A C D E G），更快更跳，8 小节
        return new (int, int)[]
        {
            (81,1),(79,1),(81,1),(84,1),(81,1),(79,1),(76,2),
            (79,1),(76,1),(79,1),(81,1),(79,1),(76,1),(74,2),
            (76,1),(74,1),(76,1),(79,1),(76,1),(74,1),(72,2),
            (69,2),(72,2),(76,4),
            (84,1),(81,1),(84,1),(88,1),(84,2),(81,2),
            (79,1),(81,1),(84,2),(81,2),(79,2),
            (76,1),(79,1),(76,1),(74,1),(72,2),(69,2),
            (72,2),(76,2),(72,4),
        };
    }

    // ==================== 合成 ====================

    private static double Freq(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);
    private static float Square(double phase) => phase % 1.0 < 0.5 ? 1f : -1f;
    private static float Triangle(double phase) => (float)(2.0 * Math.Abs(2.0 * (phase % 1.0) - 1.0) - 1.0);
    private static float Sine(double phase) => (float)Math.Sin(2 * Math.PI * (phase % 1.0));

    private static void Gen(
        string path, int bpm, (int midi, int len)[] melody, int[] bassPerBar,
        Func<double, float> lead, Func<double, float> bass, bool hats)
    {
        double spb = 60.0 / bpm;              // 秒/拍
        double spe = spb / 2;                 // 秒/八分音符
        int bars = 16;
        int total = (int)(bars * 8 * spe * SampleRate);
        var buf = new float[total];
        var rand = new Random(20260905);      // 固定 seed，hat 噪声可复现

        // ---- 主旋律 ----
        int cursor = 0;   // 八分音符游标
        foreach (var (midi, len) in melody)
        {
            if (midi > 0)
            {
                double dur = len * spe;
                int start = (int)(cursor * spe * SampleRate);
                int n = (int)(dur * SampleRate);
                double f = Freq(midi);
                for (int i = 0; i < n && start + i < total; i++)
                {
                    double t = (double)i / SampleRate;
                    // 包络：5% 起音，随后指数衰减（防爆音 + 8-bit 颗粒感）
                    double env = i < n * 0.05
                        ? (double)i / (n * 0.05)
                        : Math.Exp(-2.5 * (i - n * 0.05) / n);
                    buf[start + i] += 0.32f * lead(t * f) * (float)env;
                }
            }
            cursor += len;
        }

        // ---- 贝斯（每小节拍 1、3 各一个根音）----
        for (int bar = 0; bar < bars; bar++)
        {
            int root = bassPerBar[bar % bassPerBar.Length];
            for (int half = 0; half < 2; half++)
            {
                double dur = 2 * spb;
                int start = (int)((bar * 4 + half * 2) * spb * SampleRate);
                int n = (int)(dur * SampleRate);
                double f = Freq(root);
                for (int i = 0; i < n && start + i < total; i++)
                {
                    double t = (double)i / SampleRate;
                    double env = Math.Exp(-2.0 * i / n);
                    buf[start + i] += 0.22f * bass(t * f) * (float)env;
                }
            }
        }

        // ---- 鼓：kick 每拍（正弦下滑），hat 每个八分（噪声）----
        for (int k = 0; k < bars * 4; k++)
        {
            int start = (int)(k * spb * SampleRate);
            int n = (int)(0.08 * SampleRate);
            for (int i = 0; i < n && start + i < total; i++)
            {
                double t = (double)i / SampleRate;
                buf[start + i] += 0.45f * (float)Math.Sin(2 * Math.PI * (110 - 700 * t) * t) * (float)Math.Exp(-22 * t);
            }
        }
        if (hats)
        {
            for (int h = 0; h < bars * 8; h++)
            {
                int start = (int)(h * spe * SampleRate);
                int n = (int)(0.03 * SampleRate);
                for (int i = 0; i < n && start + i < total; i++)
                {
                    double t = (double)i / SampleRate;
                    buf[start + i] += 0.10f * (float)(rand.NextDouble() * 2 - 1) * (float)Math.Exp(-70 * t);
                }
            }
        }

        // ---- 归一化防削波 ----
        float peak = 0f;
        foreach (var s in buf) peak = Math.Max(peak, Math.Abs(s));
        if (peak > 0.001f)
        {
            float g = 0.85f / peak;
            for (int i = 0; i < buf.Length; i++) buf[i] *= g;
        }

        WriteWav(path, buf);
        GD.Print($"[GenBgm] wrote {path} ({buf.Length / SampleRate}s)");
    }

    // ==================== WAV 写出（16-bit PCM mono）====================

    private static void WriteWav(string path, float[] samples)
    {
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        int dataLen = samples.Length * 2;

        bw.Write("RIFF"u8);
        bw.Write(36 + dataLen);
        bw.Write("WAVE"u8);
        bw.Write("fmt "u8);
        bw.Write(16);                 // fmt 块长
        bw.Write((short)1);           // PCM
        bw.Write((short)1);           // mono
        bw.Write(SampleRate);
        bw.Write(SampleRate * 2);     // 字节率
        bw.Write((short)2);           // 块对齐
        bw.Write((short)16);          // 位深
        bw.Write("data"u8);
        bw.Write(dataLen);
        foreach (var s in samples)
            bw.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f));
    }
}
