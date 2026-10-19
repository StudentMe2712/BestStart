using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Services;
using UserControl = System.Windows.Controls.UserControl;

namespace PersonalProvocateur.App.UI.Controls;

/// <summary>
/// Interactive 2D mascot control hosting the character sprite,
/// speech bubble provocative dialogue, and Storyboard-driven animations.
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

    /// <summary>
    /// Gets or sets the asset service providing mascot sprites.
    /// </summary>
    public MascotAssetService AssetService { get; set; }

    /// <summary>
    /// Gets the current mascot state.
    /// </summary>
    public MascotState CurrentState { get; private set; } = MascotState.Hidden;

    /// <summary>
    /// Occurs when the user clicks on the mascot control.
    /// </summary>
    public event EventHandler? MascotClicked;

    /// <summary>
    /// Initializes a new instance of <see cref="MascotControl"/>.
    /// </summary>
    /// <param name="assetService">Optional asset service instance.</param>
    public MascotControl(MascotAssetService? assetService = null)
    {
        InitializeComponent();

        AssetService = assetService ?? new MascotAssetService();

        _attentionTimer = new DispatcherTimer();
        _attentionTimer.Tick += OnAttentionTimerTick;

        Unloaded += (s, e) => StopAllAnimations();
    }

    /// <summary>
    /// Parameterless constructor for XAML and designer support.
    /// </summary>
    public MascotControl() : this(null)
    {
    }

    /// <summary>
    /// Switches the displayed mascot sprite to match the specified state.
    /// </summary>
    /// <param name="state">The target mascot state.</param>
    public void SetState(MascotState state)
    {
        CurrentState = state;
        MascotImage.Source = AssetService.GetSprite(state);
    }

    /// <summary>
    /// Updates the text shown inside the speech bubble.
    /// </summary>
    /// <param name="text">The message to display.</param>
    public void SetSpeechText(string text)
    {
        SpeechBubbleText.Text = text;
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
    /// Plays the peek entrance animation (smooth slide-in from top screen edge with spring back-ease),
    /// automatically transitioning to <see cref="PlayIdleAnimation"/> upon completion.
    /// </summary>
    public void PlayPeekAnimation()
    {
        StopRunningAnimations();

        SetState(MascotState.Peek);
        SetSpeechText("Ку-ку! 👀");

        MascotTranslate.Y = -260.0;
        MascotTranslate.X = 0.0;
        Opacity = 0.0;

        var sb = new Storyboard();

        var slideAnim = new DoubleAnimation
        {
            From = -260.0,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(520),
            EasingFunction = new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(slideAnim, MascotTranslate);
        Storyboard.SetTargetProperty(slideAnim, new PropertyPath(TranslateTransform.YProperty));
        sb.Children.Add(slideAnim);

        var opacityAnim = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(520),
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
    /// Plays the ambient idle animation (delicate vertical floating at top edge 0..6px),
    /// starting the 6–8s attention timer.
    /// </summary>
    public void PlayIdleAnimation()
    {
        StopRunningAnimations();

        SetState(MascotState.Idle);
        SetRandomSpeech(MascotState.Idle);

        Opacity = 1.0;
        MascotTranslate.Y = 0.0;
        MascotTranslate.X = 0.0;
        MascotScale.ScaleX = 1.0;
        MascotScale.ScaleY = 1.0;
        MascotRotate.Angle = 0.0;

        var sb = new Storyboard();

        // Subtle vertical floating at top edge (0 to 6px)
        var floatAnim = new DoubleAnimation
        {
            From = 0.0,
            To = 6.0,
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

        ResetAttentionTimer();
    }

    /// <summary>
    /// Plays the attention animation (periodic bouncing jumps, energetic rocking, and insistent speech),
    /// returning to idle upon completion.
    /// </summary>
    public void PlayAttentionAnimation()
    {
        StopRunningAnimations();

        SetState(MascotState.Attention);
        SetRandomSpeech(MascotState.Attention);

        var sb = new Storyboard();

        // Bouncing jump downwards from notch (TranslateY 0 to 16)
        var bounceAnim = new DoubleAnimation
        {
            From = 0.0,
            To = 16.0,
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
    }

    /// <summary>
    /// Plays the click response animation (short scale impulse to 1.12 in 100ms, then smooth upward slide
    /// to -260 over 220ms with Opacity fading to 0), invoking the callback upon completion.
    /// </summary>
    /// <param name="onCompleted">Action invoked after the click animation concludes.</param>
    public void PlayClickAnimation(Action onCompleted)
    {
        StopAllAnimations();

        SetState(MascotState.Clicked);
        SetRandomSpeech(MascotState.Clicked);

        var sb = new Storyboard();

        // Short scale impulse (to 1.12 in 100ms)
        var scaleXAnim = new DoubleAnimation
        {
            From = 1.0,
            To = 1.12,
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
            To = 1.12,
            Duration = TimeSpan.FromMilliseconds(100),
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(scaleYAnim, MascotScale);
        Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath(ScaleTransform.ScaleYProperty));
        sb.Children.Add(scaleYAnim);

        // Smooth exit upwards: MascotTranslate.Y from 0 to -260 over 220ms
        var exitAnim = new DoubleAnimation
        {
            From = 0.0,
            To = -260.0,
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
        // 6 to 8 seconds delay
        var seconds = Random.Shared.Next(6, 9);
        _attentionTimer.Interval = TimeSpan.FromSeconds(seconds);
        _attentionTimer.Start();
    }

    private void StopRunningAnimations()
    {
        _attentionTimer.Stop();

        if (_activeStoryboard != null)
        {
            _activeStoryboard.Stop();
            _activeStoryboard = null;
        }

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
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        MascotClicked?.Invoke(this, EventArgs.Empty);
    }
}
