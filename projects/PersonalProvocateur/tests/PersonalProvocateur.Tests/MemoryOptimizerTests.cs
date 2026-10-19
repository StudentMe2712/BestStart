using PersonalProvocateur.App.WindowsInterop;
using Xunit;

namespace PersonalProvocateur.Tests;

public class MemoryOptimizerTests
{
    [Fact]
    public void TrimProcessMemory_ExecutesWithoutThrowing()
    {
        // Must execute cleanly in any environment without raising exceptions
        var exception = Record.Exception(() => MemoryOptimizer.TrimProcessMemory());
        Assert.Null(exception);
    }

    [Fact]
    public void TrimProcessMemory_CanBeCalledRepeatedly()
    {
        for (int i = 0; i < 3; i++)
        {
            var exception = Record.Exception(() => MemoryOptimizer.TrimProcessMemory());
            Assert.Null(exception);
        }
    }
}
