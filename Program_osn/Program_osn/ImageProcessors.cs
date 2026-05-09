using System;
using System.Collections.Generic;
using System.Drawing;

namespace ImageEnhancement
{
    public static class ImageProcessors
    {
        public static Bitmap ToGrayscale(Bitmap source)
        {
            var result = new Bitmap(source.Width, source.Height);

            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    Color c = source.GetPixel(x, y);
                    int gray = (int)(0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
                    gray = Math.Clamp(gray, 0, 255);
                    result.SetPixel(x, y, Color.FromArgb(gray, gray, gray));
                }
            }

            return result;
        }

        public static Bitmap LinearContrastStretch(Bitmap source)
        {
            var gray = ToGrayscale(source);
            int width = gray.Width;
            int height = gray.Height;

            int min = 255;
            int max = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int v = gray.GetPixel(x, y).R;
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }

            if (max == min)
            {
                return (Bitmap)gray.Clone();
            }

            var result = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int v = gray.GetPixel(x, y).R;
                    double stretched = (double)(v - min) / (max - min) * 255.0;
                    int newVal = Math.Clamp((int)Math.Round(stretched), 0, 255);
                    result.SetPixel(x, y, Color.FromArgb(newVal, newVal, newVal));
                }
            }

            return result;
        }

        public static Bitmap HistogramEqualization(Bitmap source)
        {
            var gray = ToGrayscale(source);
            int width = gray.Width;
            int height = gray.Height;
            int totalPixels = width * height;

            int[] hist = new int[256];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int v = gray.GetPixel(x, y).R;
                    hist[v]++;
                }
            }

            int[] cdf = new int[256];
            cdf[0] = hist[0];
            for (int i = 1; i < 256; i++)
            {
                cdf[i] = cdf[i - 1] + hist[i];
            }

            int cdfMin = 0;
            for (int i = 0; i < 256; i++)
            {
                if (cdf[i] != 0)
                {
                    cdfMin = cdf[i];
                    break;
                }
            }

            int[] map = new int[256];
            for (int i = 0; i < 256; i++)
            {
                if (cdf[i] == 0)
                {
                    map[i] = 0;
                }
                else
                {
                    double val = (double)(cdf[i] - cdfMin) / (totalPixels - cdfMin) * 255.0;
                    map[i] = Math.Clamp((int)Math.Round(val), 0, 255);
                }
            }

            var result = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int v = gray.GetPixel(x, y).R;
                    int newVal = map[v];
                    result.SetPixel(x, y, Color.FromArgb(newVal, newVal, newVal));
                }
            }

            return result;
        }

        public static Bitmap MedianFilter(Bitmap source, int radius = 1)
        {
            var gray = ToGrayscale(source);
            int width = gray.Width;
            int height = gray.Height;
            var result = new Bitmap(width, height);

            int windowSize = 2 * radius + 1;
            int windowPixelCount = windowSize * windowSize;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    List<int> values = new List<int>(windowPixelCount);

                    for (int j = -radius; j <= radius; j++)
                    {
                        int yy = y + j;
                        if (yy < 0 || yy >= height) continue;

                        for (int i = -radius; i <= radius; i++)
                        {
                            int xx = x + i;
                            if (xx < 0 || xx >= width) continue;

                            int v = gray.GetPixel(xx, yy).R;
                            values.Add(v);
                        }
                    }

                    values.Sort();
                    int median = values[values.Count / 2];
                    result.SetPixel(x, y, Color.FromArgb(median, median, median));
                }
            }

            return result;
        }

        public static Bitmap WienerFilter(Bitmap source, int windowRadius = 1, double noiseVariance = 10.0)
        {
            var gray = ToGrayscale(source);
            int width = gray.Width;
            int height = gray.Height;
            var result = new Bitmap(width, height);

            int windowSize = 2 * windowRadius + 1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    List<double> window = new List<double>();

                    for (int j = -windowRadius; j <= windowRadius; j++)
                    {
                        int yy = y + j;
                        if (yy < 0 || yy >= height) continue;

                        for (int i = -windowRadius; i <= windowRadius; i++)
                        {
                            int xx = x + i;
                            if (xx < 0 || xx >= width) continue;

                            int v = gray.GetPixel(xx, yy).R;
                            window.Add(v);
                        }
                    }

                    double mu = 0.0;
                    foreach (double v in window) mu += v;
                    mu /= window.Count;

                    double sigma2 = 0.0;
                    foreach (double v in window)
                    {
                        double d = v - mu;
                        sigma2 += d * d;
                    }
                    sigma2 /= window.Count;

                    double xVal = gray.GetPixel(x, y).R;
                    double yVal;

                    if (sigma2 > noiseVariance && sigma2 > 0)
                    {
                        double k = (sigma2 - noiseVariance) / sigma2;
                        yVal = mu + k * (xVal - mu);
                    }
                    else
                    {
                        yVal = mu;
                    }

                    int outVal = Math.Clamp((int)Math.Round(yVal), 0, 255);
                    result.SetPixel(x, y, Color.FromArgb(outVal, outVal, outVal));
                }
            }

            return result;
        }

        public static Bitmap BoxBlur(Bitmap source, int radius = 1)
        {
            var gray = ToGrayscale(source);
            int width = gray.Width;
            int height = gray.Height;
            var result = new Bitmap(width, height);

            int windowSize = 2 * radius + 1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    long sum = 0;
                    int count = 0;

                    for (int j = -radius; j <= radius; j++)
                    {
                        int yy = y + j;
                        if (yy < 0 || yy >= height) continue;
                        for (int i = -radius; i <= radius; i++)
                        {
                            int xx = x + i;
                            if (xx < 0 || xx >= width) continue;
                            sum += gray.GetPixel(xx, yy).R;
                            count++;
                        }
                    }

                    int avg = count > 0 ? (int)(sum / count) : 0;
                    avg = Math.Clamp(avg, 0, 255);
                    result.SetPixel(x, y, Color.FromArgb(avg, avg, avg));
                }
            }

            return result;
        }

        public static Bitmap GammaCorrection(Bitmap source, double gamma = 2.2, double c = 1.0)
        {
            if (gamma <= 0) gamma = 1.0;
            var gray = ToGrayscale(source);
            int width = gray.Width;
            int height = gray.Height;
            var result = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double f = gray.GetPixel(x, y).R / 255.0;
                    double g = c * Math.Pow(f, gamma) * 255.0;
                    int val = Math.Clamp((int)Math.Round(g), 0, 255);
                    result.SetPixel(x, y, Color.FromArgb(val, val, val));
                }
            }

            return result;
        }

        public static Bitmap UnsharpMask(Bitmap source, double strength = 1.5, int blurRadius = 1)
        {
            var gray = ToGrayscale(source);
            var blurred = BoxBlur(gray, blurRadius);
            int width = gray.Width;
            int height = gray.Height;
            var result = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int f = gray.GetPixel(x, y).R;
                    int fBlur = blurred.GetPixel(x, y).R;
                    double g = f + strength * (f - fBlur);
                    int val = Math.Clamp((int)Math.Round(g), 0, 255);
                    result.SetPixel(x, y, Color.FromArgb(val, val, val));
                }
            }

            blurred.Dispose();
            return result;
        }

        public static Bitmap BilateralFilter(Bitmap source, int radius = 2, double sigmaSpace = 2, double sigmaRange = 30)
        {
            var gray = ToGrayscale(source);
            int width = gray.Width;
            int height = gray.Height;
            var result = new Bitmap(width, height);
            double sigmaS2 = 2 * sigmaSpace * sigmaSpace;
            double sigmaR2 = 2 * sigmaRange * sigmaRange;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int centerVal = gray.GetPixel(x, y).R;
                    double sumW = 0, sumWv = 0;

                    for (int j = -radius; j <= radius; j++)
                    {
                        int yy = y + j;
                        if (yy < 0 || yy >= height) continue;
                        for (int i = -radius; i <= radius; i++)
                        {
                            int xx = x + i;
                            if (xx < 0 || xx >= width) continue;
                            int v = gray.GetPixel(xx, yy).R;
                            double dSpace = i * i + j * j;
                            double dRange = (v - centerVal) * (v - centerVal);
                            double w = Math.Exp(-dSpace / sigmaS2 - dRange / sigmaR2);
                            sumW += w;
                            sumWv += w * v;
                        }
                    }

                    int outVal = sumW > 0 ? Math.Clamp((int)Math.Round(sumWv / sumW), 0, 255) : centerVal;
                    result.SetPixel(x, y, Color.FromArgb(outVal, outVal, outVal));
                }
            }

            return result;
        }

        public static Bitmap MorphologyErode(Bitmap source, int radius = 1)
        {
            var gray = ToGrayscale(source);
            int width = gray.Width;
            int height = gray.Height;
            var result = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int minVal = 255;
                    for (int j = -radius; j <= radius; j++)
                    {
                        int yy = y + j;
                        if (yy < 0 || yy >= height) continue;
                        for (int i = -radius; i <= radius; i++)
                        {
                            int xx = x + i;
                            if (xx < 0 || xx >= width) continue;
                            int v = gray.GetPixel(xx, yy).R;
                            if (v < minVal) minVal = v;
                        }
                    }
                    result.SetPixel(x, y, Color.FromArgb(minVal, minVal, minVal));
                }
            }
            return result;
        }

        public static Bitmap MorphologyDilate(Bitmap source, int radius = 1)
        {
            var gray = ToGrayscale(source);
            int width = gray.Width;
            int height = gray.Height;
            var result = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int maxVal = 0;
                    for (int j = -radius; j <= radius; j++)
                    {
                        int yy = y + j;
                        if (yy < 0 || yy >= height) continue;
                        for (int i = -radius; i <= radius; i++)
                        {
                            int xx = x + i;
                            if (xx < 0 || xx >= width) continue;
                            int v = gray.GetPixel(xx, yy).R;
                            if (v > maxVal) maxVal = v;
                        }
                    }
                    result.SetPixel(x, y, Color.FromArgb(maxVal, maxVal, maxVal));
                }
            }
            return result;
        }

        public static Bitmap MorphologyOpen(Bitmap source, int radius = 1)
        {
            var eroded = MorphologyErode(source, radius);
            var opened = MorphologyDilate(eroded, radius);
            eroded.Dispose();
            return opened;
        }

        public static Bitmap MorphologyClose(Bitmap source, int radius = 1)
        {
            var dilated = MorphologyDilate(source, radius);
            var closed = MorphologyErode(dilated, radius);
            dilated.Dispose();
            return closed;
        }

        public static Bitmap CannyEdgeDetection(Bitmap source, double lowThreshold = 0.15, double highThreshold = 0.35)
        {
            var gray = ToGrayscale(source);
            var blurred = BoxBlur(gray, 1);
            int width = blurred.Width;
            int height = blurred.Height;

            var mag = new double[width * height];
            var dir = new double[width * height];
            int[] sobelX = { -1, 0, 1, -2, 0, 2, -1, 0, 1 };
            int[] sobelY = { -1, -2, -1, 0, 0, 0, 1, 2, 1 };

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int gx = 0, gy = 0;
                    int k = 0;
                    for (int j = -1; j <= 1; j++)
                        for (int i = -1; i <= 1; i++)
                        {
                            int v = blurred.GetPixel(x + i, y + j).R;
                            gx += sobelX[k] * v;
                            gy += sobelY[k++] * v;
                        }
                    int idx = y * width + x;
                    mag[idx] = Math.Sqrt(gx * gx + gy * gy);
                    dir[idx] = Math.Atan2(gy, gx);
                }
            }
            blurred.Dispose();

            var suppressed = new double[width * height];
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int idx = y * width + x;
                    double angle = dir[idx] * 180 / Math.PI;
                    if (angle < 0) angle += 180;
                    double m = mag[idx];
                    double m1 = 0, m2 = 0;
                    if (angle < 22.5 || angle >= 157.5)
                    {
                        m1 = mag[idx - 1]; m2 = mag[idx + 1];
                    }
                    else if (angle < 67.5)
                    {
                        m1 = mag[idx - width - 1]; m2 = mag[idx + width + 1];
                    }
                    else if (angle < 112.5)
                    {
                        m1 = mag[idx - width]; m2 = mag[idx + width];
                    }
                    else
                    {
                        m1 = mag[idx - width + 1]; m2 = mag[idx + width - 1];
                    }
                    suppressed[idx] = (m >= m1 && m >= m2) ? m : 0;
                }
            }

            double maxMag = 0;
            for (int i = 0; i < suppressed.Length; i++)
                if (suppressed[i] > maxMag) maxMag = suppressed[i];
            if (maxMag <= 0) maxMag = 1;

            int low = (int)(lowThreshold * 255);
            int high = (int)(highThreshold * 255);
            var result = new Bitmap(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    int v = (int)(suppressed[idx] / maxMag * 255);
                    v = v >= high ? 255 : (v >= low ? 128 : 0);
                    result.SetPixel(x, y, Color.FromArgb(v, v, v));
                }
            }
            return result;
        }

        public static Bitmap DenoiseNLM(Bitmap source, int searchRadius = 3, int patchRadius = 1, double sigma = 15)
        {
            var gray = ToGrayscale(source);
            int width = gray.Width;
            int height = gray.Height;
            var result = new Bitmap(width, height);
            double sigma2 = 2 * sigma * sigma;
            int patchSize = 2 * patchRadius + 1;
            int patchPixels = patchSize * patchSize;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x < patchRadius || x >= width - patchRadius || y < patchRadius || y >= height - patchRadius)
                    {
                        result.SetPixel(x, y, gray.GetPixel(x, y));
                        continue;
                    }
                    double sumW = 0, sumWv = 0;
                    for (int sj = -searchRadius; sj <= searchRadius; sj++)
                    {
                        int qy = y + sj;
                        if (qy < patchRadius || qy >= height - patchRadius) continue;
                        for (int si = -searchRadius; si <= searchRadius; si++)
                        {
                            int qx = x + si;
                            if (qx < patchRadius || qx >= width - patchRadius) continue;

                            double dist = 0;
                            for (int pj = -patchRadius; pj <= patchRadius; pj++)
                            {
                                for (int pi = -patchRadius; pi <= patchRadius; pi++)
                                {
                                    int vp = gray.GetPixel(x + pi, y + pj).R;
                                    int vq = gray.GetPixel(qx + pi, qy + pj).R;
                                    dist += (vp - vq) * (vp - vq);
                                }
                            }
                            dist /= patchPixels;
                            double w = Math.Exp(-dist / sigma2);
                            sumW += w;
                            sumWv += w * gray.GetPixel(qx, qy).R;
                        }
                    }
                    int outVal = sumW > 0 ? Math.Clamp((int)Math.Round(sumWv / sumW), 0, 255) : gray.GetPixel(x, y).R;
                    result.SetPixel(x, y, Color.FromArgb(outVal, outVal, outVal));
                }
            }
            return result;
        }

        public static Bitmap Deblur(Bitmap source, int iterations = 3)
        {
            var gray = ToGrayscale(source);
            int width = gray.Width;
            int height = gray.Height;
            double[] kernel = { 1.0/16, 2.0/16, 1.0/16, 2.0/16, 4.0/16, 2.0/16, 1.0/16, 2.0/16, 1.0/16 };
            var g = new double[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    g[y * width + x] = gray.GetPixel(x, y).R;

            var f = new double[width * height];
            Array.Copy(g, f, g.Length);

            for (int iter = 0; iter < iterations; iter++)
            {
                var fBlur = new double[width * height];
                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        double sum = 0;
                        int k = 0;
                        for (int j = -1; j <= 1; j++)
                            for (int i = -1; i <= 1; i++)
                                sum += kernel[k++] * f[(y + j) * width + (x + i)];
                        fBlur[y * width + x] = sum;
                    }
                }
                for (int i = 0; i < f.Length; i++)
                {
                    if (fBlur[i] > 1e-6)
                        f[i] *= g[i] / fBlur[i];
                    f[i] = Math.Clamp(f[i], 0, 255);
                }
            }

            var result = new Bitmap(width, height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int v = Math.Clamp((int)Math.Round(f[y * width + x]), 0, 255);
                    result.SetPixel(x, y, Color.FromArgb(v, v, v));
                }
            return result;
        }

        public static Bitmap SeamCarving(Bitmap source, int removeColumns, int removeRows)
        {
            var bmp = (Bitmap)source.Clone();
            for (int s = 0; s < removeColumns && bmp.Width > 1; s++)
            {
                int w = bmp.Width;
                int h = bmp.Height;
                var energy = ComputeEnergyMap(bmp);
                var seam = FindVerticalSeam(energy, w, h);
                var next = RemoveVerticalSeam(bmp, seam);
                bmp.Dispose();
                bmp = next;
            }
            for (int s = 0; s < removeRows && bmp.Height > 1; s++)
            {
                int w = bmp.Width;
                int h = bmp.Height;
                var energy = ComputeEnergyMap(bmp);
                var seam = FindHorizontalSeam(energy, w, h);
                var next = RemoveHorizontalSeam(bmp, seam);
                bmp.Dispose();
                bmp = next;
            }
            return bmp;
        }

        private static double[] ComputeEnergyMap(Bitmap bmp)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            var gray = ToGrayscale(bmp);
            var E = new double[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int c = gray.GetPixel(x, y).R;
                    int dx = (x < w - 1 ? gray.GetPixel(x + 1, y).R : c) - (x > 0 ? gray.GetPixel(x - 1, y).R : c);
                    int dy = (y < h - 1 ? gray.GetPixel(x, y + 1).R : c) - (y > 0 ? gray.GetPixel(x, y - 1).R : c);
                    E[y * w + x] = Math.Abs(dx) + Math.Abs(dy);
                }
            }
            gray.Dispose();
            return E;
        }

        private static int[] FindVerticalSeam(double[] E, int w, int h)
        {
            var M = new double[w * h];
            for (int j = 0; j < w; j++)
                M[j] = E[j];
            for (int i = 1; i < h; i++)
            {
                for (int j = 0; j < w; j++)
                {
                    double a = j > 0 ? M[(i - 1) * w + (j - 1)] : double.MaxValue;
                    double b = M[(i - 1) * w + j];
                    double c = j < w - 1 ? M[(i - 1) * w + (j + 1)] : double.MaxValue;
                    M[i * w + j] = E[i * w + j] + Math.Min(Math.Min(a, b), c);
                }
            }
            var seam = new int[h];
            int jmin = 0;
            for (int j = 1; j < w; j++)
                if (M[(h - 1) * w + j] < M[(h - 1) * w + jmin])
                    jmin = j;
            seam[h - 1] = jmin;
            for (int i = h - 2; i >= 0; i--)
            {
                int j = seam[i + 1];
                double a = j > 0 ? M[i * w + (j - 1)] : double.MaxValue;
                double b = M[i * w + j];
                double c = j < w - 1 ? M[i * w + (j + 1)] : double.MaxValue;
                if (a <= b && a <= c) seam[i] = j - 1;
                else if (b <= c) seam[i] = j;
                else seam[i] = j + 1;
            }
            return seam;
        }

        private static Bitmap RemoveVerticalSeam(Bitmap bmp, int[] seam)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            var result = new Bitmap(w - 1, h);
            for (int y = 0; y < h; y++)
            {
                int skip = seam[y];
                for (int x = 0; x < skip; x++)
                    result.SetPixel(x, y, bmp.GetPixel(x, y));
                for (int x = skip + 1; x < w; x++)
                    result.SetPixel(x - 1, y, bmp.GetPixel(x, y));
            }
            return result;
        }

        private static int[] FindHorizontalSeam(double[] E, int w, int h)
        {
            var M = new double[w * h];
            for (int i = 0; i < h; i++)
                M[i * w] = E[i * w];
            for (int j = 1; j < w; j++)
            {
                for (int i = 0; i < h; i++)
                {
                    double a = i > 0 ? M[(i - 1) * w + (j - 1)] : double.MaxValue;
                    double b = M[i * w + (j - 1)];
                    double c = i < h - 1 ? M[(i + 1) * w + (j - 1)] : double.MaxValue;
                    M[i * w + j] = E[i * w + j] + Math.Min(Math.Min(a, b), c);
                }
            }
            var seam = new int[w];
            int imin = 0;
            for (int i = 1; i < h; i++)
                if (M[i * w + (w - 1)] < M[imin * w + (w - 1)])
                    imin = i;
            seam[w - 1] = imin;
            for (int j = w - 2; j >= 0; j--)
            {
                int i = seam[j + 1];
                double a = i > 0 ? M[(i - 1) * w + j] : double.MaxValue;
                double b = M[i * w + j];
                double c = i < h - 1 ? M[(i + 1) * w + j] : double.MaxValue;
                if (a <= b && a <= c) seam[j] = i - 1;
                else if (b <= c) seam[j] = i;
                else seam[j] = i + 1;
            }
            return seam;
        }

        public static Bitmap CLAHE(Bitmap source, int tileSize = 64, double clipLimit = 2.5)
        {
            var gray  = ToGrayscale(source);
            int w     = gray.Width;
            int h     = gray.Height;

            int tilesX = Math.Max(1, (w + tileSize - 1) / tileSize);
            int tilesY = Math.Max(1, (h + tileSize - 1) / tileSize);

            var lut = new byte[tilesY, tilesX, 256];

            for (int ty = 0; ty < tilesY; ty++)
            {
                for (int tx = 0; tx < tilesX; tx++)
                {
                    int x0 = tx * tileSize;
                    int y0 = ty * tileSize;
                    int x1 = Math.Min(x0 + tileSize, w);
                    int y1 = Math.Min(y0 + tileSize, h);
                    int n  = (x1 - x0) * (y1 - y0);

                    var hist   = new int[256];
                    for (int y = y0; y < y1; y++)
                        for (int x = x0; x < x1; x++)
                            hist[gray.GetPixel(x, y).R]++;

                    int clipAbs = Math.Max(1, (int)(clipLimit * n / 256));
                    int excess  = 0;
                    for (int i = 0; i < 256; i++)
                    {
                        if (hist[i] > clipAbs) { excess += hist[i] - clipAbs; hist[i] = clipAbs; }
                    }
                    int add = excess / 256;
                    for (int i = 0; i < 256; i++) hist[i] += add;

                    int cumsum = 0;
                    for (int i = 0; i < 256; i++)
                    {
                        cumsum += hist[i];
                        lut[ty, tx, i] = (byte)Math.Clamp(cumsum * 255 / n, 0, 255);
                    }
                }
            }

            var result = new Bitmap(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int bin = gray.GetPixel(x, y).R;

                    float fTx = (x - tileSize * 0.5f) / tileSize;
                    float fTy = (y - tileSize * 0.5f) / tileSize;
                    int   tx0 = Math.Clamp((int)Math.Floor(fTx), 0, tilesX - 1);
                    int   tx1 = Math.Clamp(tx0 + 1,              0, tilesX - 1);
                    int   ty0 = Math.Clamp((int)Math.Floor(fTy), 0, tilesY - 1);
                    int   ty1 = Math.Clamp(ty0 + 1,              0, tilesY - 1);
                    float wx  = Math.Max(0f, fTx - (float)Math.Floor(fTx));
                    float wy  = Math.Max(0f, fTy - (float)Math.Floor(fTy));

                    float v = lut[ty0, tx0, bin] * (1 - wx) * (1 - wy)
                            + lut[ty0, tx1, bin] * wx       * (1 - wy)
                            + lut[ty1, tx0, bin] * (1 - wx) * wy
                            + lut[ty1, tx1, bin] * wx       * wy;

                    byte bv = (byte)Math.Clamp((int)v, 0, 255);
                    result.SetPixel(x, y, Color.FromArgb(bv, bv, bv));
                }
            }

            gray.Dispose();
            return result;
        }

        public static Bitmap MultiScaleRetinex(Bitmap source, double[] sigmas, double[] weights)
        {
            int w = source.Width;
            int h = source.Height;

            var logR = new double[h, w];
            var logG = new double[h, w];
            var logB = new double[h, w];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = source.GetPixel(x, y);
                    logR[y, x] = Math.Log(c.R + 1.0);
                    logG[y, x] = Math.Log(c.G + 1.0);
                    logB[y, x] = Math.Log(c.B + 1.0);
                }
            }

            var msrR = new double[h, w];
            var msrG = new double[h, w];
            var msrB = new double[h, w];

            for (int s = 0; s < sigmas.Length; s++)
            {
                double sigma = sigmas[s];
                double w_s   = weights[s];

                var blurredR = GaussianBlurChannel(ExtractChannel(source, 0), sigma);
                var blurredG = GaussianBlurChannel(ExtractChannel(source, 1), sigma);
                var blurredB = GaussianBlurChannel(ExtractChannel(source, 2), sigma);

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        msrR[y, x] += w_s * (logR[y, x] - Math.Log(blurredR[y, x] + 1.0));
                        msrG[y, x] += w_s * (logG[y, x] - Math.Log(blurredG[y, x] + 1.0));
                        msrB[y, x] += w_s * (logB[y, x] - Math.Log(blurredB[y, x] + 1.0));
                    }
                }
            }

            static byte Norm(double[,] ch, int y, int x)
            {
                double min = double.MaxValue, max = double.MinValue;
                int H = ch.GetLength(0), W = ch.GetLength(1);
                for (int yy = 0; yy < H; yy++)
                    for (int xx = 0; xx < W; xx++)
                    {
                        if (ch[yy, xx] < min) min = ch[yy, xx];
                        if (ch[yy, xx] > max) max = ch[yy, xx];
                    }
                if (max == min) return 128;
                return (byte)Math.Clamp((ch[y, x] - min) / (max - min) * 255, 0, 255);
            }

            double minR = double.MaxValue, maxR = double.MinValue;
            double minG = double.MaxValue, maxG = double.MinValue;
            double minB = double.MaxValue, maxB = double.MinValue;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (msrR[y, x] < minR) minR = msrR[y, x]; if (msrR[y, x] > maxR) maxR = msrR[y, x];
                    if (msrG[y, x] < minG) minG = msrG[y, x]; if (msrG[y, x] > maxG) maxG = msrG[y, x];
                    if (msrB[y, x] < minB) minB = msrB[y, x]; if (msrB[y, x] > maxB) maxB = msrB[y, x];
                }
            }

            var result = new Bitmap(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    byte r = maxR > minR ? (byte)Math.Clamp((msrR[y, x] - minR) / (maxR - minR) * 255, 0, 255) : (byte)128;
                    byte g = maxG > minG ? (byte)Math.Clamp((msrG[y, x] - minG) / (maxG - minG) * 255, 0, 255) : (byte)128;
                    byte b = maxB > minB ? (byte)Math.Clamp((msrB[y, x] - minB) / (maxB - minB) * 255, 0, 255) : (byte)128;
                    result.SetPixel(x, y, Color.FromArgb(r, g, b));
                }
            }
            return result;
        }

        private static double[,] ExtractChannel(Bitmap bmp, int channel)
        {
            int w = bmp.Width, h = bmp.Height;
            var ch = new double[h, w];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    ch[y, x] = channel == 0 ? c.R : channel == 1 ? c.G : c.B;
                }
            return ch;
        }

        private static double[,] GaussianBlurChannel(double[,] ch, double sigma)
        {
            int h = ch.GetLength(0), w = ch.GetLength(1);
            int radius = Math.Max(1, (int)(sigma * 2.5));
            double[] kernel = new double[2 * radius + 1];
            double sum = 0;
            for (int i = -radius; i <= radius; i++)
            {
                kernel[i + radius] = Math.Exp(-i * i / (2 * sigma * sigma));
                sum += kernel[i + radius];
            }
            for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;

            var tmp = new double[h, w];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double v = 0;
                    for (int k = -radius; k <= radius; k++)
                        v += kernel[k + radius] * ch[y, Math.Clamp(x + k, 0, w - 1)];
                    tmp[y, x] = v;
                }

            var result = new double[h, w];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double v = 0;
                    for (int k = -radius; k <= radius; k++)
                        v += kernel[k + radius] * tmp[Math.Clamp(y + k, 0, h - 1), x];
                    result[y, x] = v;
                }
            return result;
        }

        private static Bitmap RemoveHorizontalSeam(Bitmap bmp, int[] seam)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            var result = new Bitmap(w, h - 1);
            for (int x = 0; x < w; x++)
            {
                int skip = seam[x];
                for (int y = 0; y < skip; y++)
                    result.SetPixel(x, y, bmp.GetPixel(x, y));
                for (int y = skip + 1; y < h; y++)
                    result.SetPixel(x, y - 1, bmp.GetPixel(x, y));
            }
            return result;
        }

        public static Bitmap LogarithmicTransform(Bitmap source, double c = 0)
        {
            var gray = ToGrayscale(source);
            int w = gray.Width, h = gray.Height;
            if (c <= 0) c = 255.0 / Math.Log(1 + 255);
            var result = new Bitmap(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int r = gray.GetPixel(x, y).R;
                    int s = Math.Clamp((int)(c * Math.Log(1.0 + r)), 0, 255);
                    result.SetPixel(x, y, Color.FromArgb(s, s, s));
                }
            return result;
        }

        public static Bitmap OtsuBinarization(Bitmap source)
        {
            var gray = ToGrayscale(source);
            int w = gray.Width, h = gray.Height;
            int total = w * h;

            var hist = new int[256];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    hist[gray.GetPixel(x, y).R]++;

            double bestVar = -1;
            int bestT = 0;
            double sumAll = 0;
            for (int i = 0; i < 256; i++) sumAll += i * hist[i];

            double sumFg = 0, wBg = 0;
            for (int t = 0; t < 256; t++)
            {
                wBg += hist[t];
                if (wBg == 0) continue;
                double wFg = total - wBg;
                if (wFg == 0) break;
                sumFg += t * hist[t];
                double muBg = sumFg / wBg;
                double muFg = (sumAll - sumFg) / wFg;
                double varB = wBg * wFg * (muBg - muFg) * (muBg - muFg);
                if (varB > bestVar) { bestVar = varB; bestT = t; }
            }

            var result = new Bitmap(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int val = gray.GetPixel(x, y).R >= bestT ? 255 : 0;
                    result.SetPixel(x, y, Color.FromArgb(val, val, val));
                }
            return result;
        }

        public static Bitmap PseudoColor(Bitmap source, int colormapIndex = 0)
        {
            var gray = ToGrayscale(source);
            int w = gray.Width, h = gray.Height;
            var result = new Bitmap(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double v = gray.GetPixel(x, y).R / 255.0; 
                    Color c = colormapIndex switch
                    {
                        0 => JetColor(v),    
                        1 => HotColor(v),   
                        _ => RainbowColor(v) 
                    };
                    result.SetPixel(x, y, c);
                }
            return result;
        }

        private static Color JetColor(double v)
        {
            double r = Math.Clamp(1.5 - Math.Abs(4 * v - 3), 0, 1);
            double g = Math.Clamp(1.5 - Math.Abs(4 * v - 2), 0, 1);
            double b = Math.Clamp(1.5 - Math.Abs(4 * v - 1), 0, 1);
            return Color.FromArgb((int)(r*255), (int)(g*255), (int)(b*255));
        }

        private static Color HotColor(double v)
        {
            double r = Math.Clamp(v * 3,       0, 1);
            double g = Math.Clamp(v * 3 - 1,   0, 1);
            double b = Math.Clamp(v * 3 - 2,   0, 1);
            return Color.FromArgb((int)(r*255), (int)(g*255), (int)(b*255));
        }

        private static Color RainbowColor(double v)
        {
            double h = (1.0 - v) * 240.0;
            double s = 1.0, br = 1.0;
            int hi = (int)(h / 60) % 6;
            double f = h / 60 - Math.Floor(h / 60);
            double p = 0, q = br * (1 - f * s), t2 = br * (1 - (1 - f) * s);
            return hi switch {
                0 => Color.FromArgb((int)(br*255), (int)(t2*255), (int)(p*255)),
                1 => Color.FromArgb((int)(q*255),  (int)(br*255), (int)(p*255)),
                2 => Color.FromArgb((int)(p*255),  (int)(br*255), (int)(t2*255)),
                3 => Color.FromArgb((int)(p*255),  (int)(q*255),  (int)(br*255)),
                4 => Color.FromArgb((int)(t2*255), (int)(p*255),  (int)(br*255)),
                _ => Color.FromArgb((int)(br*255), (int)(p*255),  (int)(q*255)),
            };
        }
    }
}
