using Netimobiledevice.EndianBitConversion;

namespace NetimobiledeviceTest.EndianBitConversion;

[TestClass]
public class InterfaceTests
{
    [TestMethod]
    public void IsLittleEndian()
    {
        Assert.IsFalse(EndianBitConverter.BigEndian.IsLittleEndian);
        Assert.IsTrue(EndianBitConverter.LittleEndian.IsLittleEndian);
    }
}
