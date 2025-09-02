namespace Netimobiledevice.Remoted.Frames;

internal enum ErrorCode : byte
{
    /// <summary>
    /// The associated condition is not a result of an error. For example, a GOAWAY might include this code to indicate graceful shutdown of a connection.
    /// </summary>
    NoError = 0x0,

    /// <summary>
    /// The endpoint detected an unspecific protocol error. This error is for use when a more specific error code is not available.
    /// </summary>
    ProtocolError = 0x1,

    /// <summary>
    /// The endpoint encountered an unexpected internal error.
    /// </summary>
    InternalError = 0x2,

    /// <summary>
    /// The endpoint detected that its peer violated the flow-control protocol.
    /// </summary>
    FlowControlError = 0x3,

    /// <summary>
    /// The endpoint sent a SETTINGS frame but did not receive a response in a timely manner.
    /// </summary>
    SettingsTimeout = 0x4,

    /// <summary>
    /// The endpoint received a frame after a stream was half-closed.
    /// </summary>
    StreamClosed = 0x5,

    /// <summary>
    /// The endpoint received a frame with an invalid size.
    /// </summary>
    FrameSizeError = 0x6,

    /// <summary>
    /// The endpoint refused the stream prior to performing any application processing
    /// </summary>
    RefusedStream = 0x7,

    /// <summary>
    /// Used by the endpoint to indicate that the stream is no longer needed.
    /// </summary>
    Cancel = 0x8,

    /// <summary>
    /// The endpoint is unable to maintain the header compression context for the connection.
    /// </summary>
    CompressionError = 0x9,

    /// <summary>
    /// The connection established in response to a CONNECT request was reset or abnormally closed.
    /// </summary>
    ConnectError = 0xA,

    /// <summary>
    /// The endpoint detected that its peer is exhibiting a behavior that might be generating excessive load.
    /// </summary>
    EnhanceYourCalm = 0xB,

    /// <summary>
    /// The underlying transport has properties that do not meet minimum security requirements
    /// </summary>
    InadequateSecurity = 0xC,

    /// <summary>
    /// The endpoint requires that HTTP/1.1 be used instead of HTTP/2.
    /// </summary>
    Http11Required = 0xD,
}
