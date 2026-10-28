# Script to generate all PNG and ICO branding assets from Assets/Branding/loka-symbol.svg coordinates
Add-Type -AssemblyName System.Drawing

$csharpCode = @"
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;

public class BrandingRenderer
{
    public static Bitmap RenderLokaSymbol(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            float s = size / 64.0f;

            // 1. Top & Left Facet (Dark Navy #0F172A)
            using (var path1 = new GraphicsPath())
            {
                var pts1 = new PointF[]
                {
                    new PointF(32f * s, 4f * s),
                    new PointF(6f * s, 30f * s),
                    new PointF(16f * s, 40f * s),
                    new PointF(32f * s, 24f * s),
                    new PointF(48f * s, 40f * s),
                    new PointF(58f * s, 30f * s)
                };
                path1.AddPolygon(pts1);
                using (var brush1 = new SolidBrush(Color.FromArgb(15, 23, 42)))
                {
                    g.FillPath(brush1, path1);
                }
            }

            // 2. Bottom & Right Facet (LOKA Blue #2563EB)
            using (var path2 = new GraphicsPath())
            {
                var pts2 = new PointF[]
                {
                    new PointF(6f * s, 34f * s),
                    new PointF(32f * s, 60f * s),
                    new PointF(58f * s, 34f * s),
                    new PointF(48f * s, 24f * s),
                    new PointF(32f * s, 40f * s),
                    new PointF(16f * s, 24f * s)
                };
                path2.AddPolygon(pts2);
                using (var brush2 = new SolidBrush(Color.FromArgb(37, 99, 235)))
                {
                    g.FillPath(brush2, path2);
                }
            }

            // 3. Center Core Diamond Node (Sky Accent #38BDF8)
            using (var path3 = new GraphicsPath())
            {
                var pts3 = new PointF[]
                {
                    new PointF(32f * s, 26f * s),
                    new PointF(38f * s, 32f * s),
                    new PointF(32f * s, 38f * s),
                    new PointF(26f * s, 32f * s)
                };
                path3.AddPolygon(pts3);
                using (var brush3 = new SolidBrush(Color.FromArgb(56, 189, 248)))
                {
                    g.FillPath(brush3, path3);
                }
            }
        }
        return bmp;
    }

    public static void SaveMultiResolutionIco(string outputPath, int[] sizes)
    {
        var pngStreams = new List<byte[]>();
        var dimensions = new List<int>();

        foreach (var sz in sizes)
        {
            using (var bmp = RenderLokaSymbol(sz))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                pngStreams.Add(ms.ToArray());
                dimensions.Add(sz);
            }
        }

        using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write((ushort)0);
            bw.Write((ushort)1);
            bw.Write((ushort)pngStreams.Count);

            uint currentOffset = (uint)(6 + pngStreams.Count * 16);

            for (int i = 0; i < pngStreams.Count; i++)
            {
                int sz = dimensions[i];
                byte w = (byte)(sz >= 256 ? 0 : sz);
                byte h = (byte)(sz >= 256 ? 0 : sz);
                uint bytesInRes = (uint)pngStreams[i].Length;

                bw.Write(w);
                bw.Write(h);
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((ushort)1);
                bw.Write((ushort)32);
                bw.Write(bytesInRes);
                bw.Write(currentOffset);

                currentOffset += bytesInRes;
            }

            for (int i = 0; i < pngStreams.Count; i++)
            {
                bw.Write(pngStreams[i]);
            }
        }
    }
}
"@

Add-Type -TypeDefinition $csharpCode -ReferencedAssemblies "System.Drawing"

$root = $PSScriptRoot
if (-not $root) { $root = Get-Location }

# 1. Generate multi-resolution ICO
$icoSizes = @(16, 24, 32, 48, 64, 128, 256)
$icoPaths = @(
    (Join-Path $root "Assets\Branding\loka.ico"),
    (Join-Path $root "Loka.App\Resources\loka.ico"),
    (Join-Path $root "Loka.Installer\Resources\loka.ico")
)

foreach ($p in $icoPaths) {
    $dir = Split-Path $p
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [BrandingRenderer]::SaveMultiResolutionIco($p, $icoSizes)
    Write-Host "Generated multi-res ICO: $p" -ForegroundColor Green
}

# 2. Generate PNGs
$pngSizes = @(16, 24, 32, 48, 64, 128, 256, 512, 1024)
foreach ($sz in $pngSizes) {
    $bmp = [BrandingRenderer]::RenderLokaSymbol($sz)
    $targetPath = Join-Path $root "Loka.App\Resources\logo_$sz.png"
    $bmp.Save($targetPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Generated PNG: $targetPath ($sz x $sz)" -ForegroundColor Green
}

# Master logo.png (1024x1024 transparent)
$masterBmp = [BrandingRenderer]::RenderLokaSymbol(1024)
$masterPaths = @(
    (Join-Path $root "logo.png"),
    (Join-Path $root "Loka.App\Resources\logo.png"),
    (Join-Path $root "Loka.App\Resources\logo_primary.png"),
    (Join-Path $root "Loka.Installer\Resources\logo.png"),
    (Join-Path $root "Assets\Branding\logo.png")
)

foreach ($mp in $masterPaths) {
    $dir = Split-Path $mp
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $masterBmp.Save($mp, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host "Updated master logo: $mp (1024x1024)" -ForegroundColor Green
}
$masterBmp.Dispose()

Write-Host "All branding assets generated from loka-symbol.svg successfully!" -ForegroundColor Cyan
