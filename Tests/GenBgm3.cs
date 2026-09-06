using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace GGJ;

/// <summary>
/// ⚠️ 临时工具 v2：合成 4 首"对决风格"游戏 BGM（riff 驱动 / A 小调暗色 / 锯齿波 / 压迫低音）。
///   bgm_game_1.wav 110 BPM 行进 riff（轮 1~2）
///   bgm_game_2.wav 125 BPM 安达卢西亚进行 Am-G-F-E（轮 3~4）
///   bgm_game_3.wav 140 BPM 和声小调 + G# 导音 + 失谐双锯齿（轮 5~6）
///   bgm_game_4.wav 155 BPM 压迫 pedal 低音 + 全密度鼓（轮 7~8）
/// 输出：audio/bgm/bgm_game_1~4.wav（各 16 小节循环）。
/// 运行：godot --headless --script res://Tests/GenBgm3.cs
/// </summary>
public partial class GenBgm3 : SceneTree
{
    private const int SampleRate = 44100;
    private bool _ran;

    public override bool _Process(double delta)
    {
        if (_ran) return true;
        _ran = true;

        Directory.CreateDirectory("audio/bgm");

        GenGame("audio/bgm/bgm_game_1.wav", 110, Riff1(), Bass1(),
            detune: false, pedalNote: -1, drumLevel: 1, hatEvery: 1, smooth: 0.35f);
        GenGame("audio/bgm/bgm_game_2.wav", 125, Riff2(), Bass2(),
            detune: false, pedalNote: -1, drumLevel: 2, hatEvery: 1, smooth: 0.45f);
        GenGame("audio/bgm/bgm_game_3.wav", 140, Riff3(), Bass3(),
            detune: true, pedalNote: -1, drumLevel: 3, hatEvery: 1, smooth: 0.55f);
        GenGame("audio/bgm/bgm_game_4.wav", 155, Riff4(), Bass4(),
            detune: true, pedalNote: 33, drumLevel: 4, hatEvery: 1, smooth: 0.65f);

        GD.Print("[GenBgm3] done: bgm_game_1~4.wav (battle style)");
        return true;
    }

    // ==================== Riff 数据（A 小调，每小节 8 个八分音符 × 8 小节）====================

    private static int[] Riff1()
    {
        int[][] bars =
        {
            new[]{45,45,57,45,48,45,57,45},
            new[]{45,45,57,45,50,45,57,45},
            new[]{41,41,52,41,48,48,52,48},
            new[]{43,43,55,43,47,43,55,47},
        };
        return Repeat(bars);
    }

    private static int[] Riff2()
    {
        // 安达卢西亚进行：Am → G → F → E（天生的对决/压迫感）
        int[][] bars =
        {
            new[]{45,57,48,57,45,57,48,57},
            new[]{43,55,47,55,43,55,47,55},
            new[]{41,53,45,53,41,53,45,53},
            new[]{40,52,44,52,40,52,44,52},
        };
        return Repeat(bars);
    }

    private static int[] Riff3()
    {
        // 和声小调：G#（44/56）导音制造尖锐紧张
        int[][] bars =
        {
            new[]{45,45,48,45,57,45,48,45},
            new[]{44,44,47,44,56,44,47,44},
            new[]{41,41,45,41,53,41,45,41},
            new[]{40,40,52,40,44,44,52,44},
        };
        return Repeat(bars);
    }

    private static int[] Riff4()
    {
        // 高音 A3（57）常驻 pedal + 低音行进（压迫感）
        int[][] bars =
        {
            new[]{57,45,57,45,57,45,48,45},
            new[]{57,43,57,43,57,43,47,43},
            new[]{57,41,57,41,57,41,45,41},
            new[]{57,40,57,40,57,44,57,40},
        };
        return Repeat(bars);
    }

