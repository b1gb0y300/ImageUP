using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ImageEnhancementWpf
{
    /// <summary>Метаданные исходного изображения для PDF-отчёта.</summary>
    internal record ImageMetadata(string DpiStr, string ColorMode, string FileFormat, string FileSizeStr);

    internal static class PdfReportGenerator
    {
        // ── Полностью светлая цветовая схема ──────────────────────────────────
        private const string BgPage       = "#f4f6fb";
        private const string BgCard       = "#ffffff";
        private const string BgInner      = "#f1f5f9";
        private const string BgAccentFaint= "#eef2ff";
        private const string BorderMain   = "#e2e8f0";
        private const string BorderAccent = "#c7d2fe";
        private const string Accent       = "#4f46e5";
        private const string TxtPrimary   = "#0f172a";
        private const string TxtSecond    = "#334155";
        private const string TxtMuted     = "#64748b";
        private const string TxtVeryMuted = "#94a3b8";
        private const string Good         = "#16a34a";
        private const string GoodFaint    = "#dcfce7";
        private const string Warn         = "#d97706";
        private const string Bad          = "#dc2626";

        // ── Описания методов (для пользователя) ───────────────────────────────
        private static readonly Dictionary<string, string> MethodDescriptions =
            new(StringComparer.OrdinalIgnoreCase)
        {
            ["Линейное контрастирование"]           = "Равномерно распределяет яркость пикселей по всему диапазону 0–255. Устраняет засвеченность и чрезмерную затемнённость.",
            ["Линейное растяжение"]                 = "Равномерно распределяет яркость пикселей по всему диапазону 0–255. Устраняет засвеченность и чрезмерную затемнённость.",
            ["Эквализация гистограммы"]             = "Автоматически выравнивает распределение яркостей. Делает детали в тёмных и светлых областях более различимыми.",
            ["Медианный фильтр"]                    = "Убирает случайные шумовые пиксели, заменяя каждый медианным значением соседей. Хорошо сохраняет границы объектов.",
            ["Фильтр Винера"]                       = "Интеллектуальный фильтр шума, учитывающий соотношение сигнал/шум. Восстанавливает детали, сглаживая шум.",
            ["Нерезкое маскирование"]               = "Повышает резкость: из изображения вычитается его размытая копия, что усиливает контуры и мелкие детали.",
            ["Двусторонний фильтр"]                 = "Сглаживает шум, сохраняя чёткие границы. Учитывает пространственную близость и схожесть цветов пикселей.",
            ["Билатеральный фильтр"]                = "Сглаживает шум, сохраняя чёткие границы. Учитывает пространственную близость и схожесть цветов пикселей.",
            ["Гамма-коррекция"]                     = "Регулирует яркость нелинейно: γ < 1 осветляет изображение, γ > 1 затемняет.",
            ["Логарифмическое преобразование"]      = "Расширяет тёмные области и сжимает светлые — детали в тенях становятся более различимыми.",
            ["Детектор Кэнни"]                      = "Выделяет границы объектов. Находит резкие переходы яркости и строит тонкие линии контуров.",
            ["Морфологические операции"]            = "Изменяет форму светлых и тёмных областей: эрозия сужает, дилатация расширяет объекты на изображении.",
            ["Шумоподавление (NLM)"]                = "Продвинутый метод шумоподавления: сравнивает похожие блоки по всему изображению для точного восстановления деталей.",
            ["NLM (Non-Local Means)"]               = "Продвинутый метод шумоподавления: сравнивает похожие блоки по всему изображению для точного восстановления деталей.",
            ["Деконволюция (Ричардсон–Люси)"]       = "Восстанавливает резкость размытых изображений, итеративно устраняя эффект размытия (PSF).",
            ["Seam Carving"]                        = "Изменяет размер изображения, удаляя или добавляя наименее важные линии пикселей без искажения ключевых объектов.",
            ["Псевдоцвет (тепловая карта)"]         = "Преобразует яркости полутонового изображения в цветовую шкалу — наглядно показывает распределение интенсивности.",
            ["ESRGAN"]                              = "Нейросетевое увеличение разрешения в 4 раза. Генерирует реалистичные детали, которых нет в исходном изображении.",
            ["Real-ESRGAN (супер-разрешение ×4)"]   = "Нейросетевое увеличение разрешения в 4 раза. Генерирует реалистичные детали, которых нет в исходном изображении.",
            ["CLAHE"]                               = "Улучшенная эквализация гистограммы по локальным блокам. Выравнивает контраст даже в неравномерно освещённых областях.",
            ["Мультимасштабный ретинекс (MSR)"]     = "Имитирует восприятие яркости человеческим глазом. Выравнивает освещение и усиливает цвета на затемнённых фотографиях.",
            ["Бинаризация по методу Оцу"]           = "Автоматически находит оптимальный порог бинаризации, максимизируя межклассовую дисперсию. Переводит изображение в чёрно-белое.",
        };

        /// <summary>
        /// Генерирует PDF-отчёт об обработке изображения.
        /// </summary>
        public static void Generate(
            string outputPath,
            Bitmap original,
            Bitmap? processed,
            string methodName,
            string formula,
            string? principle,
            string fileName,
            string origDims,
            string procDims,
            double ssim,
            double psnr,
            double sharpOrig,
            double sharpProc,
            List<(string name, string formula)> pipeline,
            Dictionary<string, string>? parameters = null,
            ImageMetadata? metadata = null)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            byte[] origBytes  = BitmapToJpegBytes(original, 88);
            byte[]? procBytes = processed != null ? BitmapToJpegBytes(processed, 88) : null;

            // Имена методов цепочки
            var methodNames = new List<string>();
            foreach (var (name, _) in pipeline)
                methodNames.Add(name);
            if (methodNames.Count == 0)
                methodNames.Add(methodName);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(1.6f, Unit.Centimetre);
                    page.MarginVertical(1.4f, Unit.Centimetre);
                    page.DefaultTextStyle(ts => ts
                        .FontFamily("Segoe UI")
                        .FontSize(9.5f)
                        .FontColor(TxtSecond));

                    page.Background().Background(BgPage);

                    page.Content().Column(col =>
                    {
                        // ── СВЕТЛАЯ ШАПКА ─────────────────────────────────
                        col.Item()
                            .Background(BgCard)
                            .Border(1).BorderColor(BorderMain)
                            .Column(header =>
                            {
                                // Акцентная полоска сверху
                                header.Item().Height(4).Background(Accent);
                                header.Item()
                                    .PaddingHorizontal(20).PaddingVertical(16)
                                    .Row(row =>
                                    {
                                        row.RelativeItem().Column(c =>
                                        {
                                            c.Item().Row(r =>
                                            {
                                                r.AutoItem()
                                                    .Text("Image")
                                                    .FontSize(24).Bold().FontColor(TxtPrimary);
                                                r.AutoItem()
                                                    .Text("UP")
                                                    .FontSize(24).Bold().FontColor(Accent);
                                            });
                                            c.Item().PaddingTop(2)
                                                .Text("Отчёт об обработке изображения")
                                                .FontSize(10).FontColor(TxtMuted);
                                        });
                                        row.AutoItem().AlignRight().Column(c =>
                                        {
                                            c.Item().AlignRight()
                                                .Text(DateTime.Now.ToString("dd.MM.yyyy  HH:mm"))
                                                .FontSize(10).FontColor(TxtSecond);
                                            c.Item().PaddingTop(4).AlignRight()
                                                .Text(fileName)
                                                .FontSize(9).FontColor(TxtMuted);
                                        });
                                    });
                            });

                        col.Item().Height(16);

                        // ── ЦЕПОЧКА МЕТОДОВ (если несколько) ─────────────
                        if (pipeline.Count > 1)
                        {
                            col.Item()
                                .Background(BgAccentFaint)
                                .Border(1.5f).BorderColor(BorderAccent)
                                .PaddingHorizontal(16).PaddingVertical(10)
                                .Column(c =>
                                {
                                    c.Item()
                                        .Text("Применённая цепочка методов")
                                        .FontSize(8).Bold().FontColor(Accent)
                                        .LetterSpacing(0.07f);
                                    c.Item().Height(6);
                                    c.Item()
                                        .Text(string.Join("   →   ", methodNames))
                                        .FontSize(10).FontColor(TxtPrimary).Bold();
                                });
                            col.Item().Height(14);
                        }

                        // ── ИЗОБРАЖЕНИЯ ───────────────────────────────────
                        col.Item()
                            .Background(BgCard)
                            .Border(1).BorderColor(BorderMain)
                            .Padding(16)
                            .Column(c =>
                            {
                                c.Item()
                                    .Text("ИЗОБРАЖЕНИЯ")
                                    .FontSize(8).Bold().FontColor(TxtVeryMuted)
                                    .LetterSpacing(0.09f);
                                c.Item().Height(10);

                                if (procBytes != null)
                                {
                                    c.Item().Row(r =>
                                    {
                                        r.RelativeItem().Column(imgC =>
                                        {
                                            imgC.Item()
                                                .Border(1).BorderColor(BorderMain)
                                                .Image(origBytes);
                                            imgC.Item().PaddingTop(6).AlignCenter()
                                                .Text($"Оригинал  ·  {origDims}")
                                                .FontSize(8.5f).FontColor(TxtMuted);
                                        });
                                        r.ConstantItem(14);
                                        r.RelativeItem().Column(imgC =>
                                        {
                                            imgC.Item()
                                                .Border(1.5f).BorderColor(Good)
                                                .Image(procBytes);
                                            imgC.Item().PaddingTop(6).AlignCenter()
                                                .Text($"Результат  ·  {procDims}")
                                                .FontSize(8.5f).FontColor(Good);
                                        });
                                    });
                                }
                                else
                                {
                                    c.Item()
                                        .MaxWidth(320).AlignCenter()
                                        .Column(imgC =>
                                        {
                                            imgC.Item()
                                                .Border(1).BorderColor(BorderMain)
                                                .Image(origBytes);
                                            imgC.Item().PaddingTop(6).AlignCenter()
                                                .Text($"Оригинал  ·  {origDims}")
                                                .FontSize(8.5f).FontColor(TxtMuted);
                                        });
                                }

                                // Метаданные изображения
                                if (metadata != null)
                                {
                                    c.Item().Height(10);
                                    c.Item()
                                        .Background(BgInner)
                                        .Border(1).BorderColor(BorderMain)
                                        .PaddingHorizontal(12).PaddingVertical(6)
                                        .Row(r =>
                                        {
                                            MetaChip(r, "DPI",    metadata.DpiStr);
                                            MetaChip(r, "Режим",  metadata.ColorMode);
                                            MetaChip(r, "Формат", metadata.FileFormat);
                                            if (!string.IsNullOrEmpty(metadata.FileSizeStr))
                                                MetaChip(r, "Размер", metadata.FileSizeStr);
                                        });
                                }
                            });

                        col.Item().Height(14);

                        // ── ПРИМЕНЁННЫЕ МЕТОДЫ ────────────────────────────
                        col.Item()
                            .Background(BgCard)
                            .Border(1).BorderColor(BorderMain)
                            .Padding(16)
                            .Column(c =>
                            {
                                c.Item()
                                    .Text("ПРИМЕНЁННЫЕ МЕТОДЫ")
                                    .FontSize(8).Bold().FontColor(TxtVeryMuted)
                                    .LetterSpacing(0.09f);
                                c.Item().Height(10);

                                if (pipeline.Count <= 1)
                                {
                                    // Один метод — подробный блок
                                    string name = methodNames[0];
                                    string desc = GetDescription(name);

                                    c.Item()
                                        .Text(name)
                                        .FontSize(16).Bold().FontColor(TxtPrimary);

                                    c.Item().Height(6);
                                    c.Item()
                                        .Background(BgInner)
                                        .Border(1).BorderColor(BorderMain)
                                        .PaddingHorizontal(12).PaddingVertical(9)
                                        .Text(desc)
                                        .FontSize(10).FontColor(TxtSecond);

                                    // Принцип работы
                                    if (!string.IsNullOrWhiteSpace(principle) && principle != "—")
                                    {
                                        c.Item().Height(6);
                                        c.Item()
                                            .Text("Принцип работы")
                                            .FontSize(8).FontColor(TxtMuted);
                                        c.Item().Height(3);
                                        c.Item()
                                            .Background(BgInner)
                                            .Border(1).BorderColor(BorderMain)
                                            .PaddingHorizontal(12).PaddingVertical(8)
                                            .Text(principle)
                                            .FontSize(9.5f).FontColor(TxtMuted).Italic();
                                    }

                                    // Математическое описание
                                    if (!string.IsNullOrWhiteSpace(formula) && formula != "—")
                                    {
                                        c.Item().Height(8);
                                        c.Item()
                                            .Text("Математическое описание")
                                            .FontSize(8).FontColor(TxtMuted);
                                        c.Item().Height(4);
                                        c.Item()
                                            .Background("#fafafa")
                                            .Border(1).BorderColor(BorderAccent)
                                            .PaddingHorizontal(12).PaddingVertical(10)
                                            .Text(formula)
                                            .FontFamily("Cambria Math")
                                            .FontSize(10.5f)
                                            .FontColor(Accent);
                                    }

                                    // Параметры обработки
                                    if (parameters != null && parameters.Count > 0)
                                    {
                                        c.Item().Height(8);
                                        c.Item()
                                            .Text("Параметры обработки")
                                            .FontSize(8).FontColor(TxtMuted);
                                        c.Item().Height(4);
                                        c.Item()
                                            .Background(BgAccentFaint)
                                            .Border(1).BorderColor(BorderAccent)
                                            .PaddingHorizontal(12).PaddingVertical(8)
                                            .Column(pc =>
                                            {
                                                bool first = true;
                                                foreach (var kv in parameters)
                                                {
                                                    if (!first) pc.Item().Height(4);
                                                    first = false;
                                                    pc.Item().Row(pr =>
                                                    {
                                                        pr.RelativeItem()
                                                            .Text(kv.Key)
                                                            .FontSize(9.5f).FontColor(TxtSecond);
                                                        pr.AutoItem()
                                                            .Text(kv.Value)
                                                            .FontSize(9.5f).Bold().FontColor(Accent);
                                                    });
                                                }
                                            });
                                    }
                                }
                                else
                                {
                                    // Несколько методов — нумерованный список с формулами
                                    for (int i = 0; i < pipeline.Count; i++)
                                    {
                                        var (name, stepFormula) = pipeline[i];
                                        string desc = GetDescription(name);
                                        bool isLast = i == pipeline.Count - 1;

                                        if (i > 0) c.Item().Height(8);

                                        c.Item()
                                            .Background(isLast ? BgAccentFaint : BgInner)
                                            .Border(1).BorderColor(isLast ? BorderAccent : BorderMain)
                                            .PaddingHorizontal(12).PaddingVertical(9)
                                            .Row(r =>
                                            {
                                                r.ConstantItem(24).AlignMiddle()
                                                    .Text($"{i + 1}")
                                                    .FontSize(13).Bold()
                                                    .FontColor(isLast ? Accent : TxtMuted);
                                                r.RelativeItem().Column(mc =>
                                                {
                                                    mc.Item()
                                                        .Text(name)
                                                        .FontSize(11).Bold().FontColor(TxtPrimary);
                                                    mc.Item().PaddingTop(3)
                                                        .Text(desc)
                                                        .FontSize(9).FontColor(TxtMuted);
                                                    if (!string.IsNullOrWhiteSpace(stepFormula) && stepFormula != "—")
                                                    {
                                                        mc.Item().PaddingTop(5)
                                                            .Text(stepFormula)
                                                            .FontFamily("Cambria Math")
                                                            .FontSize(9.5f).FontColor(Accent);
                                                    }
                                                });
                                            });
                                    }
                                }
                            });

                        col.Item().Height(14);

                        // ── МЕТРИКИ КАЧЕСТВА ──────────────────────────────
                        col.Item()
                            .Background(BgCard)
                            .Border(1).BorderColor(BorderMain)
                            .Padding(16)
                            .Column(c =>
                            {
                                c.Item()
                                    .Text("МЕТРИКИ КАЧЕСТВА")
                                    .FontSize(8).Bold().FontColor(TxtVeryMuted)
                                    .LetterSpacing(0.09f);
                                c.Item().Height(10);

                                if (procBytes != null)
                                {
                                    bool sameSize = !double.IsNaN(ssim);

                                    MetricRow(c,
                                        "SSIM  —  структурное сходство",
                                        "Насколько похожи оригинал и результат (1.0 = идентичны)",
                                        sameSize ? $"{ssim:F4}" : "н/д",
                                        sameSize ? (ssim > 0.85 ? Good : ssim > 0.6 ? Warn : Bad) : TxtMuted,
                                        sameSize ? ssim : 0.5);

                                    c.Item().Height(2);

                                    MetricRow(c,
                                        "PSNR  —  отношение сигнал/шум",
                                        "Уровень искажений в децибелах (>30 дБ — хороший результат)",
                                        sameSize ? $"{psnr:F1} дБ" : "н/д",
                                        sameSize ? (psnr > 30 ? Good : psnr > 20 ? Warn : Bad) : TxtMuted,
                                        sameSize ? Math.Min(psnr / 50.0, 1.0) : 0.5);

                                    if (!double.IsNaN(sharpOrig) && !double.IsNaN(sharpProc))
                                    {
                                        double delta = sharpOrig > 0
                                            ? (sharpProc - sharpOrig) / sharpOrig : 0;
                                        string sharpLabel = delta > 0.005
                                            ? $"+{delta * 100:F0}%"
                                            : delta < -0.005
                                                ? $"{delta * 100:F0}%"
                                                : "без изменений";
                                        string sharpColor = delta > 0.005 ? Good
                                            : delta < -0.005 ? Bad : TxtMuted;
                                        string sharpDesc = delta > 0.005
                                            ? $"Резкость выросла: {sharpOrig:F0} → {sharpProc:F0}"
                                            : delta < -0.005
                                                ? $"Резкость снизилась: {sharpOrig:F0} → {sharpProc:F0}"
                                                : $"Резкость практически не изменилась ({sharpProc:F0})";

                                        c.Item().Height(2);
                                        MetricRow(c,
                                            "Резкость  —  оценка лапласианом",
                                            sharpDesc,
                                            sharpLabel,
                                            sharpColor,
                                            Math.Clamp(sharpProc / Math.Max(sharpOrig * 2, 1), 0, 1));
                                    }

                                    // Итоговое заключение
                                    c.Item().Height(12);
                                    string verdict;
                                    string verdictColor;
                                    string verdictBg;
                                    if (!sameSize)
                                    {
                                        verdict      = "Размеры изображений различаются — SSIM и PSNR не вычисляются.";
                                        verdictColor = TxtMuted;
                                        verdictBg    = BgInner;
                                    }
                                    else if (ssim > 0.85 && psnr > 30)
                                    {
                                        verdict      = "Отличный результат — высокое структурное сходство и малые искажения.";
                                        verdictColor = Good;
                                        verdictBg    = GoodFaint;
                                    }
                                    else if (ssim > 0.6 || psnr > 20)
                                    {
                                        verdict      = "Удовлетворительный результат — заметные изменения при приемлемом качестве.";
                                        verdictColor = Warn;
                                        verdictBg    = "#fffbeb";
                                    }
                                    else
                                    {
                                        verdict      = "Значительное преобразование — изображение существенно изменено.";
                                        verdictColor = Bad;
                                        verdictBg    = "#fef2f2";
                                    }

                                    c.Item()
                                        .Background(verdictBg)
                                        .Border(1).BorderColor(BorderMain)
                                        .PaddingHorizontal(12).PaddingVertical(8)
                                        .Row(r =>
                                        {
                                            r.ConstantItem(4).Background(verdictColor);
                                            r.ConstantItem(10);
                                            r.RelativeItem().AlignMiddle()
                                                .Text(verdict)
                                                .FontSize(9.5f).FontColor(verdictColor);
                                        });
                                }
                                else
                                {
                                    c.Item()
                                        .Background(BgInner)
                                        .Border(1).BorderColor(BorderMain)
                                        .PaddingHorizontal(12).PaddingVertical(10)
                                        .Text("Обработка ещё не выполнена. Метрики появятся после применения метода.")
                                        .FontSize(9).FontColor(TxtMuted);
                                }
                            });

                        // ── ПОДВАЛ ────────────────────────────────────────
                        col.Item().Height(20);
                        col.Item()
                            .BorderTop(1).BorderColor(BorderMain)
                            .PaddingTop(10)
                            .Row(r =>
                            {
                                r.RelativeItem()
                                    .Text("ImageUP — платформа улучшения и анализа изображений")
                                    .FontSize(8).FontColor(TxtVeryMuted);
                                r.AutoItem().AlignRight()
                                    .Text(DateTime.Now.ToString("dd.MM.yyyy  HH:mm"))
                                    .FontSize(8).FontColor(TxtVeryMuted);
                            });
                    });
                });
            }).GeneratePdf(outputPath);
        }

        // ── Вспомогательные методы ────────────────────────────────────────────

        private static void MetaChip(RowDescriptor row, string label, string value)
        {
            row.AutoItem()
                .PaddingRight(16)
                .Column(c =>
                {
                    c.Item().Text(label)
                        .FontSize(7.5f).FontColor(TxtVeryMuted);
                    c.Item().Text(value)
                        .FontSize(9).Bold().FontColor(TxtSecond);
                });
        }

        private static string GetDescription(string name)
        {
            if (MethodDescriptions.TryGetValue(name, out var d)) return d;
            foreach (var kv in MethodDescriptions)
                if (name.Contains(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                    kv.Key.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
            return "Метод цифровой обработки изображений.";
        }

        private static void MetricRow(
            ColumnDescriptor col,
            string label,
            string hint,
            string value,
            string valueColor,
            double fill)
        {
            col.Item()
                .BorderBottom(1).BorderColor(BorderMain)
                .PaddingVertical(9)
                .Row(r =>
                {
                    r.RelativeItem(5).Column(mc =>
                    {
                        mc.Item().Text(label)
                            .FontSize(9.5f).Bold().FontColor(TxtSecond);
                        mc.Item().PaddingTop(2).Text(hint)
                            .FontSize(8).FontColor(TxtVeryMuted);

                        double safeF = Math.Clamp(fill, 0.01, 0.99);
                        mc.Item().PaddingTop(5).Height(4).Row(barRow =>
                        {
                            barRow.RelativeItem((float)safeF).Background(valueColor);
                            barRow.RelativeItem((float)(1 - safeF)).Background(BorderMain);
                        });
                    });

                    r.ConstantItem(16);
                    r.AutoItem().AlignRight().AlignMiddle()
                        .Text(value)
                        .FontSize(12).Bold().FontColor(valueColor);
                });
        }

        private static byte[] BitmapToJpegBytes(Bitmap bmp, long quality)
        {
            int maxW = 900, maxH = 700;
            double ratio = Math.Min((double)maxW / bmp.Width, (double)maxH / bmp.Height);
            ratio = Math.Min(ratio, 1.0);
            int w = Math.Max(1, (int)(bmp.Width  * ratio));
            int h = Math.Max(1, (int)(bmp.Height * ratio));

            using var resized  = new Bitmap(bmp, w, h);
            using var ms       = new MemoryStream();
            var codec          = GetJpegCodec();
            var encParams      = new EncoderParameters(1);
            encParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            resized.Save(ms, codec, encParams);
            return ms.ToArray();
        }

        private static ImageCodecInfo GetJpegCodec() =>
            Array.Find(ImageCodecInfo.GetImageEncoders(),
                c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid)!;
    }
}
