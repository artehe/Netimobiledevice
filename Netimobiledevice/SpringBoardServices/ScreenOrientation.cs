namespace Netimobiledevice.SpringBoardServices
{
    /// <summary>
    /// Represent the interface orientations same as <see href="https://developer.apple.com/documentation/uikit/uiinterfaceorientation">UIKit UIInterfaceOrientation</see>.
    /// </summary>
    public enum ScreenOrientation
    {
        /// <summary>
        /// The interface orientation is unknown.
        /// </summary>
        Unknown = 0,
        /// <summary>
        /// The portrait orientation, where the device is in an upright position with the home button at the bottom.
        /// </summary>
        Portrait = 1,
        /// <summary>
        /// The portrait orientation, where the device is in an upright position with the home button at the top.
        /// </summary>
        PortraitUpsideDown = 2,
        /// <summary>
        /// The landscape orientation, where the device is in a horizontal position with the home button on the left side.
        /// </summary>
        LandscapeLeft = 3,
        /// <summary>
        /// The landscape orientation, where the device is in a horizontal position with the home button on the right side.
        /// </summary>
        LandscapeRight = 4
    }
}