    private static int[] Repeat(int[][] bars)
    {
        var all = new List<int>(64);
        for (int pass = 0; pass < 2; pass++)
            foreach (var bar in bars)
                all.AddRange(bar);
        return all.ToArray();
    }

    private static int[] Bass1() => Repeat4(new[] { 45, 45, 41, 43 });       // Am Am F G
    private static int[] Bass2() => Repeat4(new[] { 45, 43, 41, 40 });       // Am G F E（安达卢西亚）
    private static int[] Bass3() => Repeat4(new[] { 45, 44, 41, 40 });       // 和声小调版
    private static int[] Bass4() => Repeat4(new[] { 33, 33, 33, 33 });       // A1 pedal（持续低音）

    private static int[] Repeat4(int[] perBar)
    {
        var all = new List<int>(64);
        for (int pass = 0; pass < 2; pass++)
            for (int bar = 0; bar < 4; bar++)
                for (int s = 0; s < 4; s++)
                    all.Add(perBar[bar]);   // 每小节 4 个四分贝斯音
        return all.ToArray();
    }

    // ==================== 音色 ====================

    private static double Freq(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);
    private static float Saw(double p) => (float)(2.0 * (p % 1.0) - 1.0);
    private static float Square(double p) => p % 1.0 < 0.5 ? 1f : -1f;
    private static float Triangle(double p) => (float)(2.0 * Math.Abs(2.0 * (p % 1.0) - 1.0) - 1.0);
    private static float Sine(double p) => (float)Math.Sin(2 * Math.PI * (p % 1.0));

    // 主音色：方波 或 失谐双锯齿（失谐幅度收敛到 0.3%，保留"打拍"紧张感但不过分科幻）
    private static float LeadSquare(double t, double f) => Square(t * f);
    private static float LeadSawDetune(double t, double f) => (Saw(t * f) + Saw(t * f * 1.003)) * 0.5f;

    // ==================== 合成 ====================

