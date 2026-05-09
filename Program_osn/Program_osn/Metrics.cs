using System;
using System.Drawing;

namespace ImageEnhancement
{
    public static class Metrics
    {
        private static double[,] BitmapToGrayArray(Bitmap img)
        {
            int width = img.Width;
            int height = img.Height;
            var arr = new double[height, width];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int v = img.GetPixel(x, y).R; 
                    arr[y, x] = v;
                }
            }

            return arr;
        }

        public static double ComputeSSIM(Bitmap img1, Bitmap img2, int windowSize = 8)
        {
            if (img1.Width != img2.Width || img1.Height != img2.Height)
                throw new ArgumentException("Изображения должны быть одного размера");

            Bitmap g1 = ImageProcessors.ToGrayscale(img1);
            Bitmap g2 = ImageProcessors.ToGrayscale(img2);

            var x = BitmapToGrayArray(g1);
            var y = BitmapToGrayArray(g2);

            int width = g1.Width;
            int height = g1.Height;

            double L = 255.0;
            double k1 = 0.01;
            double k2 = 0.03;
            double C1 = (k1 * L) * (k1 * L);
            double C2 = (k2 * L) * (k2 * L);

            int half = windowSize / 2;

            double ssimSum = 0.0;
            int windowsCount = 0;

            for (int cy = half; cy < height - half; cy++)
            {
                for (int cx = half; cx < width - half; cx++)
                {
                    double meanX = 0.0;
                    double meanY = 0.0;
                    int count = 0;

                    for (int j = -half; j <= half; j++)
                    {
                        for (int i = -half; i <= half; i++)
                        {
                            int yy = cy + j;
                            int xx = cx + i;

                            double xv = x[yy, xx];
                            double yv = y[yy, xx];

                            meanX += xv;
                            meanY += yv;
                            count++;
                        }
                    }

                    meanX /= count;
                    meanY /= count;

                    double varX = 0.0;
                    double varY = 0.0;
                    double covXY = 0.0;

                    for (int j = -half; j <= half; j++)
                    {
                        for (int i = -half; i <= half; i++)
                        {
                            int yy = cy + j;
                            int xx = cx + i;

                            double xv = x[yy, xx];
                            double yv = y[yy, xx];

                            double dx = xv - meanX;
                            double dy = yv - meanY;

                            varX += dx * dx;
                            varY += dy * dy;
                            covXY += dx * dy;
                        }
                    }

                    varX /= (count - 1);
                    varY /= (count - 1);
                    covXY /= (count - 1);

                    double numerator = (2 * meanX * meanY + C1) * (2 * covXY + C2);
                    double denominator = (meanX * meanX + meanY * meanY + C1) * (varX + varY + C2);

                    double ssimLocal = numerator / denominator;
                    ssimSum += ssimLocal;
                    windowsCount++;
                }
            }

            if (windowsCount == 0) return 0.0;
            return ssimSum / windowsCount;
        }

        public static double ComputePSNR(Bitmap img1, Bitmap img2)
        {
            if (img1.Width != img2.Width || img1.Height != img2.Height)
                throw new ArgumentException("Изображения должны быть одного размера");

            Bitmap g1 = ImageProcessors.ToGrayscale(img1);
            Bitmap g2 = ImageProcessors.ToGrayscale(img2);

            long sumSq = 0;
            int n = g1.Width * g1.Height;

            for (int y = 0; y < g1.Height; y++)
            {
                for (int x = 0; x < g1.Width; x++)
                {
                    int d = g1.GetPixel(x, y).R - g2.GetPixel(x, y).R;
                    sumSq += d * d;
                }
            }

            double mse = (double)sumSq / n;
            if (mse <= 0) return 100.0;
            return 10.0 * Math.Log10(255.0 * 255.0 / mse);
        }

        /// <summary>
        /// Безэталонная оценка резкости — дисперсия лапласиана (Laplacian Variance).
        /// Чем выше значение, тем резче изображение.
        /// Работает без эталона: подходит для AI-методов с изменением разрешения.
        /// </summary>
        public static double ComputeSharpness(Bitmap img)
        {
            var gray = ImageProcessors.ToGrayscale(img);
            int w = gray.Width, h = gray.Height;
            if (w < 3 || h < 3) return 0;

            double sum = 0, sum2 = 0;
            int count = 0;

            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    // Ядро Лапласа 3×3: центр -4, соседи +1
                    double v = -4.0 * gray.GetPixel(x, y).R
                               + gray.GetPixel(x - 1, y).R
                               + gray.GetPixel(x + 1, y).R
                               + gray.GetPixel(x, y - 1).R
                               + gray.GetPixel(x, y + 1).R;
                    sum  += v;
                    sum2 += v * v;
                    count++;
                }
            }

            double mean = sum / count;
            return sum2 / count - mean * mean; // дисперсия
        }
    }
}
