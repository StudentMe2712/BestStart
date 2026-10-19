using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using PersonalProvocateur.App.Models;
using PersonalProvocateur.App.Services;
using PersonalProvocateur.App.Storage;
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
    public void SetSpeechText_UpdatesLastSpeechText()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);
            control.SetSpeechText("Привет, мир!");
            Assert.Equal("Привет, мир!", control.LastSpeechText);
        });
    }

    [Fact]
    public void SetRandomSpeech_SetsValidPhraseFromExpectedPool()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);

            control.SetRandomSpeech(MascotState.Idle);
            Assert.False(string.IsNullOrWhiteSpace(control.LastSpeechText));

            control.SetRandomSpeech(MascotState.Attention);
            Assert.False(string.IsNullOrWhiteSpace(control.LastSpeechText));

            control.SetRandomSpeech(MascotState.Clicked);
            Assert.False(string.IsNullOrWhiteSpace(control.LastSpeechText));
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

            var sbField = typeof(MascotControl).GetField("_activeStoryboard", BindingFlags.Instance | BindingFlags.NonPublic);
            var sb = sbField?.GetValue(control) as Storyboard;
            Assert.NotNull(sb);

            var slideAnim = sb.Children.OfType<DoubleAnimation>()
                .FirstOrDefault(a => a.From == -130.0 && a.To == 14.0);
            Assert.NotNull(slideAnim);
            Assert.Equal(TimeSpan.FromMilliseconds(380), slideAnim.Duration.TimeSpan);

            var opacityAnim = sb.Children.OfType<DoubleAnimation>()
                .FirstOrDefault(a => a.From == 0.0 && a.To == 1.0);
            Assert.NotNull(opacityAnim);
            Assert.Equal(TimeSpan.FromMilliseconds(300), opacityAnim.Duration.TimeSpan);

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

            // Scale impulse to 1.25 over 100ms (ADR-012)
            var scaleAnim = sb.Children.OfType<DoubleAnimation>()
                .FirstOrDefault(a => a.From == 1.0 && a.To == 1.25);
            Assert.NotNull(scaleAnim);
            Assert.Equal(TimeSpan.FromMilliseconds(100), scaleAnim.Duration.TimeSpan);

            // Smooth exit upwards: MascotTranslate.Y from 14 to -130 over 220ms (ADR-018)
            var exitAnim = sb.Children.OfType<DoubleAnimation>()
                .FirstOrDefault(a => a.From == 14.0 && a.To == -130.0);
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

    [Fact]
    public void ApplySkin_DynamicOrb_SwitchesVisibility_AndSetsDefaultSkin()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);
            control.ApplySkin("DynamicOrb");

            Assert.Equal("DynamicOrb", control.CurrentSkin);
            Assert.Equal(Visibility.Visible, control.DynamicOrbContainer.Visibility);
            Assert.Equal(Visibility.Collapsed, control.CyberVisorContainer.Visibility);
            Assert.Equal(Visibility.Collapsed, control.CustomImageContainer.Visibility);
            control.StopAllAnimations();
        });
    }

    [Fact]
    public void ApplySkin_CyberVisor_SwitchesVisibility_AndSetsSkin()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);
            control.ApplySkin("CyberVisor");

            Assert.Equal("CyberVisor", control.CurrentSkin);
            Assert.Equal(Visibility.Collapsed, control.DynamicOrbContainer.Visibility);
            Assert.Equal(Visibility.Visible, control.CyberVisorContainer.Visibility);
            Assert.Equal(Visibility.Collapsed, control.CustomImageContainer.Visibility);
            control.StopAllAnimations();
        });
    }

    [Fact]
    public void ApplySkin_CustomImage_WithCustomPath_LoadsImageFile()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);
            var customFilePath = Path.Combine(_tempDir, "custom_avatar.png");
            
            // Create a valid 1x1 png image
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(
                new System.Windows.Media.Imaging.RenderTargetBitmap(16, 16, 96, 96, System.Windows.Media.PixelFormats.Pbgra32)));
            using (var fs = File.Create(customFilePath))
            {
                encoder.Save(fs);
            }

            control.ApplySkin("CustomImage", customFilePath);

            Assert.Equal("CustomImage", control.CurrentSkin);
            Assert.Equal(customFilePath, control.CurrentCustomPath);
            Assert.Equal(Visibility.Collapsed, control.DynamicOrbContainer.Visibility);
            Assert.Equal(Visibility.Collapsed, control.CyberVisorContainer.Visibility);
            Assert.Equal(Visibility.Visible, control.CustomImageContainer.Visibility);
            Assert.NotNull(control.MascotImage.Source);
            control.StopAllAnimations();
        });
    }

    [Fact]
    public void ApplySkin_CustomImage_WithoutCustomPath_FallsBackToAssetService()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);
            control.SetState(MascotState.Idle);
            control.ApplySkin("CustomImage", null);

            Assert.Equal("CustomImage", control.CurrentSkin);
            Assert.Equal(Visibility.Visible, control.CustomImageContainer.Visibility);
            Assert.NotNull(control.MascotImage.Source);
            control.StopAllAnimations();
        });
    }

    [Fact]
    public void ApplySkin_InvalidSkin_DefaultsToDynamicOrb()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);
            control.ApplySkin("InvalidSkin_ABC");

            Assert.Equal("DynamicOrb", control.CurrentSkin);
            Assert.Equal(Visibility.Visible, control.DynamicOrbContainer.Visibility);
            Assert.Equal(Visibility.Collapsed, control.CyberVisorContainer.Visibility);
            Assert.Equal(Visibility.Collapsed, control.CustomImageContainer.Visibility);
            control.StopAllAnimations();
        });
    }

    [Fact]
    public void OnLoaded_PullsSkinPreference_FromSettingsStore()
    {
        RunInSta(() =>
        {
            var settingsPath = Path.Combine(_tempDir, "settings_test.json");
            var store = new SettingsStore(settingsPath);
            store.Update(s => s.MascotSkin = "CyberVisor");

            var control = new MascotControl(_assetService, store);
            control.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            Assert.Equal("CyberVisor", control.CurrentSkin);
            Assert.Equal(Visibility.Visible, control.CyberVisorContainer.Visibility);
            Assert.Equal(Visibility.Collapsed, control.DynamicOrbContainer.Visibility);
            control.StopAllAnimations();
        });
    }

    [Fact]
    public void StopAllAnimations_StopsSkinAnimations_AndResetsTransforms()
    {
        RunInSta(() =>
        {
            var control = new MascotControl(_assetService);
            control.ApplySkin("DynamicOrb");
            control.PlayIdleAnimation();

            control.StopAllAnimations();

            Assert.Equal(1.0, control.MascotScale.ScaleX);
            Assert.Equal(1.0, control.MascotScale.ScaleY);
            Assert.Equal(0.0, control.MascotRotate.Angle);
            Assert.Equal(0.0, control.MascotTranslate.X);
            Assert.Equal(0.0, control.MascotTranslate.Y);
            Assert.Equal(1.0, control.OrbCoreScale.ScaleX);
            Assert.Equal(1.0, control.OrbCoreScale.ScaleY);
            Assert.Equal(0.25, control.OrbOuterRing.Opacity);
        });
    }
}
