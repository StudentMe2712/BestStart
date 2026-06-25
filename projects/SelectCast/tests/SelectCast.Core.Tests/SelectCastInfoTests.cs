using SelectCast.Core;
using Xunit;

namespace SelectCast.Core.Tests;

public class SelectCastInfoTests
{
    [Fact]
    public void ProductName_is_SelectCast()
    {
        Assert.Equal("SelectCast", SelectCastInfo.ProductName);
    }
}
