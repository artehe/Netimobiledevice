using Netimobiledevice.Remoted.Frames;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Xpc;

public class RemoteXPCConnection {
    private const uint DEFAULT_SETTINGS_MAX_CONCURRENT_STREAMS = 100;
    private const uint DEFAULT_SETTINGS_INITIAL_WINDOW_SIZE = 1048576;
    private const uint DEFAULT_WIN_SIZE_INCR = 983041;

    private const int REPLY_CHANNEL = 3;
    private const int ROOT_CHANNEL = 1;

    private static readonly byte[] HTTP2_MAGIC = Encoding.UTF8.GetBytes("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n");

    private byte[] _previousFrameData = [];

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly Dictionary<uint, ulong> _nextMessageId;

    public string Address { get; }

    public RemoteXPCConnection(string ip, int port) {
        Address = $"{ip}";
        _client = new TcpClient(ip, port);
        _stream = _client.GetStream();
        _nextMessageId = new Dictionary<uint, ulong> {
            { ROOT_CHANNEL, 0 },
            { REPLY_CHANNEL, 0 }
        };
    }

    private void DoHandshake() {
        _stream.Write(HTTP2_MAGIC);
        _stream.Flush();

        // Send h2 headers
        SendFrame(
            new SettingsFrame() {
                MaxConcurrentStreams = DEFAULT_SETTINGS_MAX_CONCURRENT_STREAMS,
                InitialWindowSize = DEFAULT_SETTINGS_INITIAL_WINDOW_SIZE
            }
        );
        SendFrame(
            new WindowUpdateFrame() {
                WindowSizeIncrement = DEFAULT_WIN_SIZE_INCR,
                StreamIdentifier = 0
            }
        );
        SendFrame(
            new HeadersFrame() {
                StreamIdentifier = ROOT_CHANNEL,
                EndHeaders = true
            }
        );

        // Send first actual requests
        SendRequest([]);
        SendFrame(
            new DataFrame() {
                StreamIdentifier = ROOT_CHANNEL,
                Data = new XpcWrapper {
                    Flags = (XpcFlags) 0x0201,
                    Message = new XpcMessage() {
                        Payload = null
                    }
                }.Serialise()
            }
        );
        _nextMessageId[ROOT_CHANNEL]++;

        // Open reply channel
        OpenChannel(REPLY_CHANNEL, XpcFlags.InitHandshake);
        _nextMessageId[REPLY_CHANNEL]++;

        Frame reply = ReceiveFrame();
        if (reply is not SettingsFrame) {
            throw new NetimobiledeviceException($"Unknown frame found expected Settings frame got {reply.Type}");
        }

        // Acknowledge settings
        SendFrame(
            new SettingsFrame() {
                Ack = true
            }
        );
    }

    private async Task DoHandshakeAsync() {
        await _stream.WriteAsync(HTTP2_MAGIC).ConfigureAwait(false);
        await _stream.FlushAsync().ConfigureAwait(false);

        // Send h2 headers
        await SendFrameAsync(new SettingsFrame {
            MaxConcurrentStreams = DEFAULT_SETTINGS_MAX_CONCURRENT_STREAMS,
            InitialWindowSize = DEFAULT_SETTINGS_INITIAL_WINDOW_SIZE
        }).ConfigureAwait(false);
        await SendFrameAsync(new WindowUpdateFrame {
            WindowSizeIncrement = DEFAULT_WIN_SIZE_INCR,
            StreamIdentifier = 0
        }).ConfigureAwait(false);
        await SendFrameAsync(new HeadersFrame() {
            StreamIdentifier = ROOT_CHANNEL,
            EndHeaders = true
        }).ConfigureAwait(false);

        // Send first actual requests
        await SendRequestAsync([]).ConfigureAwait(false);
        await SendFrameAsync(new DataFrame() {
            StreamIdentifier = ROOT_CHANNEL,
            Data = new XpcWrapper {
                Flags = (XpcFlags) 0x0201,
                Message = new XpcMessage() {
                    Payload = null
                }
            }.Serialise()
        }).ConfigureAwait(false);
        _nextMessageId[ROOT_CHANNEL]++;

        // Open reply channel
        await OpenChannelAsync(REPLY_CHANNEL, XpcFlags.InitHandshake).ConfigureAwait(false);
        _nextMessageId[REPLY_CHANNEL]++;

        Frame reply = await ReceiveFrameAsync().ConfigureAwait(false);
        if (reply is not SettingsFrame) {
            throw new NetimobiledeviceException($"Unknown frame found expected Settings frame got {reply.Type}");
        }

        // Acknowledge settings
        await SendFrameAsync(new SettingsFrame() {
            Ack = true
        }).ConfigureAwait(false);
    }

