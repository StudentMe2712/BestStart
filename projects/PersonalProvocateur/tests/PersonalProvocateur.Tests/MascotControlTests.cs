using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media.Animation;
using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Services;
using PersonalProvocateur.App.UI.Controls;
using Xunit;

namespace PersonalProvocateur.Tests;

public class MascotControlTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MascotAssetService _assetService;

    public MascotControlTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pp_control_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _assetService = new MascotAssetService(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Suppress cleanup exceptions in tests
        }
    }

    private static void RunInSta(Action action)
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (caught != null)
        {
            ExceptionDispatchInfo.Capture(caught).Throw();
        }
    }

    [Fact]
    public void Instantiation_SetsDefaultProperties_AndInitializesControls()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);

            Assert.NotNull(control.MascotScale);
            Assert.NotNull(control.MascotRotate);
            Assert.NotNull(control.MascotTranslate);
            Assert.NotNull(control.MascotImage);
            Assert.NotNull(control.SpeechBubbleText);
            Assert.NotNull(control.SpeechBubbleBorder);

            Assert.False(control.Focusable);
            Assert.Equal(Cursors.Hand, control.Cursor);
            Assert.Equal(MascotState.Hidden, control.CurrentState);
        });
    }

    [Fact]
    public void SetState_UpdatesCurrentState_AndMascotImageSource()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);

            control.SetState(MascotState.Idle);
            Assert.Equal(MascotState.Idle, control.CurrentState);
            Assert.NotNull(control.MascotImage.Source);

            control.SetState(MascotState.Attention);
            Assert.Equal(MascotState.Attention, control.CurrentState);
            Assert.NotNull(control.MascotImage.Source);

            control.SetState(MascotState.Peek);
            Assert.Equal(MascotState.Peek, control.CurrentState);
            Assert.NotNull(control.MascotImage.Source);

            control.SetState(MascotState.Clicked);
            Assert.Equal(MascotState.Clicked, control.CurrentState);
            Assert.NotNull(control.MascotImage.Source);
        });
    }

    [Fact]
    public void SetSpeechText_UpdatesBubbleTextBlock()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);
            control.SetSpeechText("Привет, мир!");
            Assert.Equal("Привет, мир!", control.SpeechBubbleText.Text);
        });
    }

    [Fact]
    public void SetRandomSpeech_SetsValidPhraseFromExpectedPool()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);

            control.SetRandomSpeech(MascotState.Idle);
            Assert.False(string.IsNullOrWhiteSpace(control.SpeechBubbleText.Text));

            control.SetRandomSpeech(MascotState.Attention);
            Assert.False(string.IsNullOrWhiteSpace(control.SpeechBubbleText.Text));

            control.SetRandomSpeech(MascotState.Clicked);
            Assert.False(string.IsNullOrWhiteSpace(control.SpeechBubbleText.Text));
        });
    }

    [Fact]
    public void PlayPeekAnimation_SetsPeekState_AndDoesNotThrow()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);
            control.PlayPeekAnimation();
            Assert.Equal(MascotState.Peek, control.CurrentState);
            control.StopAllAnimations();
        });
    }

    [Fact]
    public void PlayPeekAnimation_SetsPeekState_AndConfiguresTopSlideParameters()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);
            control.PlayPeekAnimation();

            Assert.Equal(MascotState.Peek, control.CurrentState);
            Assert.Equal("Ку-ку! 👀", control.SpeechBubbleText.Text);

            var sbField = typeof(MascotControl).GetField("_activeStoryboard", BindingFlags.Instance | BindingFlags.NonPublic);
            var sb = sbField?.GetValue(control) as Storyboard;
            Assert.NotNull(sb);

            var slideAnim = sb.Children.OfType<DoubleAnimation>()
                .FirstOrDefault(a => a.From == -260.0 && a.To == 0.0);
            Assert.NotNull(slideAnim);
            Assert.Equal(TimeSpan.FromMilliseconds(520), slideAnim.Duration.TimeSpan);

            var opacityAnim = sb.Children.OfType<DoubleAnimation>()
                .FirstOrDefault(a => a.From == 0.0 && a.To == 1.0);
            Assert.NotNull(opacityAnim);

            control.StopAllAnimations();
        });
    }

    [Fact]
    public void PlayIdleAnimation_SetsIdleState_AndDoesNotThrow()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);
            control.PlayIdleAnimation();
            Assert.Equal(MascotState.Idle, control.CurrentState);
            control.StopAllAnimations();
        });
    }

    [Fact]
    public void PlayAttentionAnimation_SetsAttentionState_AndDoesNotThrow()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);
            control.PlayAttentionAnimation();
            Assert.Equal(MascotState.Attention, control.CurrentState);
            control.StopAllAnimations();
        });
    }

    [Fact]
    public void PlayClickAnimation_SetsClickedState_AndInitiatesAnimation()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);
            bool callbackInvoked = false;

            control.PlayClickAnimation(() => callbackInvoked = true);
            Assert.Equal(MascotState.Clicked, control.CurrentState);
            Assert.False(callbackInvoked);

            control.StopAllAnimations();
        });
    }

    [Fact]
    public void PlayClickAnimation_SetsClickedState_AndAnimatesUpwardExit()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);
            bool callbackInvoked = false;

            control.PlayClickAnimation(() => callbackInvoked = true);
            Assert.Equal(MascotState.Clicked, control.CurrentState);
            Assert.False(callbackInvoked);

            var sbField = typeof(MascotControl).GetField("_activeStoryboard", BindingFlags.Instance | BindingFlags.NonPublic);
            var sb = sbField?.GetValue(control) as Storyboard;
            Assert.NotNull(sb);

            // Scale impulse to 1.12 over 100ms
            var scaleAnim = sb.Children.OfType<DoubleAnimation>()
                .FirstOrDefault(a => a.From == 1.0 && a.To == 1.12);
            Assert.NotNull(scaleAnim);
            Assert.Equal(TimeSpan.FromMilliseconds(100), scaleAnim.Duration.TimeSpan);

            // Smooth exit upwards: MascotTranslate.Y from 0 to -260 over 220ms
            var exitAnim = sb.Children.OfType<DoubleAnimation>()
                .FirstOrDefault(a => a.From == 0.0 && a.To == -260.0);
            Assert.NotNull(exitAnim);
            Assert.Equal(TimeSpan.FromMilliseconds(220), exitAnim.Duration.TimeSpan);

            // Opacity fade to 0.0
            var opacityAnim = sb.Children.OfType<DoubleAnimation>()
                .FirstOrDefault(a => a.From == 1.0 && a.To == 0.0);
            Assert.NotNull(opacityAnim);

            control.StopAllAnimations();
        });
    }

    [Fact]
    public void StopAllAnimations_ResetsTransformValuesToBaseline()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);
            control.PlayIdleAnimation();

            control.StopAllAnimations();

            Assert.Equal(1.0, control.MascotScale.ScaleX);
            Assert.Equal(1.0, control.MascotScale.ScaleY);
            Assert.Equal(0.0, control.MascotRotate.Angle);
            Assert.Equal(0.0, control.MascotTranslate.X);
            Assert.Equal(0.0, control.MascotTranslate.Y);
        });
    }

    [Fact]
    public void MascotClicked_EventFired_WhenSubscribed()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);
            bool clicked = false;
            control.MascotClicked += (s, e) => clicked = true;

            // Simulate left button up
            var mouseDevice = Mouse.PrimaryDevice;
            var args = new MouseButtonEventArgs(mouseDevice, 0, MouseButton.Left)
            {
                RoutedEvent = Mouse.MouseUpEvent
            };
            control.RaiseEvent(args);

            Assert.True(clicked);
        });
    }
}
