#nullable enable
using Godot;
using KakarotMod.KakarotCode.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace KakarotMod.KakarotCode.DragonBalls;

// 召唤神龙的全屏演出：一张盖住战斗房间的全屏矩形，画面全在 kakarot_shenron_summon.gdshader 里。
// 纯表现，不碰玩法状态；挂在战斗房间下面，所以愿望菜单（在全局界面层）会盖在它上面。
internal sealed class KakarotShenronSummonFx
{
    private const float IntroSeconds = 2.9f;
    private const float OutroSeconds = 0.9f;
    private const string AudioDir = "sfx/shenron/";

    private readonly ColorRect _rect;
    private readonly ShaderMaterial _material;
    private readonly TaskCompletionSource _introDone = new();
    private Tween _clock;
    private float _time;

    public Task IntroDone => _introDone.Task;

    private KakarotShenronSummonFx(NCombatRoom room, KakarotDragonBallSet set)
    {
        string name = set switch
        {
            KakarotDragonBallSet.Namek => "namek",
            KakarotDragonBallSet.Super => "super",
            KakarotDragonBallSet.BlackStar => "blackstar",
            _ => "earth",
        };
        var dragon = GD.Load<Texture2D>($"res://Kakarot/Images/Cards/Big/kakarot_dragon_set_{name}.png");
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Kakarot/Shaders/kakarot_shenron_summon.gdshader") };
        _material.SetShaderParameter("dragon_tex", dragon);
        _material.SetShaderParameter("ball_tex", GD.Load<Texture2D>($"res://Kakarot/Images/UI/kakarot_dragon_ball_{name}.png"));
        _material.SetShaderParameter("dragon_aspect", (float)dragon.GetWidth() / dragon.GetHeight());
        _material.SetShaderParameter("aspect", room.Size.X / room.Size.Y);
        _material.SetShaderParameter("glow_color", set switch
        {
            KakarotDragonBallSet.Namek => new Color(0.3f, 1f, 0.8f),
            KakarotDragonBallSet.Super => new Color(1f, 0.85f, 0.3f),
            KakarotDragonBallSet.BlackStar => new Color(0.95f, 0.2f, 0.25f),
            _ => new Color(0.45f, 1f, 0.5f),
        });

        _rect = new ColorRect { Name = "KakarotShenronSummonFx", Material = _material, MouseFilter = Control.MouseFilterEnum.Stop };
        _rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _rect.GuiInput += OnInput;
        room.AddChild(_rect);

        _clock = StartClock(0f);
        After(IntroSeconds, () => _introDone.TrySetResult());

        KakarotAudio.PlaySfx(AudioDir + "sfx_rain.ogg", -6f);
        After(0.55f, () => KakarotAudio.PlaySfx(AudioDir + "sfx_thunder.ogg"));
        After(1.3f, () => KakarotAudio.PlaySfx(AudioDir + "sfx_shenron_appear.ogg"));
        After(2.3f, () => KakarotAudio.PlayVoice(AudioDir + $"voice_{name}.ogg"));
    }

    public static KakarotShenronSummonFx? Start(KakarotDragonBallSet set)
    {
        var room = NCombatRoom.Instance;
        return room == null ? null : new KakarotShenronSummonFx(room, set);
    }

    private Tween StartClock(float from)
    {
        var tween = _rect.CreateTween();
        tween.TweenMethod(Callable.From<float>(value =>
        {
            _time = value;
            _material.SetShaderParameter("time_s", value);
        }), from, from + 600f, 600.0);
        return tween;
    }

    private void After(float seconds, Action action)
    {
        var tween = _rect.CreateTween();
        tween.TweenInterval(seconds);
        tween.TweenCallback(Callable.From(action));
    }

    // 点一下直接跳到神龙已经现身的画面。
    private void OnInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { Pressed: true } || _introDone.Task.IsCompleted)
        {
            return;
        }

        _clock.Kill();
        _clock = StartClock(Math.Max(_time, 3.2f));
        _introDone.TrySetResult();
    }

    public void Finish()
    {
        if (!GodotObject.IsInstanceValid(_rect))
        {
            return;
        }

        _introDone.TrySetResult();
        _rect.MouseFilter = Control.MouseFilterEnum.Ignore;
        KakarotAudio.PlaySfx(AudioDir + "sfx_shenron_fade.ogg");
        var tween = _rect.CreateTween();
        tween.TweenMethod(Callable.From<float>(value => _material.SetShaderParameter("out_s", value)), 0f, OutroSeconds, OutroSeconds);
        tween.TweenCallback(Callable.From(_rect.QueueFree));
    }
}
