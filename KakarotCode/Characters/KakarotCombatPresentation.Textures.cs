using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace KakarotMod.KakarotCode.Characters;

// 全局贴图与材质工厂，供角色与弗利萨特效共用。
// 程序渐变优先使用同步生成的 ImageTexture，避免异步纹理未就绪时采样退化。
public static partial class KakarotCombatPresentation
{
    // 横向圆柱贴图以高度方向承载明暗，便于 X/Y 分别控制长度和粗细。
    private static ImageTexture CreateCylinderTextureH(
        int thicknessPixels,
        Color edge,
        Color body,
        Color highlight,
        float highlightAt)
    {
        const int width = 4;
        var img = Image.CreateEmpty(width, thicknessPixels, false, Image.Format.Rgba8);
        for (int y = 0; y < thicknessPixels; y++)
        {
            float t = thicknessPixels <= 1 ? 0.5f : y / (float)(thicknessPixels - 1);
            float rim = Mathf.Pow(Math.Abs(t - 0.5f) * 2f, 1.5f);
            Color c = body.Lerp(edge, rim);
            float spec = Mathf.Exp(-Mathf.Pow((t - highlightAt) / 0.13f, 2f));
            c = c.Lerp(highlight, spec * 0.85f);
            for (int x = 0; x < width; x++)
            {
                img.SetPixel(x, y, c);
            }
        }

        return ImageTexture.CreateFromImage(img);
    }

    // 沿宽度建立圆柱明暗：边缘压暗并加入偏轴高光，避免纯色矩形呈现纸片感。
    private static ImageTexture CreateCylinderTexture(
        int width,
        Color edge,
        Color body,
        Color highlight,
        float highlightAt)
    {
        const int height = 4;
        var img = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (int x = 0; x < width; x++)
        {
            float t = width <= 1 ? 0.5f : x / (float)(width - 1);

            float rim = Mathf.Pow(Math.Abs(t - 0.5f) * 2f, 1.5f);
            Color c = body.Lerp(edge, rim);

            float spec = Mathf.Exp(-Mathf.Pow((t - highlightAt) / 0.13f, 2f));
            c = c.Lerp(highlight, spec * 0.85f);

            for (int y = 0; y < height; y++)
            {
                img.SetPixel(x, y, c);
            }
        }

        return ImageTexture.CreateFromImage(img);
    }

    // 使用同步 ImageTexture，避免异步 GradientTexture2D 未就绪时 UV 采样退化。
    private static ImageTexture CreateSolidTexture(int width, int height)
    {
        var img = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        img.Fill(Colors.White);
        return ImageTexture.CreateFromImage(img);
    }

    internal static CanvasItemMaterial CreateAdditiveMaterial()
    {
        return new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
    }

    private static Sprite2D CreateRadialGlowSprite(float innerAlpha, float outerAlpha)
    {
        var gradientTex = new GradientTexture2D
        {
            Width = 128,
            Height = 128,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.5f, 0f),
        };
        var grad = new Gradient();
        grad.SetColor(0, new Color(0.75f, 0.98f, 1f, innerAlpha));
        grad.SetColor(1, new Color(0.2f, 0.5f, 1f, outerAlpha));
        gradientTex.Gradient = grad;

        return new Sprite2D
        {
            Texture = gradientTex,
            Centered = true,
            Material = CreateAdditiveMaterial(),
        };
    }

    private static Sprite2D CreateWhiteGlow(float outerAlpha)
    {
        var gradientTex = new GradientTexture2D
        {
            Width = 128,
            Height = 128,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.5f, 0f),
        };
        var grad = new Gradient();
        grad.SetColor(0, new Color(1f, 1f, 0.96f, 1f));
        grad.SetColor(1, new Color(1f, 0.95f, 0.7f, outerAlpha));
        gradientTex.Gradient = grad;

        return new Sprite2D
        {
            Texture = gradientTex,
            Centered = true,
            Material = CreateAdditiveMaterial(),
        };
    }

    // 环形冲击波在指定半径形成高斯亮带。
    internal static ImageTexture CreateRingTexture(int size, float thickness, Color inner, Color outer)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                if (d >= 1f)
                {
                    img.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                    continue;
                }

                float band = Mathf.Exp(-Mathf.Pow((d - 0.78f) / thickness, 2f));
                Color c = inner.Lerp(outer, Mathf.Clamp((d - 0.5f) / 0.5f, 0f, 1f));
                c.A = band;
                img.SetPixel(x, y, c);
            }
        }

        return ImageTexture.CreateFromImage(img);
    }

    // 碎片贴图沿 X 方向拉长，并向尾端逐渐收尖。
    internal static ImageTexture CreateShardTexture(int width, int height, Color inner, Color outer)
    {
        var img = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = width <= 1 ? 0f : x / (float)(width - 1);
                float v = height <= 1 ? 0f : Math.Abs(y - (height - 1) * 0.5f) / ((height - 1) * 0.5f);

                float taper = Mathf.Pow(Mathf.Clamp(1f - u, 0f, 1f), 0.6f);
                float a = Mathf.Pow(Mathf.Clamp(taper - v * 0.9f, 0f, 1f), 1.4f);

                Color c = inner.Lerp(outer, u);
                c.A = a;
                img.SetPixel(x, y, c);
            }
        }

        return ImageTexture.CreateFromImage(img);
    }

    // 径向辉光使用同步 ImageTexture，外圈快速衰减并叠加实心核，以保持清晰中心。
    internal static ImageTexture CreateRadialGlowTexture(int size, Color inner, Color outer)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                if (d >= 1f)
                {
                    img.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                    continue;
                }

                Color c = inner.Lerp(outer, Mathf.Pow(d, 0.85f));
                float core = 0.55f * Mathf.Exp(-((d / 0.17f) * (d / 0.17f)));
                c.A = Mathf.Min(1f, Mathf.Pow(1f - d, 3.2f) + core);
                img.SetPixel(x, y, c);
            }
        }

        return ImageTexture.CreateFromImage(img);
    }

    private static GradientTexture2D CreateStreakTexture()
    {
        var tex = new GradientTexture2D
        {
            Width = 128,
            Height = 12,
            Fill = GradientTexture2D.FillEnum.Linear,
            FillFrom = new Vector2(0f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
        };
        var g = new Gradient();
        g.SetColor(0, new Color(1f, 1f, 1f, 0f));
        g.SetColor(1, new Color(1f, 1f, 1f, 0f));
        g.AddPoint(0.5f, new Color(1f, 1f, 1f, 0.95f));
        tex.Gradient = g;
        return tex;
    }

    private static Sprite2D CreateRingSprite(Color ringColor)
    {
        var tex = new GradientTexture2D
        {
            Width = 128,
            Height = 128,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.5f, 0f),
        };
        var transparent = new Color(ringColor.R, ringColor.G, ringColor.B, 0f);
        var g = new Gradient();
        g.SetColor(0, transparent);
        g.SetColor(1, transparent);
        g.AddPoint(0.74f, transparent);
        g.AddPoint(0.9f, ringColor);
        tex.Gradient = g;

        return new Sprite2D
        {
            Texture = tex,
            Centered = true,
            Material = CreateAdditiveMaterial(),
        };
    }
}
