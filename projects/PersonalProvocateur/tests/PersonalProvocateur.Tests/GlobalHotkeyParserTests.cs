using PersonalProvocateur.App.WindowsInterop;
using Xunit;

namespace PersonalProvocateur.Tests;

public class GlobalHotkeyParserTests
{
    [Fact]
    public void TryParseHotkey_StandardTriggerHotkey_ReturnsExpectedModifiersAndKey()
    {
        // "Ctrl+Alt+P"
        bool success = GlobalHotkeyService.TryParseHotkey("Ctrl+Alt+P", out uint modifiers, out uint vk);

        Assert.True(success);
        Assert.Equal(NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, modifiers);
        Assert.Equal((uint)'P', vk);
    }

    [Fact]
    public void TryParseHotkey_StandardPanicHotkey_ReturnsExpectedModifiersAndKey()
    {
        // "Ctrl+Alt+Shift+P"
        bool success = GlobalHotkeyService.TryParseHotkey("Ctrl+Alt+Shift+P", out uint modifiers, out uint vk);

        Assert.True(success);
        Assert.Equal(NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_SHIFT, modifiers);
        Assert.Equal((uint)'P', vk);
    }

    [Fact]
    public void TryParseHotkey_WinModifierWithFunctionKey_ParsesCorrectly()
    {
        // "Win+Shift+F5"
        bool success = GlobalHotkeyService.TryParseHotkey("Win+Shift+F5", out uint modifiers, out uint vk);

        Assert.True(success);
        Assert.Equal(NativeMethods.MOD_WIN | NativeMethods.MOD_SHIFT, modifiers);
        Assert.Equal((uint)0x74, vk); // VK_F5
    }

    [Fact]
    public void TryParseHotkey_ControlAliasAndDigitKey_ParsesCorrectly()
    {
        // "Control+Alt+1"
        bool success = GlobalHotkeyService.TryParseHotkey("Control+Alt+1", out uint modifiers, out uint vk);

        Assert.True(success);
        Assert.Equal(NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, modifiers);
        Assert.Equal((uint)'1', vk);
    }

    [Theory]
    [InlineData("Esc", 0x1B)]
    [InlineData("Escape", 0x1B)]
    [InlineData("Space", 0x20)]
    [InlineData("Tab", 0x09)]
    [InlineData("Enter", 0x0D)]
    [InlineData("Backspace", 0x08)]
    [InlineData("Delete", 0x2E)]
    [InlineData("Home", 0x24)]
    [InlineData("End", 0x23)]
    [InlineData("PageUp", 0x21)]
    [InlineData("PageDown", 0x22)]
    public void TryParseHotkey_SpecialKeysWithoutModifiers_ParsesCorrectly(string keyName, uint expectedVk)
    {
        bool success = GlobalHotkeyService.TryParseHotkey(keyName, out uint modifiers, out uint vk);

        Assert.True(success);
        Assert.Equal(0u, modifiers);
        Assert.Equal(expectedVk, vk);
    }

    [Fact]
    public void TryParseHotkey_WithWhitespacePadding_ParsesCorrectly()
    {
        bool success = GlobalHotkeyService.TryParseHotkey("  ctrl  +  alt  +  p  ", out uint modifiers, out uint vk);

        Assert.True(success);
        Assert.Equal(NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, modifiers);
        Assert.Equal((uint)'P', vk);
    }

    [Fact]
    public void TryParseHotkey_FunctionKeysF1ThroughF12_ParsesAllCorrectly()
    {
        for (int i = 1; i <= 12; i++)
        {
            string keyStr = $"Ctrl+F{i}";
            bool success = GlobalHotkeyService.TryParseHotkey(keyStr, out uint modifiers, out uint vk);

            Assert.True(success);
            Assert.Equal(NativeMethods.MOD_CONTROL, modifiers);
            Assert.Equal((uint)(0x70 + (i - 1)), vk);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseHotkey_NullOrWhitespace_ReturnsFalse(string? input)
    {
        bool success = GlobalHotkeyService.TryParseHotkey(input, out uint modifiers, out uint vk);

        Assert.False(success);
        Assert.Equal(0u, modifiers);
        Assert.Equal(0u, vk);
    }

    [Theory]
    [InlineData("Ctrl")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+Alt+Shift")]
    [InlineData("Win+Alt")]
    public void TryParseHotkey_OnlyModifiersWithoutKey_ReturnsFalse(string input)
    {
        bool success = GlobalHotkeyService.TryParseHotkey(input, out uint modifiers, out uint vk);

        Assert.False(success);
        Assert.Equal(0u, modifiers);
        Assert.Equal(0u, vk);
    }

    [Theory]
    [InlineData("Ctrl+A+B")]
    [InlineData("Alt+P+Q")]
    [InlineData("Ctrl+Alt+X+Y")]
    public void TryParseHotkey_MultipleKeys_ReturnsFalse(string input)
    {
        bool success = GlobalHotkeyService.TryParseHotkey(input, out uint modifiers, out uint vk);

        Assert.False(success);
    }

    [Theory]
    [InlineData("Ctrl+UnknownKey123")]
    [InlineData("Ctrl+FooBar")]
    [InlineData("Ctrl++P")]
    [InlineData("+")]
    public void TryParseHotkey_InvalidKeyOrFormat_ReturnsFalse(string input)
    {
        bool success = GlobalHotkeyService.TryParseHotkey(input, out uint modifiers, out uint vk);

        Assert.False(success);
    }

    [Fact]
    public void GlobalHotkeyService_SimulateHotkey_InvokesRegisteredCallbacks()
    {
        using var service = new GlobalHotkeyService();
        bool triggerInvoked = false;
        bool panicInvoked = false;

        // Directly test registered handler dispatch via SimulateHotkey
        // without registering with Windows OS window manager
        typeof(GlobalHotkeyService)
            .GetField("_triggerCallback", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .SetValue(service, (Action)(() => triggerInvoked = true));

        typeof(GlobalHotkeyService)
            .GetField("_panicCallback", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .SetValue(service, (Action)(() => panicInvoked = true));

        service.SimulateHotkey(GlobalHotkeyService.TriggerHotkeyId);
        Assert.True(triggerInvoked);
        Assert.False(panicInvoked);

        service.SimulateHotkey(GlobalHotkeyService.PanicHotkeyId);
        Assert.True(panicInvoked);
    }
}
