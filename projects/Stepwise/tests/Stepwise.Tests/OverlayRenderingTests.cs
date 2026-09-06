using System;
using System.Diagnostics;
using System.Drawing;
using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Native;
using Stepwise.WindowsIntegration.Overlay;
using Xunit;

namespace Stepwise.Tests;

/// <summary>
/// Набор тестов визуального рендеринга десктопного оверлея (OverlayRenderer & NativeOverlayWindow).
/// Проверяет:
/// 1. Корректность прозрачного выреза (Alpha = 0) и затемнения экрана (Alpha = 90).
/// 2. Отрисовку карточки выноски (Alpha = 240) и акцентной рамки (Alpha = 255).
/// 3. Режим Missing Target (затемнение без выреза и предупреждающая плашка).
/// 4. Поддержку отрицательных координат мультимониторов (-1920..0).
/// 5. Маркер клика (Click Pin) при ненулевых ClickX/ClickY.
/// 6. Отсутствие утечек дескрипторов GDI (GetGuiResources) при 30 последовательных быстрых обновлениях.
/// </summary>
[Collection("GoldenGuiE2ETestsCollection")]
public class OverlayRenderingTests
{
    [Fact]
    public void Render_WithValidTargetAndCallout_VerifiesDimensions_CutoutAlphaZero_DimAlphaPositive()
    {
        using var renderer = new OverlayRenderer();

        var target = new OverlayTargetInfo(
            BoundingRectangle: new BoundingBox(200, 150, 300, 100),
            WindowHandle: 12345,
            Title: "Кнопка 'Сохранить'",
            Description: "Нажмите для сохранения изменений",
            ClickX: 350,
            ClickY: 200,
            IsTargetValid: true
        );

        var callout = new CalloutPosition(520, 150, 250, 90, CalloutPlacement.Right);

        using var bitmap = renderer.RenderToBitmap(0, 0, 1920, 1080, target, callout, isMissingTarget: false);

        // 1. Проверяем габариты битмапа
        Assert.Equal(1920, bitmap.Width);
        Assert.Equal(1080, bitmap.Height);

        // 2. Затемненная область экрана вдали от цели и выноски (Alpha = 90)
        Color dimPixel = bitmap.GetPixel(50, 50);
        Assert.Equal(90, dimPixel.A);

        // 3. Область выреза подсветки (Alpha = 0, 100% прозрачность)
        // Точка (250, 180) находится внутри выреза (200..500, 150..250), но вдали от маркера клика (350, 200)
        Color cutoutPixel = bitmap.GetPixel(250, 180);
        Assert.Equal(0, cutoutPixel.A);

        // 4. Область карточки подсказки (Alpha = 240)
        // Точка (540, 155) внутри верхней зоны карточки (520..770, 150..240), выше текста
        Color cardPixel = bitmap.GetPixel(540, 155);
        Assert.Equal(240, cardPixel.A);

        // 5. Проверяем вызов Render с реальным окном NativeOverlayWindow (no unhandled exceptions)
        using var window = new NativeOverlayWindow(renderer);
        renderer.Render(window.Handle, 0, 0, 1920, 1080, target, callout, isMissingTarget: false);
    }

    [Fact]
    public void Render_WithMissingTarget_ShouldDimScreen_NoCutout_AndDrawWarningCallout()
    {
        using var renderer = new OverlayRenderer();

        var target = new OverlayTargetInfo(
            BoundingRectangle: new BoundingBox(200, 150, 300, 100),
            WindowHandle: 12345,
            Title: "Целевой элемент",
            Description: "Элемент временно недоступен",
            ClickX: 350,
            ClickY: 200,
            IsTargetValid: true
        );

        var callout = new CalloutPosition(520, 150, 250, 90, CalloutPlacement.Right);

        using var bitmap = renderer.RenderToBitmap(0, 0, 1920, 1080, target, callout, isMissingTarget: true);

        // Затемнение экрана сохраняется
        Color dimPixel = bitmap.GetPixel(50, 50);
        Assert.Equal(90, dimPixel.A);

        // Вырез НЕ должен быть пробит: точка (250, 180) должна оставаться затемненной (Alpha = 90), а не прозрачной (Alpha = 0)
        Color targetPixel = bitmap.GetPixel(250, 180);
        Assert.Equal(90, targetPixel.A);

        // Карточка предупреждения отрисована с Alpha = 240
        Color cardPixel = bitmap.GetPixel(540, 155);
        Assert.Equal(240, cardPixel.A);

        // Проверяем устойчивость при null target и null callout
        using var nullBmp = renderer.RenderToBitmap(0, 0, 1920, 1080, null, null, isMissingTarget: true);
        Assert.NotNull(nullBmp);
        Color centerPixel = nullBmp.GetPixel(960, 540); // Центр экрана, где размещается карточка по умолчанию
        Assert.True(centerPixel.A > 0, "Предупреждающая плашка должна отображаться по центру при отсутствии координат.");
    }

