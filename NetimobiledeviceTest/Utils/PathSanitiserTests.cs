using Netimobiledevice.Utils;

namespace NetimobiledeviceTest.Utils;

[TestClass]
public class PathSanitiserTests {
    [TestMethod]
    public void HandleIncorrectWindowsPath() {
        string sourcePath = @"C:\\Users\\User\\My:Folder*<File>?\\";
        string resultPath = PathSanitiser.SantiseWindowsPath(sourcePath);
        if (OperatingSystem.IsWindows()) {
            Assert.AreEqual(@"C:\\Users\\User\\My_Folder__File__\\", resultPath);
        }
        else {
            Assert.AreEqual(sourcePath, resultPath);
        }
    }

    [TestMethod]
    public void HandleIncorrectWindowsPath2() {
        string sourcePath = @"C:\path\something\output_at_13:26:43.txt";
        string resultPath = PathSanitiser.SantiseWindowsPath(sourcePath);
        if (OperatingSystem.IsWindows()) {
            Assert.AreEqual(@"C:\path\something\output_at_13_26_43.txt", resultPath);
        }
        else {
            Assert.AreEqual(sourcePath, resultPath);
        }
    }

    [TestMethod]
    public void HandleNormalWindowsPath() {
        string sourcePath = @"C:\\Users\\User\\MyFolder\\File.txt";
        string resultPath = PathSanitiser.SantiseWindowsPath(sourcePath);
        Assert.AreEqual(sourcePath, resultPath);
    }

    [TestMethod]
    public void HandleStartingWhiteSpaceWindowsPath() {
        string sourcePath = @"      C:\\Users\\User\\MyFolder\\File.txt";
        string resultPath = PathSanitiser.SantiseWindowsPath(sourcePath);
        string expectedPath = @"C:\\Users\\User\\MyFolder\\File.txt";
        Assert.AreEqual(expectedPath, resultPath);
    }

    [TestMethod]
    public void HandleTrailingWhiteSpaceWindowsPath() {
        string sourcePath = @"C:\\Users\\User\\MyFolder\\File.txt   ";
        string resultPath = PathSanitiser.SantiseWindowsPath(sourcePath);
        string expectedPath = @"C:\\Users\\User\\MyFolder\\File.txt";
        Assert.AreEqual(expectedPath, resultPath);
    }

    [TestMethod]
    public void HandleWhiteSpaceWindowsPath() {
        string sourcePath = @"      C:\\Users\\User\\MyFolder\\File.txt     ";
        string resultPath = PathSanitiser.SantiseWindowsPath(sourcePath);
        string expectedPath = @"C:\\Users\\User\\MyFolder\\File.txt";
        Assert.AreEqual(expectedPath, resultPath);
    }

    [TestMethod]
    public void HandleNoDriveLetterWindowsPath() {
        string sourcePath = @"Users\\User\\MyFolder\\File.txt";
        string resultPath = PathSanitiser.SantiseWindowsPath(sourcePath);
        Assert.AreEqual(sourcePath, resultPath);
    }
}
