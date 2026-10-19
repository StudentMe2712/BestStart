using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Services;
using PersonalProvocateur.App.Storage;
using UserControl = System.Windows.Controls.UserControl;

namespace PersonalProvocateur.App.UI.Controls;

/// <summary>
/// macOS Dynamic Island interactive mascot control supporting DynamicOrb, CyberVisor,
/// and CustomImage skins with Storyboard-driven animations (ADR-012).
/// </summary>
public partial class MascotControl : UserControl
{
    private static readonly string[] IdlePhrases =
    [
        "Ну давай, кликни!",
        "Я тут жду...",
        "Отвлекись на секунду!",
        "Ты не посмеешь нажать!"
    ];

    private static readonly string[] AttentionPhrases =
    [
        "Эй, ты спишь?!",
        "Не игнорируй меня!",
        "Пора взбодриться!",
        "Жми, пока не поздно!"
    ];

    private static readonly string[] ClickedPhrases =
    [
        "Погнали! 🎬",
        "Отличный выбор!",
        "Включаю ролик!"
    ];

    private readonly DispatcherTimer _attentionTimer;
    private Storyboard? _activeStoryboard;
    private Storyboard? _skinStoryboard;

    /// <summary>
    /// Gets or sets the asset service providing mascot sprites.
    /// </summary>
    public MascotAssetService AssetService { get; set; }

    /// <summary>
    /// Gets the current mascot state.
    /// </summary>
    public MascotState CurrentState { get; private set; } = MascotState.Hidden;

    /// <summary>
    /// Gets the active visual skin ("DynamicOrb", "CyberVisor", "CustomImage").
    /// </summary>
    public string CurrentSkin { get; private set; } = "DynamicOrb";

    /// <summary>
    /// Gets the custom file path if "CustomImage" skin is active.
    /// </summary>
    public string? CurrentCustomPath { get; private set; }

    /// <summary>
    /// Gets or sets the application settings store for pulling skin preferences.
    /// </summary>
    public SettingsStore? SettingsStore { get; set; }

    /// <summary>
    /// Occurs when the user clicks on the mascot control.
    /// </summary>
    public event EventHandler? MascotClicked;

    /// <summary>
    /// Initializes a new instance of <see cref="MascotControl"/>.
    /// </summary>
    /// <param name="assetService">Optional asset service instance.</param>
    /// <param name="settingsStore">Optional settings store instance.</param>
    public MascotControl(MascotAssetService? assetService = null, SettingsStore? settingsStore = null)
    {
        InitializeComponent();

        AssetService = assetService ?? new MascotAssetService();
        SettingsStore = settingsStore;

        _attentionTimer = new DispatcherTimer();
        _attentionTimer.Tick += OnAttentionTimerTick;

        Loaded += OnLoaded;
        Unloaded += (s, e) => StopAllAnimations();
    }

    /// <summary>
    /// Parameterless constructor for XAML and designer support.
    /// </summary>
    public MascotControl() : this(null, null)
    {
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var skin = SettingsStore?.CurrentSettings?.MascotSkin ?? CurrentSkin ?? "DynamicOrb";
        var customPath = SettingsStore?.CurrentSettings?.CustomMascotPath ?? CurrentCustomPath;
        ApplySkin(skin, customPath);
    }

    /// <summary>
    /// Applies the specified visual skin ("DynamicOrb", "CyberVisor", "CustomImage")
    /// and configures appropriate sub-containers and animations (ADR-012).
    /// </summary>
    /// <param name="skin">Skin identifier.</param>
    /// <param name="customPath">Optional custom file path for CustomImage skin.</param>
    public void ApplySkin(string skin, string? customPath = null)
    {
        if (skin != "CyberVisor" && skin != "CustomImage")
        {
            skin = "DynamicOrb";
        }

        CurrentSkin = skin;
        CurrentCustomPath = customPath;

        if (DynamicOrbContainer != null)
        {
            DynamicOrbContainer.Visibility = skin == "DynamicOrb" ? Visibility.Visible : Visibility.Collapsed;
        }

        if (CyberVisorContainer != null)
        {
            CyberVisorContainer.Visibility = skin == "CyberVisor" ? Visibility.Visible : Visibility.Collapsed;
        }

        if (CustomImageContainer != null)
        {
            CustomImageContainer.Visibility = skin == "CustomImage" ? Visibility.Visible : Visibility.Collapsed;
        }

        if (skin == "CustomImage")
        {
            LoadCustomImageOrSprite();
        }

        if (CurrentState == MascotState.Idle)
        {
            StartSkinIdleAnimation();
        }
        else if (CurrentState == MascotState.Attention)
        {
            StartSkinAttentionAnimation();
        }
    }