    /// <summary>一阶低通滤波（IIR）：alpha 越小越柔（去电子毛刺），1 = 不过滤。</summary>
    private static void LowPass(float[] buf, float alpha)
    {
        float y = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            y += alpha * (buf[i] - y);
            buf[i] = y;
        }
    }

    private static void GenGame(
        string path, int bpm, int[] riff, int[] bassPerBar,
        bool detune, int pedalNote, int drumLevel, int hatEvery, float smooth)
    {
        double spb = 60.0 / bpm;
        int bars = 16;
        int total = (int)(bars * 4 * spb * SampleRate);
        var buf = new float[total];
        var rand = new Random(999);
        Func<double, double, float> lead = detune ? LeadSawDetune : LeadSquare;

        // ---- 1) Riff 主音（64 音 ×2 遍 = 16 小节，音色随 detune）----
        for (int i = 0; i < riff.Length; i++)
        {
            if (riff[i] <= 0) continue;
            double dur = spb;
            int start = (int)(i * spb * SampleRate);
            int n = (int)(dur * SampleRate);
            double f = Freq(riff[i]);
            for (int s = 0; s < n && start + s < total; s++)
            {
                double t = (double)s / SampleRate;
                double env = s < n * 0.05
                    ? (double)s / (n * 0.05)
                    : Math.Exp(-2.0 * (s - n * 0.05) / n);
                buf[start + s] += 0.26f * lead(t, f) * (float)env;
            }
        }

        // ---- 2) 贝斯：每小节 4 个四分音符；高 drumLevel 时后 2 音升高八度（行进感）----
        for (int bar = 0; bar < bars; bar++)
        {
            int root = bassPerBar[bar % bassPerBar.Length];
            for (int s = 0; s < 4; s++)
            {
                int m = (drumLevel >= 3 && s >= 2) ? root + 12 : root;
                double dur = spb * 0.95;
                int start = (int)((bar * 4 + s) * spb * SampleRate);
                int n = (int)(dur * SampleRate);
                double f = Freq(m);
                for (int i = 0; i < n && start + i < total; i++)
                {
                    double t = (double)i / SampleRate;
                    double env = Math.Exp(-2.5 * i / n);
                    buf[start + i] += 0.26f * Triangle(t * f) * (float)env;
                }
            }
        }

        // ---- 3) Pedal 低音（仅曲 4）：A1 全音符持续，压迫感铺底 ----
        if (pedalNote >= 0)
        {
            double f = Freq(pedalNote);
            for (int i = 0; i < total; i++)
            {
                double t = (double)i / SampleRate;
                double pulse = 0.75 + 0.25 * Math.Sin(2 * Math.PI * (t / spb));   // 每拍脉动
                buf[i] += 0.14f * Sine(t * f) * (float)pulse;
            }
        }

        // ---- 4) 鼓组（密度随 drumLevel：1 行进 → 4 全密度）----
        int beats = bars * 4;
        for (int k = 0; k < beats; k++)
        {
            bool snare = drumLevel >= 2 && k % 2 == 1;
            int start = (int)(k * spb * SampleRate);
            int n = (int)(0.09 * SampleRate);
            for (int i = 0; i < n && start + i < total; i++)
            {
                double t = (double)i / SampleRate;
                if (snare)
                    buf[start + i] += (0.08f + 0.06f * drumLevel) * (float)(rand.NextDouble() * 2 - 1) * (float)Math.Exp(-26 * t);
                else
                    buf[start + i] += 0.42f * (float)Math.Sin(2 * Math.PI * (110 - 700 * t) * t) * (float)Math.Exp(-22 * t);
            }

            // drumLevel >= 3：每拍之间追加 kick（双踩感）
            if (drumLevel >= 3)
            {
                int s2 = (int)((k + 0.5) * spb * SampleRate);
                int n2 = (int)(0.05 * SampleRate);
                for (int i = 0; i < n2 && s2 + i < total; i++)
                {
                    double t = (double)i / SampleRate;
                    buf[s2 + i] += (0.14f + 0.08f * drumLevel) * (float)Math.Sin(2 * Math.PI * (100 - 500 * t) * t) * (float)Math.Exp(-30 * t);
                }
            }
        }

        int hatCount = bars * 4 * 2 / hatEvery;
        for (int h = 0; h < hatCount; h++)
        {
            int start = (int)(h * hatEvery * (spb / 2) * SampleRate);
            int n = (int)(0.03 * SampleRate);
            for (int i = 0; i < n && start + i < total; i++)
            {
                double t = (double)i / SampleRate;
                buf[start + i] += (0.04f + 0.015f * drumLevel) * (float)(rand.NextDouble() * 2 - 1) * (float)Math.Exp(-70 * t);
            }
        }

        // ---- 低通滤波（去电子毛刺：alpha 越小越柔；打击乐瞬态仍保留节奏感）----
        LowPass(buf, smooth);

        // ---- 归一化 ----
        float peak = 0f;
        foreach (var s in buf) peak = Math.Max(peak, Math.Abs(s));
        if (peak > 0.001f)
        {
            float g = 0.85f / peak;
            for (int i = 0; i < buf.Length; i++) buf[i] *= g;
        }

        WriteWav(path, buf);
        GD.Print($"[GenBgm3] wrote {path} ({buf.Length / SampleRate}s, {bpm} BPM, drum lv{drumLevel}, {(detune ? "detuned saw" : "square")})");
    }

    private static void WriteWav(string path, float[] samples)
    {
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        int dataLen = samples.Length * 2;
        bw.Write("RIFF"u8);
        bw.Write(36 + dataLen);
        bw.Write("WAVE"u8);
        bw.Write("fmt "u8);
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)1);
        bw.Write(SampleRate);
        bw.Write(SampleRate * 2);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write("data"u8);
        bw.Write(dataLen);
        foreach (var s in samples)
            bw.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f));
    }
}