    private void OpenChannel(uint streamId, XpcFlags flags) {
        flags |= XpcFlags.AlwaysSet;
        SendFrame(
            new HeadersFrame() {
                StreamIdentifier = streamId,
                EndHeaders = true
            }
        );
        SendFrame(
            new DataFrame() {
                StreamIdentifier = streamId,
                Data = new XpcWrapper {
                    Flags = flags,
                    Message = new XpcMessage() {
                        Payload = null
                    }
                }.Serialise()
            }
        );
    }

    private async Task OpenChannelAsync(uint streamId, XpcFlags flags) {
        flags |= XpcFlags.AlwaysSet;
        await SendFrameAsync(new HeadersFrame() {
            StreamIdentifier = streamId,
            EndHeaders = true
        }).ConfigureAwait(false);
        await SendFrameAsync(new DataFrame() {
            StreamIdentifier = streamId,
            Data = new XpcWrapper {
                Flags = flags,
                Message = new XpcMessage() {
                    Payload = null
                }
            }.Serialise()
        });
    }

    private Frame ReceiveFrame() {
        byte[] headerBuffer = new byte[FrameHeader.FrameHeaderLength];
        _stream.ReadExactly(headerBuffer);
        FrameHeader frameHeader = Frame.ParseFrameHeader(headerBuffer);

        byte[] frameBuffer = new byte[frameHeader.Length];
        _stream.ReadExactly(frameBuffer);
        Frame frame = Frame.Create(frameHeader.Type);
        frame.ParsePayload(frameBuffer, frameHeader);

        return frame;
    }

    private async Task<Frame> ReceiveFrameAsync() {
        byte[] headerBuffer = new byte[FrameHeader.FrameHeaderLength];
        await _stream.ReadAsync(headerBuffer).ConfigureAwait(false);
        FrameHeader frameHeader = Frame.ParseFrameHeader(headerBuffer);

        byte[] frameBuffer = new byte[frameHeader.Length];
        await _stream.ReadAsync(frameBuffer).ConfigureAwait(false);
        Frame frame = Frame.Create(frameHeader.Type);
        frame.ParsePayload(frameBuffer, frameHeader);

        return frame;
    }

    private DataFrame ReceiveNextDataFrame() {
        while (true) {
            Frame frame = ReceiveFrame();
            if (frame is GoAwayFrame) {
                throw new NetimobiledeviceException($"Stream closed got frame {frame}");
            }
            if (frame is RstStreamFrame) {
                throw new NetimobiledeviceException($"Stream closed got frame {frame}");
            }

            if (frame is DataFrame dataFrame) {
                if (dataFrame.StreamIdentifier % 2 == 0 && dataFrame.PayloadLength > 0) {
                    SendFrame(
                        new WindowUpdateFrame() {
                            StreamIdentifier = 0,
                            WindowSizeIncrement = dataFrame.PayloadLength
                        }
                    );
                    SendFrame(
                        new WindowUpdateFrame() {
                            StreamIdentifier = dataFrame.StreamIdentifier,
                            WindowSizeIncrement = dataFrame.PayloadLength
                        }
                    );
                }
                return dataFrame;
            }
        }
    }

