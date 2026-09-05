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

// 贴图与材质工厂。
//
// 这一层是全 mod 共用的地基，不属于任何一张卡：CreateAdditiveMaterial 被引用 30 处，
// CreateRadialGlowTexture 14 处，连弗利萨的特效套件也在用。
//
// 🔴 所有渐变一律 ImageTexture 自己逐像素画，不用 GradientTexture1D / CurveTexture。
// 那些是异步生成的，就绪之前采样会退化成纯色。
public static partial class KakarotCombatPresentation
{
    // 程序化光束：形状与辉光全部由 shader 计算，不依赖 kamehameha_beam.png。
    // 失败时返回 false，调用方会自动退回贴图路径。
    // 横向棍贴图：宽度只有 4 像素（沿长度方向拉伸），明暗做在高度方向上，
    // 也就是棍子的粗细方向。这样 sprite 不用旋转，长宽缩放各管各的。
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

    // 沿宽度做圆柱明暗的贴图：边缘压暗 → 本体 → 偏一侧的高光。
    // 一根纯色矩形读起来是纸片，加上这条明暗曲线才像一根圆棍。
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

            // 到中轴的距离决定明暗，边缘最暗
            float rim = Mathf.Pow(Math.Abs(t - 0.5f) * 2f, 1.5f);
            Color c = body.Lerp(edge, rim);

            // 高光带：偏离中轴一点，才有受光方向
            float spec = Mathf.Exp(-Mathf.Pow((t - highlightAt) / 0.13f, 2f));
            c = c.Lerp(highlight, spec * 0.85f);

            for (int y = 0; y < height; y++)
            {
                img.SetPixel(x, y, c);
            }
        }

        return ImageTexture.CreateFromImage(img);
    }

    // 纯色硬边贴图。GradientTexture2D 是异步生成的，就绪前 UV 会退化，别用它。
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

    // 环形冲击波贴图：距中心 0.78 处最亮，两侧高斯衰减。
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

    // 碎片贴图：横向拉长、一端收成尖。
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

    // 程序生成的径向辉光。用 ImageTexture 而不是 GradientTexture2D——
    // 后者是异步生成的，就绪前采样会退化（龟波光束踩过这个坑）。
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
                // 外圈用 3.2 次方快速收掉，再叠一个高斯实心核。
                // 只有幂衰减时加法混合会糊成一大团紫雾，球心也读不出来。
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
