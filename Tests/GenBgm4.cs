using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace GGJ;

/// <summary>
/// ⚠️ 临时工具 v3：合成 4 首"森林自然风"游戏 BGM（大调五声音阶 / 卡林巴拨弦 / 沙锤 / 鸟鸣水滴）。
///   bgm_game_1.wav  96 BPM C 大调 悠然漫步（轮 1~2）
///   bgm_game_2.wav 104 BPM F 大调 溪流轻快（轮 3~4）
///   bgm_game_3.wav 112 BPM G 大调 林间跳跃（轮 5~6）
///   bgm_game_4.wav 120 BPM D 大调 生机奔跑（轮 7~8）
/// 输出：audio/bgm/bgm_game_1~4.wav（各 16 小节循环）。
/// 运行：godot --headless --script res://Tests/GenBgm4.cs
/// </summary>
public partial class GenBgm4 : SceneTree
{
    private const int SampleRate = 44100;
    private bool _ran;

    public override bool _Process(double delta)
    {
        if (_ran) return true;
        _ran = true;

        Directory.CreateDirectory("audio/bgm");

        GenForest("audio/bgm/bgm_game_1.wav", 96,
            bass: new[] { 48, 45, 41, 43 }, pad: new[] { 60, 57, 53, 55 },
            mel: Mel1(), kick: false, birdChance: 0.50, smooth: 0.50f);
        GenForest("audio/bgm/bgm_game_2.wav", 104,
            bass: new[] { 41, 38, 34, 36 }, pad: new[] { 53, 50, 46, 48 },
            mel: Mel2(), kick: true, birdChance: 0.60, smooth: 0.50f);
        GenForest("audio/bgm/bgm_game_3.wav", 112,
            bass: new[] { 43, 40, 36, 38 }, pad: new[] { 55, 52, 48, 50 },
            mel: Mel3(), kick: true, birdChance: 0.70, smooth: 0.55f);
        GenForest("audio/bgm/bgm_game_4.wav", 120,
            bass: new[] { 38, 35, 43, 45 }, pad: new[] { 50, 47, 43, 45 },
            mel: Mel4(), kick: true, birdChance: 0.50, smooth: 0.60f);

        GD.Print("[GenBgm4] done: bgm_game_1~4.wav (forest style)");
        return true;
    }

    // ==================== 旋律数据（大调五声，每小节 8 个八分音符 × 4 小节，循环 4 遍 = 16 小节）====================

    private static int[] Mel1()
    {
        // C — Am — F — G，五声音阶环绕
        int[][] bars =
        {
            new[]{60,64,67,64,60,64,67,72},
            new[]{57,60,64,60,57,60,64,69},
            new[]{53,57,60,57,53,57,60,65},
            new[]{55,59,62,59,55,59,62,67},
        };
        return Repeat(bars);
    }

    private static int[] Mel2()
    {
        // F — Dm — Bb — C，上行 sparkling
        int[][] bars =
        {
            new[]{65,69,72,69,65,69,72,77},
            new[]{62,65,69,65,62,65,69,74},
            new[]{58,62,65,62,58,62,65,70},
            new[]{60,64,67,64,60,64,67,72},
        };
        return Repeat(bars);
    }

    private static int[] Mel3()
    {
        // G — Em — C — D，跳跃感
        int[][] bars =
        {
            new[]{67,71,74,71,67,71,74,79},
            new[]{64,67,71,67,64,67,71,76},
            new[]{60,64,67,64,60,64,67,72},
            new[]{62,66,69,66,62,66,69,74},
        };
        return Repeat(bars);
    }

    private static int[] Mel4()
    {
        // D — Bm — G — A，奔跑感（结尾推高）
        int[][] bars =
        {
            new[]{62,66,69,66,62,66,69,74},
            new[]{59,62,66,62,59,62,66,71},
            new[]{55,59,62,59,55,59,62,67},
            new[]{57,61,64,61,57,61,64,69},
        };
        return Repeat(bars);
    }

    private static int[] Repeat(int[][] bars)
    {
        var all = new List<int>(128);
        for (int pass = 0; pass < 4; pass++)
            foreach (var bar in bars)
                all.AddRange(bar);
        return all.ToArray();
    }

    // ==================== 合成 ====================

    private static double Freq(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

    /// <summary>一阶低通滤波：alpha 越小越柔。</summary>
    private static void LowPass(float[] buf, float alpha)
    {
        float y = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            y += alpha * (buf[i] - y);
            buf[i] = y;
        }
    }

