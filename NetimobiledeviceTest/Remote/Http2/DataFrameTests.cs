using Netimobiledevice.Remote.Http2;

namespace NetimobiledeviceTest.Remote.Http2;

[TestClass]
public class DataFrameTests {
    [TestMethod]
    public void Constructor_Default_HasExpectedDefaults() {
        DataFrame frame = new DataFrame();

        Assert.AreEqual(FrameType.Data, frame.Type);
        Assert.AreEqual(0u, frame.StreamIdentifier);
        Assert.AreEqual(0, frame.PadLength);
        Assert.IsFalse(frame.Flags.HasFlag(FrameFlags.Padded));
        Assert.IsNotNull(frame.Data);
        Assert.IsEmpty(frame.Data);
    }

    [TestMethod]
    public void Constructor_WithStreamIdentifier_SetsStreamIdentifier() {
        const uint streamIdentifier = 123;

        DataFrame frame = new DataFrame(streamIdentifier);

        Assert.AreEqual(streamIdentifier, frame.StreamIdentifier);
    }

    [TestMethod]
    public void Type_IsData() {
        DataFrame frame = new DataFrame();

        Assert.AreEqual(FrameType.Data, frame.Type);
    }

    [TestMethod]
    public void Data_CanBeAssigned() {
        byte[] data = [1, 2, 3, 4];

        DataFrame frame = new DataFrame {
            Data = data
        };

        Assert.AreSequenceEqual(data, frame.Data);
    }

    [TestMethod]
    public void PadLength_Zero_DoesNotSetPaddedFlag() {
        DataFrame frame = new DataFrame {
            PadLength = 0
        };

        Assert.AreEqual(0, frame.PadLength);
        Assert.IsFalse(frame.Flags.HasFlag(FrameFlags.Padded));
    }

    [TestMethod]
    public void PadLength_GreaterThanZero_SetsPaddedFlag() {
        DataFrame frame = new DataFrame {
            PadLength = 4
        };

        Assert.AreEqual(4, frame.PadLength);
        Assert.IsTrue(frame.Flags.HasFlag(FrameFlags.Padded));
    }

    [TestMethod]
    public void PadLength_CanBeChanged() {
        DataFrame frame = new DataFrame {
            PadLength = 4
        };
        frame.PadLength = 8;

        Assert.AreEqual(8, frame.PadLength);
        Assert.IsTrue(frame.Flags.HasFlag(FrameFlags.Padded));
    }

    [TestMethod]
    public void ToString_UnpaddedFrame_ContainsExpectedValues() {
        DataFrame frame = new DataFrame(42) {
            Data = [1, 2, 3]
        };

        string result = frame.ToString();

        Assert.Contains("[Frame: DATA", result);
        Assert.Contains("Id=42", result);
        Assert.Contains("EndStream=False", result);
        Assert.Contains("Padded=False", result);
        Assert.Contains("PadLength=0", result);
        Assert.Contains("DataLength=3", result);
    }

    [TestMethod]
    public void ToString_PaddedFrame_ContainsExpectedValues() {
        DataFrame frame = new DataFrame(42) {
            Data = [1, 2, 3],
            PadLength = 4
        };

        string result = frame.ToString();

        Assert.Contains("[Frame: DATA", result);
        Assert.Contains("Id=42", result);
        Assert.Contains("EndStream=False", result);
        Assert.Contains("Padded=True", result);
        Assert.Contains("PadLength=4", result);
        Assert.Contains("DataLength=3", result);
    }

    [TestMethod]
    public void EndStreamFlag_IsReflectedByToString() {
        DataFrame frame = new DataFrame {
            Flags = FrameFlags.EndStream
        };

        string result = frame.ToString();

        Assert.Contains("EndStream=True", result);
        Assert.Contains("Padded=False", result);
    }

    [TestMethod]
    public void EndStreamAndPaddedFlags_CanBeSetTogether() {
        DataFrame frame = new DataFrame {
            Flags = FrameFlags.EndStream,
            PadLength = 2
        };

        Assert.IsTrue(frame.Flags.HasFlag(FrameFlags.EndStream));
        Assert.IsTrue(frame.Flags.HasFlag(FrameFlags.Padded));
    }
}
