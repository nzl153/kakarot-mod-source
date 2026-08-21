using System.Collections.Generic;
using Godot;

namespace KakarotMod.KakarotCode.Helpers;

/// <summary>
/// 卡卡罗特界面层自定义音频播放（纯表现）。用 Godot 原生 AudioStreamPlayer 播打包进 pck 的普通音频，
/// 绕开 FMOD 事件那套——mod 加不了 FMOD event，写了也只会静默。
///
/// 战斗内音效走 KakarotCombatPresentation.PlaySfx（挂在角色 visuals 上、带随机音高）；
/// 这里只服务于没有 visuals 可挂的界面场景：过场擦除音、选人语音。
///
/// 每次一次性播放：新建 AudioStreamPlayer 挂场景树根，Finished 后自 QueueFree；流按 path 静态缓存。
/// 绝不影响玩法/RNG/联机。异常一律吞掉，不中断游戏。
/// </summary>
public static class KakarotAudio
{
    private const string Dir = "res://Kakarot/Audio/";

    private static readonly Dictionary<string, AudioStream> Cache = new();

    // 语音独占：正在播的语音实例，避免来回选人叠音。
    private static AudioStreamPlayer _voicePlayer;

    private static SceneTree Tree => Engine.GetMainLoop() as SceneTree;

    private static AudioStream Load(string name)
    {
        try
        {
            var path = Dir + name;
            if (Cache.TryGetValue(path, out var cached))
            {
                return cached;
            }

            var stream = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
            Cache[path] = stream;   // null 也缓存，避免反复 Exists。
            return stream;
        }
        catch
        {
            return null;
        }
    }

    private static AudioStreamPlayer Spawn(AudioStream stream, float volumeDb)
    {
        var root = Tree?.Root;
        if (root == null)
        {
            return null;
        }

        var player = new AudioStreamPlayer
        {
            Stream = stream,
            VolumeDb = volumeDb,
        };
        root.AddChild(player);
        player.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(player))
            {
                player.QueueFree();
            }
        };
        player.Play();
        return player;
    }

    /// <summary>一次性播放一个音效文件（相对 Kakarot/Audio/，如 "sfx/ui/transition.wav"）；缺失/失败静默跳过。</summary>
    public static void PlaySfx(string name, float volumeDb = 0f)
    {
        try
        {
            var stream = Load(name);
            if (stream != null)
            {
                Spawn(stream, volumeDb);
            }
        }
        catch
        {
            // 纯表现：绝不因它中断游戏。
        }
    }

    /// <summary>播放语音，若上一句仍在播则跳过（避免来回选人叠音）。</summary>
    public static void PlayVoice(string name, float volumeDb = 0f)
    {
        try
        {
            if (_voicePlayer != null && GodotObject.IsInstanceValid(_voicePlayer) && _voicePlayer.Playing)
            {
                return;
            }

            var stream = Load(name);
            if (stream != null)
            {
                _voicePlayer = Spawn(stream, volumeDb);
            }
        }
        catch
        {
            // 纯表现。
        }
    }
}
