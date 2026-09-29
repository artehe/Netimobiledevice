using Netimobiledevice.Remote.Http2;
using Netimobiledevice.Remoted.Frames;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Remote.Xpc;

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

    private async Task DoHandshake(CancellationToken ct) {
        await _stream.WriteAsync(HTTP2_MAGIC, ct).ConfigureAwait(false);
        await _stream.FlushAsync(ct).ConfigureAwait(false);

        // Send h2 headers
        await SendFrameAsync(
            new SettingsFrame {
                MaxConcurrentStreams = DEFAULT_SETTINGS_MAX_CONCURRENT_STREAMS,
                InitialWindowSize = DEFAULT_SETTINGS_INITIAL_WINDOW_SIZE
            },
            ct
        ).ConfigureAwait(false);
        await SendFrameAsync(
            new WindowUpdateFrame {
                WindowSizeIncrement = DEFAULT_WIN_SIZE_INCR,
                StreamIdentifier = 0
            },
            ct
        ).ConfigureAwait(false);
        await SendFrameAsync(
            new HeadersFrame() {
                StreamIdentifier = ROOT_CHANNEL,
                EndHeaders = true
            },
            ct
        ).ConfigureAwait(false);

        // Send first actual requests
        await SendRequestAsync([], ct).ConfigureAwait(false);
        await SendFrameAsync(
            new DataFrame() {
                StreamIdentifier = ROOT_CHANNEL,
                Data = new XpcWrapper {
                    Flags = (XpcFlags) 0x0201,
                    Message = new XpcMessage() {
                        Payload = null
                    }
                }.Serialise()
            },
            ct
        ).ConfigureAwait(false);
        _nextMessageId[ROOT_CHANNEL]++;

        // Open reply channel
        await OpenChannelAsync(REPLY_CHANNEL, XpcFlags.InitHandshake, ct).ConfigureAwait(false);
        _nextMessageId[REPLY_CHANNEL]++;

        Frame reply = await ReceiveFrame().ConfigureAwait(false);
        if (reply is not SettingsFrame) {
            throw new NetimobiledeviceException($"Unknown frame found expected Settings frame got {reply.Type}");
        }

        // Acknowledge settings
        await SendFrameAsync(
            new SettingsFrame() {
                Ack = true
            },
            ct
        ).ConfigureAwait(false);
    }

    private async Task OpenChannelAsync(uint streamId, XpcFlags flags, CancellationToken ct) {
        flags |= XpcFlags.AlwaysSet;
        await SendFrameAsync(
            new HeadersFrame() {
                StreamIdentifier = streamId,
                EndHeaders = true
            },
            ct
        ).ConfigureAwait(false);
        await SendFrameAsync(
            new DataFrame() {
                StreamIdentifier = streamId,
                Data = new XpcWrapper {
                    Flags = flags,
                    Message = new XpcMessage() {
                        Payload = null
                    }
                }.Serialise()
            },
            ct
        );
    }

    private async Task<Frame> ReceiveFrame() {
        byte[] headerBuffer = new byte[Frame.HeaderLength];
        await _stream.ReadAsync(headerBuffer).ConfigureAwait(false);
        FrameHeader frameHeader = Frame.ParseFrameHeader(headerBuffer);

        byte[] frameBuffer = new byte[frameHeader.Length];
        await _stream.ReadAsync(frameBuffer).ConfigureAwait(false);
        Frame frame = Frame.Create(frameHeader.Type);
        frame.ParsePayload(frameBuffer, frameHeader);

        return frame;
    }

    private async Task<DataFrame> ReceiveNextDataFrame(CancellationToken ct) {
        while (true) {
            Frame frame = await ReceiveFrame().ConfigureAwait(false);

            if (frame is GoAwayFrame) {
                throw new NetimobiledeviceException($"Stream closed got frame {frame}");
            }
            if (frame is RstStreamFrame) {
                throw new NetimobiledeviceException($"Stream closed got frame {frame}");
            }

            if (frame is DataFrame dataFrame) {
                if (dataFrame.StreamIdentifier % 2 == 0 && dataFrame.PayloadLength > 0) {
                    await SendFrameAsync(
                        new WindowUpdateFrame() {
                            StreamIdentifier = 0,
                            WindowSizeIncrement = dataFrame.PayloadLength
                        },
                        ct
                    ).ConfigureAwait(false);
                    await SendFrameAsync(
                        new WindowUpdateFrame() {
                            StreamIdentifier = dataFrame.StreamIdentifier,
                            WindowSizeIncrement = dataFrame.PayloadLength
                        },
                        ct
                    ).ConfigureAwait(false);
                }
                return dataFrame;
            }
        }
    }

    private async Task SendFrameAsync(Frame frame, CancellationToken cancellationToken) {
        IEnumerable<byte> data = frame.ToBytes();
        await _stream.WriteAsync(data.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    public void Close() {
        _stream.Close();
        _client.Close();
    }

    public async Task Connect(CancellationToken ct) {
        await DoHandshake(ct).ConfigureAwait(false);
    }

    public async Task<XpcDictionary> ReceiveResponse(CancellationToken ct) {
        while (true) {
            DataFrame frame = await ReceiveNextDataFrame(ct).ConfigureAwait(false);

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

    public async Task<XpcDictionary> ReceiveResponseAsync(CancellationToken ct) {
        while (true) {
            DataFrame frame = await ReceiveNextDataFrame(ct).ConfigureAwait(false);

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

    public async Task SendRequestAsync(XpcDictionary data, CancellationToken ct, bool wantingReply = false) {
        XpcWrapper xpcWrapper = XpcWrapper.Create(data, _nextMessageId[ROOT_CHANNEL], wantingReply);
        await SendFrameAsync(
            new DataFrame() {
                StreamIdentifier = ROOT_CHANNEL,
                Data = xpcWrapper.Serialise()
            },
            ct
        ).ConfigureAwait(false);
    }
}
