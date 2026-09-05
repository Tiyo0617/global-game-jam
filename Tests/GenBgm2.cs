using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace GGJ;

/// <summary>
/// ⚠️ 临时工具（P2-18 附加）：在 bgm_menu_a 风格基础上的两个升级变体。
///   d = 丰满版：25% duty 主奏+颤音 / 和声卡农 / 琶音伴奏 / 跳贝斯 / 三件鼓 / 回声
///   e = 梦幻版：三角波主奏 + 强回声 + 慢琶音，休闲解谜感
/// 运行：godot --headless --script res://Tests/GenBgm2.cs
/// </summary>
public partial class GenBgm2 : SceneTree
{
    private const int SampleRate = 44100;
    private bool _ran;

    public override bool _Process(double delta)
    {
        if (_ran) return true;
        _ran = true;

        Directory.CreateDirectory("audio/bgm");

        GenD("audio/bgm/bgm_menu_d.wav");
        GenE("audio/bgm/bgm_menu_e.wav");

        GD.Print("[GenBgm2] done: bgm_menu_d.wav / bgm_menu_e.wav");
        return true;
    }

    // ==================== 公共 ====================

    private static double Freq(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);
    private static float Square25(double phase) => phase % 1.0 < 0.25 ? 1f : -1f;
    private static float Square(double phase) => phase % 1.0 < 0.5 ? 1f : -1f;
    private static float Triangle(double phase) => (float)(2.0 * Math.Abs(2.0 * (phase % 1.0) - 1.0) - 1.0);
    private static float Sine(double phase) => (float)Math.Sin(2 * Math.PI * (phase % 1.0));

    // MelodyA 的 16 小节（用户已认可的主题），(MIDI, 八分音符时值)，0 = 休止
    private static (int, int)[] ThemeA()
    {
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
        all.AddRange(a);
        return all.ToArray();
    }

    // 16 小节的和弦根音（用于琶音/贝斯）：C G Am F ×2
    private static readonly int[] ChordRoots = { 48, 43, 45, 41, 48, 43, 45, 41, 48, 43, 45, 41, 48, 43, 45, 41 };

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

