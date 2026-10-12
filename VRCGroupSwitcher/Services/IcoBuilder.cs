using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace VRCGroupSwitcher.Services
{
    public static class IcoBuilder
    {
        public static void Build(string srcPath, string outIcoPath, string outPngPath)
        {
            if (!File.Exists(srcPath)) return;

            using var srcImg = Image.FromFile(srcPath);
            int masterSize = 512;
            using var masterBmp = new Bitmap(masterSize, masterSize, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(masterBmp))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(System.Drawing.Color.Transparent);

                using var path = new GraphicsPath();
                path.AddEllipse(2, 2, masterSize - 4, masterSize - 4);
                g.SetClip(path);

                var srcRect = new RectangleF(118f, 122f, 788f, 788f);
                var dstRect = new RectangleF(2f, 2f, masterSize - 4, masterSize - 4);
                g.DrawImage(srcImg, dstRect, srcRect, GraphicsUnit.Pixel);
                g.ResetClip();
            }

            // Save master 256x256 PNG
            using (var png256 = new Bitmap(256, 256, PixelFormat.Format32bppArgb))
            {
                using (var g256 = Graphics.FromImage(png256))
                {
                    g256.SmoothingMode = SmoothingMode.HighQuality;
                    g256.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g256.DrawImage(masterBmp, 0, 0, 256, 256);
                }
                png256.Save(outPngPath, ImageFormat.Png);
            }

            // Generate multi-resolution ICO
            int[] sizes = new[] { 256, 128, 64, 48, 32, 24, 16 };
            byte[][] pngBytes = new byte[sizes.Length][];

            for (int i = 0; i < sizes.Length; i++)
            {
                int sz = sizes[i];
                using var bmp = new Bitmap(sz, sz, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(masterBmp, 0, 0, sz, sz);
                }

                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                pngBytes[i] = ms.ToArray();
            }

            using var fs = new FileStream(outIcoPath, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(fs);

            // ICONDIR
            writer.Write((ushort)0); // reserved
            writer.Write((ushort)1); // type 1 = ICO
            writer.Write((ushort)sizes.Length); // count

            int offset = 6 + (sizes.Length * 16);

            for (int i = 0; i < sizes.Length; i++)
            {
                int sz = sizes[i];
                byte bSz = sz >= 256 ? (byte)0 : (byte)sz;

                writer.Write(bSz); // width
                writer.Write(bSz); // height
                writer.Write((byte)0); // colors
                writer.Write((byte)0); // reserved
                writer.Write((ushort)1); // planes
                writer.Write((ushort)32); // bpp
                writer.Write((uint)pngBytes[i].Length); // size
                writer.Write((uint)offset); // offset

                offset += pngBytes[i].Length;
            }

            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write(pngBytes[i]);
            }
        }
    }
}
