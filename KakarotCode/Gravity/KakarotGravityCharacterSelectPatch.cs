using System.Reflection;
using BaseLib.Config;
using Godot;
using HarmonyLib;
using KakarotMod.KakarotCode.Config;
using KakarotMod.KakarotCode.Extensions;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using KakarotCharacter = KakarotMod.KakarotCode.Characters.Kakarot;
using MainFile = KakarotMod.Kakarot.MainFile;

namespace KakarotMod.KakarotCode.Gravity;

// 选人界面右上角的重力修行面板。底板、文字和箭头复制自官方进阶条，字体和样式跟官方一致；
// 复制出来的节点一律先改成左上角锚点再用固定坐标摆放。
[HarmonyPatch]
public static class KakarotGravityCharacterSelectPatch
{
    private const string PanelName = "KakarotGravityPanel";
    private const string UnavailableKey = "kakarot_gravity_panel_unavailable";
    private const float Width = 540f;
    private const float HeaderHeight = 92f;
    private const string LockedColor = "#948c78";

    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen._Ready));
        yield return AccessTools.Method(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter));
        yield return AccessTools.Method(typeof(NCharacterSelectScreen), "AfterInitialized");
    }

    public static void Postfix(NCharacterSelectScreen __instance)
    {
        if (__instance.HasMeta(UnavailableKey))
        {
            return;
        }

        var panel = __instance.GetNodeOrNull<Control>(PanelName) ?? CreatePanel(__instance);
        if (panel == null)
        {
            MainFile.Logger.Warn("Gravity panel: ascension panel nodes or mod config unavailable; panel disabled.");
            __instance.SetMeta(UnavailableKey, true);
            return;
        }

        panel.Visible = __instance.Lobby?.NetService.Type == NetGameType.Singleplayer
            && __instance.Lobby.LocalPlayer.character is KakarotCharacter;
        if (panel.Visible)
        {
            Refresh(panel);
        }
    }

    private static Control CreatePanel(NCharacterSelectScreen screen)
    {
        var ascension = screen.GetNodeOrNull<Control>("%AscensionPanel");
        var background = ascension?.GetNodeOrNull<NinePatchRect>("Background");
        var text = ascension?.GetNodeOrNull<RichTextLabel>("HBoxContainer/AscensionDescription/Description");
        var leftArrow = ascension?.GetNodeOrNull<Control>("HBoxContainer/LeftArrowContainer");
        var rightArrow = ascension?.GetNodeOrNull<Control>("HBoxContainer/RightArrowContainer");
        var config = ModConfigRegistry.Get<KakarotModConfig>();
        if (background == null || text == null || leftArrow == null || rightArrow == null || config == null)
        {
            return null;
        }

        var panel = new Control
        {
            Name = PanelName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 1f,
            AnchorRight = 1f,
            OffsetLeft = -Width - 36f,
            OffsetRight = -36f,
            OffsetTop = 150f,
            OffsetBottom = 150f + HeaderHeight,
        };

        var panelBackground = (NinePatchRect)background.Duplicate();
        panelBackground.Name = "Background";
        panelBackground.Modulate = new Color(0f, 0f, 0f, 0.74f);
        Place(panelBackground, 0, 0, Width, HeaderHeight);
        panel.AddChild(panelBackground);

        var icon = new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>("kakarot_gravity_weights.png".BigRelicImagePath()),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        Place(icon, 76, 12, 68, 68);
        panel.AddChild(icon);

        var header = (RichTextLabel)text.Duplicate();
        header.Name = "Header";
        header.AddThemeFontSizeOverride("normal_font_size", 22);
        header.AddThemeFontSizeOverride("bold_font_size", 32);
        Place(header, 156, 0, Width - 156 - 76, HeaderHeight);
        panel.AddChild(header);

        panel.AddChild(CreateArrow(leftArrow, "LeftArrow", 6, -1, panel, config));
        panel.AddChild(CreateArrow(rightArrow, "RightArrow", Width - 70, 1, panel, config));

        var divider = new ColorRect
        {
            Color = new Color(0.94f, 0.78f, 0.32f, 0.45f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        Place(divider, 22, HeaderHeight, Width - 44, 2);
        panel.AddChild(divider);

        var body = (RichTextLabel)text.Duplicate();
        body.Name = "Body";
        body.VerticalAlignment = VerticalAlignment.Top;
        body.AddThemeFontSizeOverride("normal_font_size", 22);
        body.AddThemeFontSizeOverride("bold_font_size", 22);
        body.AddThemeConstantOverride("line_separation", 4);
        Place(body, 26, HeaderHeight + 14, Width - 52, 200);
        panel.AddChild(body);

        screen.AddChild(panel);
        return panel;
    }

    private static void Place(Control node, float x, float y, float width, float height)
    {
        node.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        node.Position = new Vector2(x, y);
        node.Size = new Vector2(width, height);
    }

    private static Control CreateArrow(Control source, string buttonName, float x, int step, Control panel, ModConfig config)
    {
        var arrow = (Control)source.Duplicate();
        Place(arrow, x, (HeaderHeight - 64) / 2, 64, 64);
        // 官方左右箭头各用一份 local_to_scene 材质，复制节点后要单独拷一份，否则悬停高亮会串到进阶条上。
        var image = arrow.GetNode<TextureRect>($"{buttonName}/TextureRect");
        image.Material = (Material)image.Material.Duplicate();
        var button = arrow.GetNode<NButton>(buttonName);
        button.Visible = true;
        button.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ =>
        {
            KakarotModConfig.GravityTraining = (KakarotGravityLevel)(((int)KakarotModConfig.GravityTraining + step + 4) % 4);
            config.Save();
            Refresh(panel);
        }));
        return arrow;
    }

    private static string Text(string key) => new LocString("settings_ui", $"KAKAROTMOD-GRAVITY_PANEL.{key}").GetFormattedText();

    private static string TierName(KakarotGravityLevel level) =>
        new LocString("settings_ui", $"KAKAROTMOD-GRAVITY_TRAINING.{level}").GetFormattedText();

    private static void Refresh(Control panel)
    {
        var level = KakarotModConfig.GravityTraining;
        panel.GetNode<RichTextLabel>("Header").Text = $"[gold]{Text("title")}[/gold]\n[b]{TierName(level)}[/b]";

        string bodyText;
        if (level == KakarotGravityLevel.Off)
        {
            bodyText = Text("off");
        }
        else
        {
            var enemy = new LocString("settings_ui", "KAKAROTMOD-GRAVITY_PANEL.enemy");
            enemy.Add("HpBoost", level switch { KakarotGravityLevel.TenTimes => 25, KakarotGravityLevel.FiftyTimes => 50, _ => 80 });
            enemy.Add("DamageBoost", level switch { KakarotGravityLevel.TenTimes => 10, KakarotGravityLevel.FiftyTimes => 20, _ => 30 });

            var lines = new List<string> { enemy.GetFormattedText() };
            for (var tier = KakarotGravityLevel.TenTimes; tier <= KakarotGravityLevel.HundredTimes; tier++)
            {
                var reward = Text($"reward{(int)tier}");
                if (level >= tier)
                {
                    lines.Add($"· {reward}");
                }
                else
                {
                    var locked = new LocString("settings_ui", "KAKAROTMOD-GRAVITY_PANEL.locked");
                    locked.Add("Tier", TierName(tier));
                    reward = reward.Replace("[gold]", "").Replace("[/gold]", "");
                    lines.Add($"[color={LockedColor}]· {reward}{locked.GetFormattedText()}[/color]");
                }
            }

            lines.Add(Text(level >= KakarotGravityLevel.FiftyTimes ? "weights" : "weightsNoCost"));
            lines.Add($"[color={LockedColor}]{Text("note")}[/color]");
            bodyText = string.Join("\n", lines);
        }

        // 正文高度随语言和档位变化，底板跟着正文走。
        var body = panel.GetNode<RichTextLabel>("Body");
        body.Text = bodyText;
        Callable.From(() =>
        {
            var height = body.GetContentHeight();
            body.Size = new Vector2(body.Size.X, height);
            panel.GetNode<NinePatchRect>("Background").Size = new Vector2(Width, body.Position.Y + height + 18f);
        }).CallDeferred();
    }
}