    private void LoadCustomImageOrSprite()
    {
        if (MascotImage == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(CurrentCustomPath) && File.Exists(CurrentCustomPath))
        {
            try
            {
                using var stream = new FileStream(CurrentCustomPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                MascotImage.Source = bitmap;
                return;
            }
            catch
            {
                // Fall back to asset service on decode failure
            }
        }

        MascotImage.Source = AssetService.GetSprite(CurrentState);
    }

    /// <summary>
    /// Switches the displayed mascot state and updates sprites/visuals.
    /// </summary>
    /// <param name="state">The target mascot state.</param>
    public void SetState(MascotState state)
    {
        CurrentState = state;

        if (CurrentSkin == "CustomImage")
        {
            LoadCustomImageOrSprite();
        }
        else if (MascotImage != null)
        {
            MascotImage.Source = AssetService.GetSprite(state);
        }
    }

    /// <summary>
    /// Gets the last speech text set on the control (for backwards compatibility).
    /// </summary>
    public string LastSpeechText { get; private set; } = string.Empty;

    /// <summary>
    /// Updates the speech text (speech bubble removed in ADR-018; stores text for tracking).
    /// </summary>
    /// <param name="text">The message to display.</param>
    public void SetSpeechText(string text)
    {
        LastSpeechText = text;
    }

    /// <summary>
    /// Sets a random provocative speech phrase matching the specified mascot state.
    /// </summary>
    /// <param name="state">The mascot state.</param>
    public void SetRandomSpeech(MascotState state)
    {
        var phrases = state switch
        {
            MascotState.Attention => AttentionPhrases,
            MascotState.Clicked => ClickedPhrases,
            _ => IdlePhrases
        };

        var selected = phrases[Random.Shared.Next(phrases.Length)];
        SetSpeechText(selected);
    }

    /// <summary>
    /// Plays the peek entrance animation (smooth spring slide-in from -130 to 14 over 380ms),
    /// automatically transitioning to <see cref="PlayIdleAnimation"/> upon completion (ADR-018).
    /// </summary>
    public void PlayPeekAnimation()
    {
        StopRunningAnimations();

        SetState(MascotState.Peek);

        MascotTranslate.Y = -130.0;
        MascotTranslate.X = 0.0;
        Opacity = 0.0;

        var sb = new Storyboard();

        var slideAnim = new DoubleAnimation
        {
            From = -130.0,
            To = 14.0,
            Duration = TimeSpan.FromMilliseconds(380),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(slideAnim, MascotTranslate);
        Storyboard.SetTargetProperty(slideAnim, new PropertyPath(TranslateTransform.YProperty));
        sb.Children.Add(slideAnim);

        var opacityAnim = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(opacityAnim, this);
        Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(UIElement.OpacityProperty));
        sb.Children.Add(opacityAnim);

        sb.Completed += (s, e) =>
        {
            if (ReferenceEquals(_activeStoryboard, sb))
            {
                PlayIdleAnimation();
            }
        };

        _activeStoryboard = sb;
        sb.Begin();
    }

    /// <summary>
    /// Plays the ambient idle animation (delicate vertical floating at top edge 0..6px,
    /// deep 2.5s AI Orb breathing or CyberVisor eye blinking), starting the 6–8s attention timer.
    /// </summary>
    public void PlayIdleAnimation()
    {
        StopRunningAnimations();

        SetState(MascotState.Idle);
        SetRandomSpeech(MascotState.Idle);

        Opacity = 1.0;
        MascotTranslate.Y = 14.0;
        MascotTranslate.X = 0.0;
        MascotScale.ScaleX = 1.0;
        MascotScale.ScaleY = 1.0;
        MascotRotate.Angle = 0.0;

        var sb = new Storyboard();

        // Subtle vertical floating at top edge (14 to 20px)
        var floatAnim = new DoubleAnimation
        {
            From = 14.0,
            To = 20.0,
            Duration = TimeSpan.FromMilliseconds(1400),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(floatAnim, MascotTranslate);
        Storyboard.SetTargetProperty(floatAnim, new PropertyPath(TranslateTransform.YProperty));
        sb.Children.Add(floatAnim);

        // Subtle breathing (ScaleY from 1.0 to 1.03)
        var breatheAnim = new DoubleAnimation
        {
            From = 1.0,
            To = 1.03,
            Duration = TimeSpan.FromMilliseconds(1400),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(breatheAnim, MascotScale);
        Storyboard.SetTargetProperty(breatheAnim, new PropertyPath(ScaleTransform.ScaleYProperty));
        sb.Children.Add(breatheAnim);

        _activeStoryboard = sb;
        sb.Begin();

        StartSkinIdleAnimation();
        ResetAttentionTimer();
    }

    /// <summary>
    /// Plays the attention animation (energetic bouncing jumps, rocking shake, and accelerated pulses),
    /// returning to idle upon completion.
    /// </summary>
    public void PlayAttentionAnimation()
    {
        StopRunningAnimations();

        SetState(MascotState.Attention);
        SetRandomSpeech(MascotState.Attention);

        var sb = new Storyboard();

        // Bouncing jump downwards from notch (TranslateY 14 to 28)
        var bounceAnim = new DoubleAnimation
        {
            From = 14.0,
            To = 28.0,
            Duration = TimeSpan.FromMilliseconds(220),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(3),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(bounceAnim, MascotTranslate);
        Storyboard.SetTargetProperty(bounceAnim, new PropertyPath(TranslateTransform.YProperty));
        sb.Children.Add(bounceAnim);

        // Energetic rocking shake
        var shakeAnim = new DoubleAnimation
        {
            From = -4.0,
            To = 4.0,
            Duration = TimeSpan.FromMilliseconds(120),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(6),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(shakeAnim, MascotRotate);
        Storyboard.SetTargetProperty(shakeAnim, new PropertyPath(RotateTransform.AngleProperty));
        sb.Children.Add(shakeAnim);

        sb.Completed += (s, e) =>
        {
            if (ReferenceEquals(_activeStoryboard, sb))
            {
                PlayIdleAnimation();
            }
        };

        _activeStoryboard = sb;
        sb.Begin();

        StartSkinAttentionAnimation();
    }

    /// <summary>
    /// Plays the click response animation (expanding light pulse with scale up to 1.25 over 100ms,
    /// followed by smooth upward exit to -130 over 220ms with Opacity fading to 0).
    /// </summary>
    /// <param name="onCompleted">Action invoked after the animation concludes.</param>
    public void PlayClickAnimation(Action onCompleted)
    {
        StopAllAnimations();

        SetState(MascotState.Clicked);
        SetRandomSpeech(MascotState.Clicked);

        var sb = new Storyboard();

        // Expanding light pulse: scale up to 1.25 in 100ms (ADR-012)
        var scaleXAnim = new DoubleAnimation
        {
            From = 1.0,
            To = 1.25,
            Duration = TimeSpan.FromMilliseconds(100),
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(scaleXAnim, MascotScale);
        Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath(ScaleTransform.ScaleXProperty));
        sb.Children.Add(scaleXAnim);

        var scaleYAnim = new DoubleAnimation
        {
            From = 1.0,
            To = 1.25,
            Duration = TimeSpan.FromMilliseconds(100),
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(scaleYAnim, MascotScale);
        Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath(ScaleTransform.ScaleYProperty));
        sb.Children.Add(scaleYAnim);

        // Smooth exit upwards: MascotTranslate.Y from 14 to -130 over 220ms
        var exitAnim = new DoubleAnimation
        {
            From = 14.0,
            To = -130.0,
            BeginTime = TimeSpan.FromMilliseconds(100),
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(exitAnim, MascotTranslate);
        Storyboard.SetTargetProperty(exitAnim, new PropertyPath(TranslateTransform.YProperty));
        sb.Children.Add(exitAnim);

        // Fade Opacity from 1.0 to 0.0
        var opacityAnim = new DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            BeginTime = TimeSpan.FromMilliseconds(100),
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(opacityAnim, this);
        Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(UIElement.OpacityProperty));
        sb.Children.Add(opacityAnim);

        sb.Completed += (s, e) =>
        {
            _activeStoryboard = null;
            onCompleted?.Invoke();
        };

        _activeStoryboard = sb;
        sb.Begin();
    }

    private void StartSkinIdleAnimation()
    {
        StopSkinAnimations();

        if (CurrentSkin == "DynamicOrb")
        {
            if (OrbCoreScale == null) return;

            var sb = new Storyboard();

            // Core breathing: Scale 0.92 to 1.08 over 1250ms (full cycle 2.5s)
            var coreScaleX = new DoubleAnimation
            {
                From = 0.92,
                To = 1.08,
                Duration = TimeSpan.FromMilliseconds(1250),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(coreScaleX, OrbCoreScale);
            Storyboard.SetTargetProperty(coreScaleX, new PropertyPath(ScaleTransform.ScaleXProperty));
            sb.Children.Add(coreScaleX);

            var coreScaleY = new DoubleAnimation
            {
                From = 0.92,
                To = 1.08,
                Duration = TimeSpan.FromMilliseconds(1250),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(coreScaleY, OrbCoreScale);
            Storyboard.SetTargetProperty(coreScaleY, new PropertyPath(ScaleTransform.ScaleYProperty));
            sb.Children.Add(coreScaleY);

            // Ring subtle breathing
            if (OrbInnerScale != null)
            {
                var innerAnim = new DoubleAnimation
                {
                    From = 0.95,
                    To = 1.05,
                    Duration = TimeSpan.FromMilliseconds(1250),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                Storyboard.SetTarget(innerAnim, OrbInnerScale);
                Storyboard.SetTargetProperty(innerAnim, new PropertyPath(ScaleTransform.ScaleXProperty));
                sb.Children.Add(innerAnim);

                var innerAnimY = innerAnim.Clone();
                Storyboard.SetTarget(innerAnimY, OrbInnerScale);
                Storyboard.SetTargetProperty(innerAnimY, new PropertyPath(ScaleTransform.ScaleYProperty));
                sb.Children.Add(innerAnimY);
            }

            if (OrbOuterScale != null)
            {
                var outerAnim = new DoubleAnimation
                {
                    From = 0.92,
                    To = 1.08,
                    Duration = TimeSpan.FromMilliseconds(1250),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                Storyboard.SetTarget(outerAnim, OrbOuterScale);
                Storyboard.SetTargetProperty(outerAnim, new PropertyPath(ScaleTransform.ScaleXProperty));
                sb.Children.Add(outerAnim);

                var outerAnimY = outerAnim.Clone();
                Storyboard.SetTarget(outerAnimY, OrbOuterScale);
                Storyboard.SetTargetProperty(outerAnimY, new PropertyPath(ScaleTransform.ScaleYProperty));
                sb.Children.Add(outerAnimY);
            }

            if (OrbOuterRing != null)
            {
                var opacityAnim = new DoubleAnimation
                {
                    From = 0.20,
                    To = 0.35,
                    Duration = TimeSpan.FromMilliseconds(1250),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                Storyboard.SetTarget(opacityAnim, OrbOuterRing);
                Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(UIElement.OpacityProperty));
                sb.Children.Add(opacityAnim);
            }

            _skinStoryboard = sb;
            sb.Begin();
        }
        else if (CurrentSkin == "CyberVisor")
        {
            if (VisorLeftScale == null || VisorRightScale == null) return;

            var sb = new Storyboard();

            // Periodic eye blink in 2.5s loop: ScaleY drops to 0.05 at 2.2s for 100ms
            var blinkLeft = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(2500),
                RepeatBehavior = RepeatBehavior.Forever
            };
            blinkLeft.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            blinkLeft.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2200))));
            blinkLeft.KeyFrames.Add(new LinearDoubleKeyFrame(0.05, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2280))));
            blinkLeft.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2360))));
            blinkLeft.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2500))));

            Storyboard.SetTarget(blinkLeft, VisorLeftScale);
            Storyboard.SetTargetProperty(blinkLeft, new PropertyPath(ScaleTransform.ScaleYProperty));
            sb.Children.Add(blinkLeft);

            var blinkRight = blinkLeft.Clone();
            Storyboard.SetTarget(blinkRight, VisorRightScale);
            Storyboard.SetTargetProperty(blinkRight, new PropertyPath(ScaleTransform.ScaleYProperty));
            sb.Children.Add(blinkRight);

            _skinStoryboard = sb;
            sb.Begin();
        }
    }

    private void StartSkinAttentionAnimation()
    {
        StopSkinAnimations();

        if (CurrentSkin == "DynamicOrb")
        {
            if (OrbCoreScale == null) return;

            var sb = new Storyboard();

            // Energetic fast pulsation: 0.8s cycle (400ms each way)
            var pulseAnim = new DoubleAnimation
            {
                From = 0.88,
                To = 1.15,
                Duration = TimeSpan.FromMilliseconds(400),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(pulseAnim, OrbCoreScale);
            Storyboard.SetTargetProperty(pulseAnim, new PropertyPath(ScaleTransform.ScaleXProperty));
            sb.Children.Add(pulseAnim);

            var pulseAnimY = pulseAnim.Clone();
            Storyboard.SetTarget(pulseAnimY, OrbCoreScale);
            Storyboard.SetTargetProperty(pulseAnimY, new PropertyPath(ScaleTransform.ScaleYProperty));
            sb.Children.Add(pulseAnimY);

            // Bright ring flashes
            if (OrbOuterRing != null)
            {
                var flashOuter = new DoubleAnimation
                {
                    From = 0.25,
                    To = 0.65,
                    Duration = TimeSpan.FromMilliseconds(400),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                Storyboard.SetTarget(flashOuter, OrbOuterRing);
                Storyboard.SetTargetProperty(flashOuter, new PropertyPath(UIElement.OpacityProperty));
                sb.Children.Add(flashOuter);
            }

            if (OrbMiddleRing != null)
            {
                var flashMiddle = new DoubleAnimation
                {
                    From = 0.45,
                    To = 0.80,
                    Duration = TimeSpan.FromMilliseconds(400),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                Storyboard.SetTarget(flashMiddle, OrbMiddleRing);
                Storyboard.SetTargetProperty(flashMiddle, new PropertyPath(UIElement.OpacityProperty));
                sb.Children.Add(flashMiddle);
            }

            _skinStoryboard = sb;
            sb.Begin();
        }
        else if (CurrentSkin == "CyberVisor")
        {
            if (VisorLeftScale == null || VisorRightScale == null) return;

            var sb = new Storyboard();

            // Fast squint and blink
            var fastSquint = new DoubleAnimation
            {
                From = 0.35,
                To = 1.1,
                Duration = TimeSpan.FromMilliseconds(200),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(fastSquint, VisorLeftScale);
            Storyboard.SetTargetProperty(fastSquint, new PropertyPath(ScaleTransform.ScaleYProperty));
            sb.Children.Add(fastSquint);

            var fastSquintR = fastSquint.Clone();
            Storyboard.SetTarget(fastSquintR, VisorRightScale);
            Storyboard.SetTargetProperty(fastSquintR, new PropertyPath(ScaleTransform.ScaleYProperty));
            sb.Children.Add(fastSquintR);

            _skinStoryboard = sb;
            sb.Begin();
        }
    }

    private void OnAttentionTimerTick(object? sender, EventArgs e)
    {
        _attentionTimer.Stop();
        if (CurrentState == MascotState.Idle)
        {
            PlayAttentionAnimation();
        }
    }

    private void ResetAttentionTimer()
    {
        _attentionTimer.Stop();
        var seconds = Random.Shared.Next(6, 9);
        _attentionTimer.Interval = TimeSpan.FromSeconds(seconds);
        _attentionTimer.Start();
    }

    private void StopSkinAnimations()
    {
        if (_skinStoryboard != null)
        {
            _skinStoryboard.Stop();
            _skinStoryboard = null;
        }

        if (OrbCoreScale != null)
        {
            OrbCoreScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            OrbCoreScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }
        if (OrbInnerScale != null)
        {
            OrbInnerScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            OrbInnerScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }
        if (OrbMiddleScale != null)
        {
            OrbMiddleScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            OrbMiddleScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }
        if (OrbOuterScale != null)
        {
            OrbOuterScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            OrbOuterScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }
        if (OrbOuterRing != null)
        {
            OrbOuterRing.BeginAnimation(UIElement.OpacityProperty, null);
        }
        if (OrbMiddleRing != null)
        {
            OrbMiddleRing.BeginAnimation(UIElement.OpacityProperty, null);
        }
        if (VisorLeftScale != null)
        {
            VisorLeftScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }
        if (VisorRightScale != null)
        {
            VisorRightScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }
    }

    private void StopRunningAnimations()
    {
        _attentionTimer.Stop();

        if (_activeStoryboard != null)
        {
            _activeStoryboard.Stop();
            _activeStoryboard = null;
        }

        StopSkinAnimations();

        BeginAnimation(UIElement.OpacityProperty, null);
        MascotScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        MascotScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        MascotRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        MascotTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        MascotTranslate.BeginAnimation(TranslateTransform.YProperty, null);
    }

    /// <summary>
    /// Stops all running Storyboard animations and the attention timer to prevent memory leaks.
    /// Resets transformation properties to their default baseline.
    /// </summary>
    public void StopAllAnimations()
    {
        StopRunningAnimations();

        Opacity = 1.0;
        MascotScale.ScaleX = 1.0;
        MascotScale.ScaleY = 1.0;
        MascotRotate.Angle = 0.0;
        MascotTranslate.X = 0.0;
        MascotTranslate.Y = 0.0;

        if (OrbCoreScale != null)
        {
            OrbCoreScale.ScaleX = 1.0;
            OrbCoreScale.ScaleY = 1.0;
        }
        if (OrbInnerScale != null)
        {
            OrbInnerScale.ScaleX = 1.0;
            OrbInnerScale.ScaleY = 1.0;
        }
        if (OrbMiddleScale != null)
        {
            OrbMiddleScale.ScaleX = 1.0;
            OrbMiddleScale.ScaleY = 1.0;
        }
        if (OrbOuterScale != null)
        {
            OrbOuterScale.ScaleX = 1.0;
            OrbOuterScale.ScaleY = 1.0;
        }
        if (OrbOuterRing != null)
        {
            OrbOuterRing.Opacity = 0.25;
        }
        if (OrbMiddleRing != null)
        {
            OrbMiddleRing.Opacity = 0.45;
        }
        if (OrbInnerRing != null)
        {
            OrbInnerRing.Opacity = 0.75;
        }
        if (VisorLeftScale != null)
        {
            VisorLeftScale.ScaleX = 1.0;
            VisorLeftScale.ScaleY = 1.0;
        }
        if (VisorRightScale != null)
        {
            VisorRightScale.ScaleX = 1.0;
            VisorRightScale.ScaleY = 1.0;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        MascotClicked?.Invoke(this, EventArgs.Empty);
    }
}