    /// <summary>后处理：1/8 拍延迟回声（feedback 0.3，两次）。</summary>
    private static void AddEcho(float[] buf, int delaySamples)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            float g = 0.32f / (pass + 1);
            for (int i = 0; i + delaySamples < buf.Length; i++)
                buf[i + delaySamples] += buf[i] * g;
            delaySamples *= 2;
        }
    }

    private static void Normalize(float[] buf)
    {
        float peak = 0f;
        foreach (var s in buf) peak = Math.Max(peak, Math.Abs(s));
        if (peak > 0.001f)
        {
            float g = 0.85f / peak;
            for (int i = 0; i < buf.Length; i++) buf[i] *= g;
        }
    }

    // ==================== 曲 D：丰满版 ====================

    private static void GenD(string path)
    {
        int bpm = 120;
        double spb = 60.0 / bpm;
        double spe = spb / 2;
        int bars = 16;
        int total = (int)(bars * 8 * spe * SampleRate);
        var buf = new float[total];
        var melody = ThemeA();
        var rand = new Random(777);

        // ---- 1) 主旋律：25% duty 方波 + 轻微颤音（5Hz ±6 音分）----
        int cursor = 0;
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
                    double env = i < n * 0.05
                        ? (double)i / (n * 0.05)
                        : Math.Exp(-2.0 * (i - n * 0.05) / n);
                    double vib = 1.0 + 0.0035 * Math.Sin(2 * Math.PI * 5.0 * t);   // 颤音
                    buf[start + i] += 0.30f * Square25(t * f * vib) * (float)env;
                }
            }
            cursor += len;
        }

        // ---- 2) 和声卡农：主旋律延迟 2 个八分、低两个八度重奏（音量小）----
        cursor = 0;
        foreach (var (midi, len) in melody)
        {
            if (midi > 0)
            {
                double dur = len * spe;
                int start = (int)((cursor + 2) * spe * SampleRate);
                int n = (int)(dur * SampleRate);
                double f = Freq(midi - 12);
                for (int i = 0; i < n && start + i < total; i++)
                {
                    double t = (double)i / SampleRate;
                    double env = Math.Exp(-1.8 * i / n);
                    buf[start + i] += 0.10f * Square(t * f) * (float)env;
                }
            }
            cursor += len;
        }

        // ---- 3) 琶音伴奏：每小节和弦音 8 分音符循环（root-3rd-5th-oct）----
        for (int bar = 0; bar < bars; bar++)
        {
            int root = ChordRoots[bar];
            int[] arp = { root, root + 4, root + 7, root + 12, root + 7, root + 4, root, root + 4 };
            for (int s = 0; s < 8; s++)
            {
                double dur = spe;
                int start = (int)((bar * 8 + s) * spe * SampleRate);
                int n = (int)(dur * SampleRate);
                double f = Freq(arp[s]);
                for (int i = 0; i < n && start + i < total; i++)
                {
                    double t = (double)i / SampleRate;
                    double env = Math.Exp(-6.0 * i / n);
                    buf[start + i] += 0.07f * Triangle(t * f) * (float)env;
                }
            }
        }

        // ---- 4) 跳贝斯：根音-高八度交替（8 分音符）----
        for (int bar = 0; bar < bars; bar++)
        {
            int root = ChordRoots[bar];
            for (int s = 0; s < 8; s++)
            {
                int m = (s % 2 == 0) ? root : root + 12;
                double dur = spe * 0.9;
                int start = (int)((bar * 8 + s) * spe * SampleRate);
                int n = (int)(dur * SampleRate);
                double f = Freq(m);
                for (int i = 0; i < n && start + i < total; i++)
                {
                    double t = (double)i / SampleRate;
                    double env = Math.Exp(-4.0 * i / n);
                    buf[start + i] += 0.22f * Triangle(t * f) * (float)env;
                }
            }
        }

        // ---- 5) 鼓组：kick 拍 1/3，snare 拍 2/4，hat 每八分 ----
        for (int k = 0; k < bars * 4; k++)
        {
            bool snare = k % 2 == 1;
            int start = (int)(k * spb * SampleRate);
            int n = (int)(0.09 * SampleRate);
            for (int i = 0; i < n && start + i < total; i++)
            {
                double t = (double)i / SampleRate;
                if (snare)
                {
                    buf[start + i] += 0.22f * (float)(rand.NextDouble() * 2 - 1) * (float)Math.Exp(-26 * t);
                }
                else
                {
                    buf[start + i] += 0.45f * (float)Math.Sin(2 * Math.PI * (110 - 700 * t) * t) * (float)Math.Exp(-22 * t);
                }
            }
        }
        for (int h = 0; h < bars * 8; h++)
        {
            int start = (int)(h * spe * SampleRate);
            int n = (int)(0.03 * SampleRate);
            for (int i = 0; i < n && start + i < total; i++)
            {
                double t = (double)i / SampleRate;
                buf[start + i] += 0.09f * (float)(rand.NextDouble() * 2 - 1) * (float)Math.Exp(-70 * t);
            }
        }

        Normalize(buf);
        AddEcho(buf, (int)(spe * SampleRate));   // 1/8 拍回声
        Normalize(buf);
        WriteWav(path, buf);
        GD.Print($"[GenBgm2] wrote {path}");
    }

    // ==================== 曲 E：梦幻版 ====================

    private static void GenE(string path)
    {
        int bpm = 100;
        double spb = 60.0 / bpm;
        double spe = spb / 2;
        int bars = 16;
        int total = (int)(bars * 8 * spe * SampleRate);
        var buf = new float[total];
        var melody = ThemeA();
        var rand = new Random(888);

        // ---- 主旋律：三角波（柔）----
        int cursor = 0;
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
                    double env = i < n * 0.1
                        ? (double)i / (n * 0.1)
                        : Math.Exp(-1.5 * (i - n * 0.1) / n);
                    buf[start + i] += 0.30f * Triangle(t * f) * (float)env;
                }
            }
            cursor += len;
        }

        // ---- 慢琶音 pad：每小节 4 个长音（root-5th-oct-5th），三角波，音量小 ----
        for (int bar = 0; bar < bars; bar++)
        {
            int root = ChordRoots[bar];
            int[] arp = { root, root + 7, root + 12, root + 7 };
            for (int s = 0; s < 4; s++)
            {
                double dur = spb;   // 每音一拍
                int start = (int)((bar * 4 + s) * spb * SampleRate);
                int n = (int)(dur * SampleRate);
                double f = Freq(arp[s] + 12);
                for (int i = 0; i < n && start + i < total; i++)
                {
                    double t = (double)i / SampleRate;
                    double env = Math.Sin(Math.PI * i / n);   // 正弦包络（淡入淡出）
                    buf[start + i] += 0.12f * Triangle(t * f) * (float)env;
                }
            }
        }

        // ---- 长音贝斯：每小节根音全音符，正弦 ----
        for (int bar = 0; bar < bars; bar++)
        {
            double dur = 4 * spb;
            int start = (int)(bar * 4 * spb * SampleRate);
            int n = (int)(dur * SampleRate);
            double f = Freq(ChordRoots[bar]);
            for (int i = 0; i < n && start + i < total; i++)
            {
                double t = (double)i / SampleRate;
                double env = Math.Sin(Math.PI * i / n);
                buf[start + i] += 0.18f * Sine(t * f) * (float)env;
            }
        }

        // ---- 轻柔 hat：每 2 个八分，音量很低 ----
        for (int h = 0; h < bars * 4; h++)
        {
            int start = (int)(h * 2 * spe * SampleRate);
            int n = (int)(0.025 * SampleRate);
            for (int i = 0; i < n && start + i < total; i++)
            {
                double t = (double)i / SampleRate;
                buf[start + i] += 0.05f * (float)(rand.NextDouble() * 2 - 1) * (float)Math.Exp(-60 * t);
            }
        }

        Normalize(buf);
        AddEcho(buf, (int)(spe * 2 * SampleRate));   // 1/4 拍长回声（梦幻空间感）
        Normalize(buf);
        WriteWav(path, buf);
        GD.Print($"[GenBgm2] wrote {path}");
    }
}
