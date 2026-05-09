using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ImageEnhancement
{
    public static class AiImageProcessors
    {
        
        private static IEnumerable<string> GetModelsCandidateDirs()
        {
            string? dir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                var modelsDir = Path.Combine(dir, "Models");
                if (Directory.Exists(modelsDir))
                    yield return modelsDir;
                dir = Path.GetDirectoryName(dir);
            }
        }

        private static string? FindModelFile(string fileName)
        {
            foreach (var modelsDir in GetModelsCandidateDirs())
            {
                var path = Path.Combine(modelsDir, fileName);
                if (File.Exists(path))
                    return path;
            }
            return null;
        }
        public static Bitmap? Esrgan(Bitmap source)
        {
            var names = new[] { "Real-ESRGAN-General-x4v3.onnx", "RealESRGAN-General-x4v3.onnx", "RealESRGAN_x4.onnx", "realesrgan-x4.onnx", "RealESRGAN.onnx" };
            string? path = names.Select(FindModelFile).FirstOrDefault(p => p != null);
            if (path == null)
                return null;

            return RunSuperResolution(path, source, scale: 4, tileSize: 128);
        }
        private static Bitmap? RunSuperResolution(string modelPath, Bitmap source, int scale, int tileSize)
        {
            int c = 3, h = source.Height, w = source.Width;
            int wPadded = ((w + tileSize - 1) / tileSize) * tileSize;
            int hPadded = ((h + tileSize - 1) / tileSize) * tileSize;
            int outWFull = wPadded * scale;
            int outHFull = hPadded * scale;
            int tileOut = tileSize * scale; // 512

            using var session = new InferenceSession(modelPath);
            string inputName = session.InputNames[0];

            Bitmap paddedSource = PadToSize(source, wPadded, hPadded);
            try
            {
                var outputFull = new float[3 * outHFull * outWFull];
                int nTy = hPadded / tileSize;
                int nTx = wPadded / tileSize;

                for (int ty = 0; ty < nTy; ty++)
                {
                    for (int tx = 0; tx < nTx; tx++)
                    {
                        int sx = tx * tileSize;
                        int sy = ty * tileSize;
                        using var tile = Crop(paddedSource, sx, sy, tileSize, tileSize);
                        float[] input = BitmapToFloatNchw(tile, tileSize, tileSize);
                        var inputTensor = new DenseTensor<float>(input, new[] { 1, c, tileSize, tileSize });
                        using var results = session.Run(new[] { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) });
                        var outTensor = results.First().AsTensor<float>();
                        int dy = ty * tileOut;
                        int dx = tx * tileOut;
                        for (int ch = 0; ch < 3; ch++)
                            for (int oy = 0; oy < tileOut; oy++)
                                for (int ox = 0; ox < tileOut; ox++)
                                    outputFull[ch * outHFull * outWFull + (dy + oy) * outWFull + (dx + ox)] = outTensor[0, ch, oy, ox];
                    }
                }

                int outW = w * scale;
                int outH = h * scale;
                if (outW == outWFull && outH == outHFull)
                    return FloatNchwToBitmap(outputFull, outW, outH);
                var cropped = new float[3 * outH * outW];
                for (int ch = 0; ch < 3; ch++)
                    for (int y = 0; y < outH; y++)
                        for (int x = 0; x < outW; x++)
                            cropped[ch * outH * outW + y * outW + x] = outputFull[ch * outHFull * outWFull + y * outWFull + x];
                return FloatNchwToBitmap(cropped, outW, outH);
            }
            finally
            {
                if (paddedSource != source)
                    paddedSource.Dispose();
            }
        }

        private static Bitmap PadToSize(Bitmap bmp, int targetW, int targetH)
        {
            if (bmp.Width == targetW && bmp.Height == targetH) return bmp;
            var result = new Bitmap(targetW, targetH, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(result))
            {
                g.Clear(Color.Black);
                g.DrawImage(bmp, 0, 0);
            }
            return result;
        }

        private static Bitmap Crop(Bitmap bmp, int x, int y, int width, int height)
        {
            var result = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(result))
                g.DrawImage(bmp, new Rectangle(0, 0, width, height), new Rectangle(x, y, width, height), GraphicsUnit.Pixel);
            return result;
        }
        private static float[] BitmapToFloatNchw(Bitmap bmp, int targetW, int targetH)
        {
            int w = bmp.Width, h = bmp.Height;
            Bitmap? toDispose = null;
            if (bmp.PixelFormat != PixelFormat.Format24bppRgb && bmp.PixelFormat != PixelFormat.Format32bppRgb)
            {
                var clone = new Bitmap(w, h, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(clone)) g.DrawImage(bmp, 0, 0);
                bmp = clone;
                toDispose = clone;
            }
            var result = new float[3 * targetH * targetW];
            var rect = new Rectangle(0, 0, w, h);
            var bd = bmp.LockBits(rect, ImageLockMode.ReadOnly, bmp.PixelFormat);
            try
            {
                int bpp = bd.PixelFormat == PixelFormat.Format32bppRgb ? 4 : 3;
                unsafe
                {
                    byte* p = (byte*)bd.Scan0;
                    int stride = bd.Stride;
                    for (int y = 0; y < h; y++)
                    {
                        byte* row = p + y * stride;
                        for (int x = 0; x < w; x++)
                        {
                            int b = row[x * bpp];
                            int g = row[x * bpp + 1];
                            int r = row[x * bpp + 2];
                            int idx = y * targetW + x;
                            result[0 * targetH * targetW + idx] = r / 255f;
                            result[1 * targetH * targetW + idx] = g / 255f;
                            result[2 * targetH * targetW + idx] = b / 255f;
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(bd);
                toDispose?.Dispose();
            }
            return result;
        }

        private static Bitmap FloatNchwToBitmap(float[] data, int w, int h)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            var rect = new Rectangle(0, 0, w, h);
            var bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                unsafe
                {
                    byte* p = (byte*)bd.Scan0;
                    int stride = bd.Stride;
                    for (int y = 0; y < h; y++)
                    {
                        byte* row = p + y * stride;
                        for (int x = 0; x < w; x++)
                        {
                            int idx = y * w + x;
                            float r = Math.Clamp(data[0 * h * w + idx], 0, 1);
                            float g = Math.Clamp(data[1 * h * w + idx], 0, 1);
                            float b = Math.Clamp(data[2 * h * w + idx], 0, 1);
                            row[x * 3] = (byte)(b * 255);
                            row[x * 3 + 1] = (byte)(g * 255);
                            row[x * 3 + 2] = (byte)(r * 255);
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(bd);
            }
            return bmp;
        }
    }
}