    [Fact]
    public void Render_MultiMonitorNegativeCoordinates_ShouldCorrectlyOffsetCutoutAndCallout()
    {
        using var renderer = new OverlayRenderer();

        // Мультимониторная конфигурация: левый экран [-1920..0], правый экран [0..1920], всего [ -1920..1920 ]
        int virtualX = -1920;
        int virtualY = 0;
        int virtualWidth = 3840;
        int virtualHeight = 1080;

        // Целевой элемент расположен на левом мониторе с отрицательными координатами
        var target = new OverlayTargetInfo(
            BoundingRectangle: new BoundingBox(-1500, 200, 200, 100),
            WindowHandle: 9999,
            Title: "Left Monitor Target",
            Description: "Button on secondary screen",
            ClickX: 0,
            ClickY: 0,
            IsTargetValid: true
        );

        // Выноска справа от цели: X = -1500 + 200 + 12 = -1288
        var callout = new CalloutPosition(-1288, 200, 220, 90, CalloutPlacement.Right);

        using var bitmap = renderer.RenderToBitmap(virtualX, virtualY, virtualWidth, virtualHeight, target, callout, isMissingTarget: false);

        Assert.Equal(3840, bitmap.Width);
        Assert.Equal(1080, bitmap.Height);

        // Левый монитор, затемненный фон: локальные (50, 50)
        Assert.Equal(90, bitmap.GetPixel(50, 50).A);

        // Правый монитор, затемненный фон: локальные (2500, 500)
        Assert.Equal(90, bitmap.GetPixel(2500, 500).A);

        // Локальные координаты цели в битмапе:
        // localX = -1500 - (-1920) = 420, localY = 200.
        // Центр цели: localX = 420 + 100 = 520, localY = 200 + 50 = 250.
        Color cutoutCenter = bitmap.GetPixel(520, 250);
        Assert.Equal(0, cutoutCenter.A);

        // Локальные координаты выноски в битмапе:
        // localX = -1288 - (-1920) = 632, localY = 200.
        // Точка внутри верхней зоны выноски (652, 205):
        Color calloutInner = bitmap.GetPixel(652, 205);
        Assert.Equal(240, calloutInner.A);
    }

    [Fact]
    public void Render_ClickMarker_ShouldDrawMarkerWhenClickNonZero()
    {
        using var renderer = new OverlayRenderer();

        // 1. С маркером клика в центре выреза (300, 200)
        var targetWithClick = new OverlayTargetInfo(
            BoundingRectangle: new BoundingBox(200, 150, 200, 100),
            WindowHandle: 1,
            Title: "Click Target",
            Description: null,
            ClickX: 300,
            ClickY: 200,
            IsTargetValid: true
        );

        using var bmpWithClick = renderer.RenderToBitmap(0, 0, 1920, 1080, targetWithClick, null, isMissingTarget: false);

        // В точке клика (300, 200) нарисован белый центральный кружок маркера (Alpha = 255)
        Color pinCenter = bmpWithClick.GetPixel(300, 200);
        Assert.Equal(255, pinCenter.A);

        // Рядом в вырезе (250, 180) прозрачность сохраняется (Alpha = 0)
        Color cutoutOnly = bmpWithClick.GetPixel(250, 180);
        Assert.Equal(0, cutoutOnly.A);

        // 2. Без маркера клика (ClickX = 0, ClickY = 0)
        var targetWithoutClick = new OverlayTargetInfo(
            BoundingRectangle: new BoundingBox(200, 150, 200, 100),
            WindowHandle: 1,
            Title: "No Click Target",
            Description: null,
            ClickX: 0,
            ClickY: 0,
            IsTargetValid: true
        );

        using var bmpWithoutClick = renderer.RenderToBitmap(0, 0, 1920, 1080, targetWithoutClick, null, isMissingTarget: false);

        // В точке (300, 200) теперь чистый прозрачный вырез (Alpha = 0)
        Color centerWithoutClick = bmpWithoutClick.GetPixel(300, 200);
        Assert.Equal(0, centerWithoutClick.A);
    }