    private static void GenForest(
        string path, int bpm, int[] bass, int[] pad, int[] mel,
        bool kick, double birdChance, float smooth)
    {
        double spb = 60.0 / bpm;
        int bars = 16;
        int total = (int)(bars * 4 * spb * SampleRate);
        var buf = new float[total];
        var rand = new Random(2024);

        // ---- 1) 卡林巴拨弦主音（正弦 + 二次谐波，快速衰减，木质清脆感）----
        for (int i = 0; i < mel.Length; i++)
        {
            double f = Freq(mel[i]);
            int start = (int)(i * spb * SampleRate);
            int n = (int)(spb * SampleRate);
            for (int s = 0; s < n && start + s < total; s++)
            {
                double t = (double)s / SampleRate;
                double env = Math.Exp(-4.0 * t / spb);                       // 拨弦衰减
                double tone = Math.Sin(2 * Math.PI * f * t)
                            + 0.30 * Math.Sin(2 * Math.PI * 2 * f * t) * Math.Exp(-8 * t);
                buf[start + s] += 0.20f * (float)tone * (float)env;
            }
        }

        // ---- 2) 柔和弦垫（每小节 1 个三和弦，正弦极轻铺底）----
        for (int bar = 0; bar < bars; bar++)
        {
            int root = pad[bar % pad.Length];
            bool minor = bar % 4 == 1;                                       // 每进行第 2 小节为小三和弦（vi）
            int[] chord = minor
                ? new[] { root, root + 3, root + 7 }
                : new[] { root, root + 4, root + 7 };
            int start = (int)(bar * 4 * spb * SampleRate);
            int n = (int)(4 * spb * SampleRate);
            foreach (int m in chord)
            {
                double f = Freq(m);
                for (int s = 0; s < n && start + s < total; s++)
                {
                    double t = (double)s / SampleRate;
                    double env = Math.Min(1.0, t / (spb * 0.5))              // 缓起
                               * Math.Min(1.0, (n / (double)SampleRate - t) / (spb * 0.5)); // 缓收
                    buf[start + s] += 0.045f * (float)Math.Sin(2 * Math.PI * f * t) * (float)env;
                }
            }
        }

        // ---- 3) 圆润贝斯（正弦，每小节 2 个二分音符）----
        for (int bar = 0; bar < bars; bar++)
        {
            int root = bass[bar % bass.Length];
            for (int half = 0; half < 2; half++)
            {
                int m = half == 1 ? root + 7 : root;                         // 根音 → 五音，流动感
                double f = Freq(m);
                int start = (int)((bar * 4 + half * 2) * spb * SampleRate);
                int n = (int)(2 * spb * 0.95 * SampleRate);
                for (int s = 0; s < n && start + s < total; s++)
                {
                    double t = (double)s / SampleRate;
                    double env = Math.Min(1.0, t / 0.02) * Math.Exp(-1.2 * t / (2 * spb));
                    buf[start + s] += 0.18f * (float)Math.Sin(2 * Math.PI * f * t) * (float)env;
                }
            }
        }

        // ---- 4) 轻打击：沙锤（每八分音符，反拍重音）+ 柔 kick ----
        int steps = bars * 8;
        for (int k = 0; k < steps; k++)
        {
            int start = (int)(k * (spb / 2) * SampleRate);
            int n = (int)(0.04 * SampleRate);
            bool accent = k % 2 == 1;                                        // 反拍沙锤重音
            float vol = accent ? 0.055f : 0.030f;
            for (int s = 0; s < n && start + s < total; s++)
            {
                double t = (double)s / SampleRate;
                buf[start + s] += vol * (float)(rand.NextDouble() * 2 - 1) * (float)Math.Exp(-80 * t);
            }
        }

        if (kick)
        {
            for (int k = 0; k < bars * 4; k += 2)                            // 每小节 1、3 拍
            {
                int start = (int)(k * spb * SampleRate);
                int n = (int)(0.10 * SampleRate);
                for (int s = 0; s < n && start + s < total; s++)
                {
                    double t = (double)s / SampleRate;
                    buf[start + s] += 0.22f * (float)Math.Sin(2 * Math.PI * (95 - 45 * t) * t) * (float)Math.Exp(-24 * t);
                }
            }
        }

        // ---- 5) 鸟鸣（正弦滑音短哨，随机位置——点题"雨林"）----
        for (int bar = 0; bar < bars; bar++)
        {
            if (rand.NextDouble() > birdChance) continue;
            int chirps = 1 + (rand.Next(2));                                 // 1~2 声
            for (int c = 0; c < chirps; c++)
            {
                double f0 = 1800 + rand.NextDouble() * 1400;                 // 1.8k~3.2kHz
                double df = (rand.NextDouble() * 2 - 1) * 900;               // 上滑或下滑
                double at = (bar * 4 + rand.NextDouble() * 4) * spb;
                int start = (int)(at * SampleRate);
                int n = (int)(0.12 * SampleRate);
                for (int s = 0; s < n && start + s < total; s++)
                {
                    double t = (double)s / SampleRate;
                    double phase = 2 * Math.PI * (f0 * t + 0.5 * df * t * t);
                    buf[start + s] += 0.040f * (float)Math.Sin(phase) * (float)Math.Exp(-22 * t);
                }
            }
        }

        // ---- 6) 水滴（下滑正弦短音，稀疏点缀）----
        for (int bar = 0; bar < bars; bar += 2)
        {
            if (rand.NextDouble() > 0.30) continue;
            double at = (bar * 4 + rand.NextDouble() * 8) * spb;
            int start = (int)(at * SampleRate);
            int n = (int)(0.09 * SampleRate);
            for (int s = 0; s < n && start + s < total; s++)
            {
                double t = (double)s / SampleRate;
                double f = 1400 - 9000 * t;                                  // 快速下滑
                if (f < 200) f = 200;
                buf[start + s] += 0.055f * (float)Math.Sin(2 * Math.PI * f * t) * (float)Math.Exp(-30 * t);
            }
        }

        // ---- 低通柔化 + 归一化 ----
        LowPass(buf, smooth);
        float peak = 0f;
        foreach (var s in buf) peak = Math.Max(peak, Math.Abs(s));
        if (peak > 0.001f)
        {
            float g = 0.85f / peak;
            for (int i = 0; i < buf.Length; i++) buf[i] *= g;
        }

        WriteWav(path, buf);
        GD.Print($"[GenBgm4] wrote {path} ({buf.Length / SampleRate}s, {bpm} BPM, forest)");
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
