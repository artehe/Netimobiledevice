using Netimobiledevice.Services.Afc;

namespace NetimobiledeviceTest.Services.Afc;

[TestClass]
public sealed class PosixPathTests {
    [TestMethod]
    [DataRow("a/b/c", "c", DisplayName = "Relative multi-segment")]
    [DataRow("/a/b/c", "c", DisplayName = "Absolute multi-segment")]
    [DataRow("/a", "a", DisplayName = "Absolute single segment")]
    [DataRow("c", "c", DisplayName = "No separator")]
    [DataRow("file.txt", "file.txt", DisplayName = "No separator with extension")]
    [DataRow("dir/file.tar.gz", "file.tar.gz", DisplayName = "Multiple dots")]
    [DataRow("a/b/", "", DisplayName = "Trailing separator")]
    [DataRow("/", "", DisplayName = "Root")]
    [DataRow("//", "", DisplayName = "Double root")]
    [DataRow("", "", DisplayName = "Empty")]
    [DataRow("a//b", "b", DisplayName = "Repeated separator")]
    [DataRow("a/.hidden", ".hidden", DisplayName = "Dotfile")]
    [DataRow("a/b c", "b c", DisplayName = "Whitespace in name")]
    public void BaseName_ReturnsLastSegment(string path, string expected) {
        Assert.AreEqual(expected, PosixPath.BaseName(path));
    }

    [TestMethod]
    [DataRow("a/b/c", "a/b", DisplayName = "Relative multi-segment")]
    [DataRow("/a/b/c", "/a/b", DisplayName = "Absolute multi-segment")]
    [DataRow("/a/b", "/a", DisplayName = "Absolute two segments")]
    [DataRow("a/b", "a", DisplayName = "Relative two segments")]
    [DataRow("a/", "a", DisplayName = "Trailing separator, single segment")]
    [DataRow("a/b/", "a/b", DisplayName = "Trailing separator, multi-segment")]
    [DataRow("c", "", DisplayName = "No separator")]
    [DataRow("", "", DisplayName = "Empty")]
    public void DirName_ReturnsParent(string path, string expected) {
        Assert.AreEqual(expected, PosixPath.DirName(path));
    }

    [TestMethod]
    [DataRow("/a", "/", DisplayName = "Child of root")]
    [DataRow("/", "/", DisplayName = "Root itself")]
    [DataRow("//a", "//", DisplayName = "Child of double root")]
    [DataRow("///", "///", DisplayName = "Only separators")]
    public void DirName_PreservesSeparatorOnlyHead(string path, string expected) {
        Assert.AreEqual(expected, PosixPath.DirName(path));
    }

    [TestMethod]
    [DataRow("a//b", "a", DisplayName = "Double separator")]
    [DataRow("/a///b", "/a", DisplayName = "Triple separator")]
    public void DirName_CollapsesTrailingSeparators(string path, string expected) {
        Assert.AreEqual(expected, PosixPath.DirName(path));
    }


    [TestMethod]
    [DataRow("a")]
    [DataRow("")]
    [DataRow("/")]
    public void Join_NoAdditionalParts_ReturnsInput(string path) {
        Assert.AreEqual(path, PosixPath.Join(path));
    }

    [TestMethod]
    [DataRow("a", new[] { "b" }, "a/b", DisplayName = "Inserts separator")]
    [DataRow("a/", new[] { "b" }, "a/b", DisplayName = "No double separator")]
    [DataRow("", new[] { "b" }, "b", DisplayName = "Empty base")]
    [DataRow("/", new[] { "a" }, "/a", DisplayName = "Root base")]
    [DataRow("a", new[] { "b", "c" }, "a/b/c", DisplayName = "Several parts")]
    [DataRow("a", new[] { "b/", "c" }, "a/b/c", DisplayName = "Part with trailing separator")]
    [DataRow("a", new[] { "b/c", "d" }, "a/b/c/d", DisplayName = "Part containing separator")]
    [DataRow("/a", new[] { "b" }, "/a/b", DisplayName = "Absolute base")]
    public void Join_CombinesParts(string path, string[] paths, string expected) {
        Assert.AreEqual(expected, PosixPath.Join(path, paths));
    }

    [TestMethod]
    [DataRow("a", new[] { "/b" }, "/b", DisplayName = "Absolute part replaces base")]
    [DataRow("a", new[] { "b", "/c", "d" }, "/c/d", DisplayName = "Absolute part in middle")]
    [DataRow("a", new[] { "/b", "/c" }, "/c", DisplayName = "Last absolute part wins")]
    [DataRow("a", new[] { "/" }, "/", DisplayName = "Root part")]
    [DataRow("", new[] { "/b" }, "/b", DisplayName = "Empty base, absolute part")]
    public void Join_AbsolutePartDiscardsPrecedingParts(string path, string[] paths, string expected) {
        Assert.AreEqual(expected, PosixPath.Join(path, paths));
    }

    [TestMethod]
    [DataRow("a", new[] { "" }, "a/", DisplayName = "Empty part adds trailing separator")]
    [DataRow("a/", new[] { "" }, "a/", DisplayName = "Empty part after trailing separator")]
    [DataRow("a", new[] { "", "b" }, "a/b", DisplayName = "Empty part in middle")]
    [DataRow("", new[] { "" }, "", DisplayName = "All empty")]
    [DataRow("", new[] { "", "a" }, "a", DisplayName = "Leading empties")]
    public void Join_EmptyParts(string path, string[] paths, string expected) {
        Assert.AreEqual(expected, PosixPath.Join(path, paths));
    }

    [TestMethod]
    public void Join_EmptyParamsArray_ReturnsInput() {
        Assert.AreEqual("a", PosixPath.Join("a", []));
    }

    [TestMethod]
    [DataRow("a/b/c")]
    [DataRow("/a/b/c")]
    [DataRow("/a")]
    [DataRow("c")]
    [DataRow("a/b/")]
    public void Join_DirNameAndBaseName_RoundTrips(string path) {
        Assert.AreEqual(path, PosixPath.Join(PosixPath.DirName(path), PosixPath.BaseName(path)));
    }
}