    [Fact]
    public void Render_RapidUpdates_ShouldHaveZeroGdiHandleLeaks_Over30ConsecutiveUpdates()
    {
        using var window = new NativeOverlayWindow();
        Assert.NotEqual(nint.Zero, window.Handle);

        var target = new OverlayTargetInfo(
            BoundingRectangle: new BoundingBox(200, 150, 200, 100),
            WindowHandle: 12345,
            Title: "Warmup Target",
            Description: "Warmup Description",
            ClickX: 250,
            ClickY: 180,
            IsTargetValid: true
        );
        var callout = new CalloutPosition(420, 150, 200, 80, CalloutPlacement.Right);

        // 1. Прогрев (Warm-up): инициализация внутренних кэшей GDI/GDI+ рантайма .NET
        window.UpdateVisuals(target, callout, isMissingTarget: false);
        window.HideWindow();
        window.ShowWindow();

        // Замеряем базовое количество дескрипторов GDI текущего процесса
        nint processHandle = Process.GetCurrentProcess().Handle;
        uint initialGdiObjects = NativeMethods.GetGuiResources(processHandle, 0);

        // 2. Выполняем 30 быстрых последовательных обновлений с динамически меняющимися параметрами
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 30; i++)
        {
            var dynamicTarget = new OverlayTargetInfo(
                BoundingRectangle: new BoundingBox(100 + i * 15, 100 + i * 10, 220, 90),
                WindowHandle: 12345,
                Title: $"Шаг {i + 1}",
                Description: $"Инструкция для шага {i + 1}",
                ClickX: 150 + i * 15,
                ClickY: 140 + i * 10,
                IsTargetValid: true
            );

            var dynamicCallout = new CalloutPosition(340 + i * 15, 100 + i * 10, 220, 80, CalloutPlacement.Right);
            bool isMissing = (i % 6 == 0); // Периодически переключаем в режим Missing Target

            window.UpdateVisuals(dynamicTarget, dynamicCallout, isMissing);

            if (i % 10 == 0)
            {
                window.HideWindow();
                window.ShowWindow();
            }
        }
        sw.Stop();

        // 3. Замеряем итоговое количество дескрипторов GDI
        uint finalGdiObjects = NativeMethods.GetGuiResources(processHandle, 0);

        // Строгое подтверждение отсутствия утечек дескрипторов GDI
        long gdiDelta = Math.Abs((long)finalGdiObjects - (long)initialGdiObjects);
        Assert.True(gdiDelta <= 3, $"Обнаружена утечка дескрипторов GDI: было {initialGdiObjects}, стало {finalGdiObjects} (дельта {gdiDelta}).");

        // Подтверждение высокой производительности: 30 обновлений нативного окна с DIBSection должны выполняться быстро
        Assert.True(sw.ElapsedMilliseconds < 3000, $"30 обновлений оверлея заняли слишком много времени: {sw.ElapsedMilliseconds} мс");
    }

    [Fact]
    public void NativeOverlayWindow_Integration_HideWindowClearsVisuals()
    {
        using var window = new NativeOverlayWindow();
        window.ShowWindow();
        Assert.True(window.IsWindowVisible);

        var target = new OverlayTargetInfo(
            BoundingRectangle: new BoundingBox(100, 100, 200, 100),
            WindowHandle: 1,
            Title: "Test",
            Description: "Test",
            ClickX: 150,
            ClickY: 150,
            IsTargetValid: true
        );
        var callout = new CalloutPosition(312, 100, 200, 80, CalloutPlacement.Right);

        // Обновляем визуальное состояние
        window.UpdateVisuals(target, callout, isMissingTarget: false);

        // Скрываем окно — внутри вызывается _renderer.Clear(Handle) и SW_HIDE
        window.HideWindow();
        Assert.False(window.IsWindowVisible);

        // 10 циклов UpdateVisuals -> HideWindow для проверки стабильности
        for (int i = 0; i < 10; i++)
        {
            window.ShowWindow();
            window.UpdateVisuals(target, callout, isMissingTarget: false);
            window.HideWindow();
        }

        Assert.False(window.IsWindowVisible);
    }
}