    private async Task<DataFrame> ReceiveNextDataFrameAsync() {
        while (true) {
            Frame frame = await ReceiveFrameAsync().ConfigureAwait(false);
            if (frame is GoAwayFrame) {
                throw new NetimobiledeviceException($"Stream closed got frame {frame}");
            }
            if (frame is RstStreamFrame) {
                throw new NetimobiledeviceException($"Stream closed got frame {frame}");
            }

            if (frame is DataFrame dataFrame) {
                if (dataFrame.StreamIdentifier % 2 == 0 && dataFrame.PayloadLength > 0) {
                    await SendFrameAsync(new WindowUpdateFrame() {
                        StreamIdentifier = 0,
                        WindowSizeIncrement = dataFrame.PayloadLength
                    }).ConfigureAwait(false);
                    await SendFrameAsync(new WindowUpdateFrame() {
                        StreamIdentifier = dataFrame.StreamIdentifier,
                        WindowSizeIncrement = dataFrame.PayloadLength
                    }).ConfigureAwait(false);
                }
                return dataFrame;
            }
        }
    }

    private void SendFrame(Frame frame) {
        IEnumerable<byte> data = frame.ToBytes();
        _stream.Write(data.ToArray());
    }

    private async Task SendFrameAsync(Frame frame) {
        IEnumerable<byte> data = frame.ToBytes();
        await _stream.WriteAsync(data.ToArray()).ConfigureAwait(false);
    }

    private void SendRequest(Dictionary<string, XpcObject> data, bool wantingReply = false) {
        XpcWrapper xpcWrapper = XpcWrapper.Create(data, _nextMessageId[ROOT_CHANNEL], wantingReply);
        SendFrame(
            new DataFrame() {
                StreamIdentifier = ROOT_CHANNEL,
                Data = xpcWrapper.Serialise()
            }
        );
    }

    private async Task SendRequestAsync(Dictionary<string, XpcObject> data, bool wantingReply = false) {
        XpcWrapper xpcWrapper = XpcWrapper.Create(data, _nextMessageId[ROOT_CHANNEL], wantingReply);
        await SendFrameAsync(new DataFrame() {
            StreamIdentifier = ROOT_CHANNEL,
            Data = xpcWrapper.Serialise()
        }).ConfigureAwait(false);
    }

    public void Close() {
        _stream.Close();
        _client.Close();
    }

    public void Connect() {
        DoHandshake();
    }

    public async Task ConnectAsync() {
        await DoHandshakeAsync().ConfigureAwait(false);
    }

    public XpcDictionary ReceiveResponse() {
        while (true) {
            DataFrame frame = ReceiveNextDataFrame();

            XpcMessage? message;
            try {
                message = XpcWrapper.Deserialise([.. _previousFrameData, .. frame.Data]).Message;
                _previousFrameData = [];
            }
            catch (Exception) {
                _previousFrameData = [.. _previousFrameData, .. frame.Data];
                continue;
            }

            if (message is null || message.Payload is null) {
                continue;
            }
            if (message.Payload.Obj == null) {
                continue;
            }
            if (message.Payload.Obj.AsXpcDictionary().Count == 0) {
                continue;
            }

            _nextMessageId[frame.StreamIdentifier] = message.MessageId + 1;
            return message.Payload.Obj.AsXpcDictionary();
        }
    }
    public async Task<XpcDictionary> ReceiveResponseAsync() {
        while (true) {
            DataFrame frame = await ReceiveNextDataFrameAsync().ConfigureAwait(false);

            XpcMessage? message;
            try {
                message = XpcWrapper.Deserialise([.. _previousFrameData, .. frame.Data]).Message;
                _previousFrameData = [];
            }
            catch (Exception) {
                _previousFrameData = [.. _previousFrameData, .. frame.Data];
                continue;
            }

            if (message is null || message.Payload is null) {
                continue;
            }
            if (message.Payload.Obj == null) {
                continue;
            }
            if (message.Payload.Obj.AsXpcDictionary().Count == 0) {
                continue;
            }

            _nextMessageId[frame.StreamIdentifier] = message.MessageId + 1;
            return message.Payload.Obj.AsXpcDictionary();
        }
    }
}
