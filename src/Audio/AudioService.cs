using System.Collections.Generic;
using Godot;

namespace GGJ;

/// <summary>
/// 音效服务。**文件名固定，美术/音频同学直接替换文件即可换皮，代码不用动。**
///
/// 文件约定：把音频放进 res://audio/，文件名保持下列 key 不变
///   shoot / hit / enemy_die / player_hurt / upgrade / win / lose（.wav 或 .ogg）
///
/// 注：初始界面 BGM（audio/bgm/UIbgm.wav）由主菜单场景（MainMenu）自行播放——
/// 本服务挂在游戏场景（Main.tscn）下，主菜单阶段不存在，BGM 不放这里。
/// </summary>
public partial class AudioService : Node
{
    private static readonly string[] Keys =
        { "shoot", "hit", "enemy_die", "player_hurt", "upgrade", "win", "lose" };

    private readonly Dictionary<string, AudioStream> _clips = new();

    public override void _Ready()
    {
        Bus.Sub<SfxRequest>(this, OnSfx);
        LoadAll();
    }

    private void LoadAll()
    {
        foreach (var key in Keys)
        {
            foreach (var ext in new[] { ".wav", ".ogg" })
            {
                string path = $"res://audio/{key}{ext}";
                if (!ResourceLoader.Exists(path)) continue;
                _clips[key] = GD.Load<AudioStream>(path);
                break;
            }
        }
    }

    private void OnSfx(SfxRequest r)
    {
        // TODO(程序C)：用 AudioStreamPlayer 播放 _clips[r.Key]（当前 SFX 文件未配置，静默跳过）
        if (!_clips.ContainsKey(r.Key)) return;
    }
}
