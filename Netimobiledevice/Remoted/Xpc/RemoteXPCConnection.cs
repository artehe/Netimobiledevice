using Netimobiledevice.Exceptions;
using Netimobiledevice.Remoted.Frames;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Xpc
{
    public class RemoteXPCConnection
    {
        private const uint DEFAULT_SETTINGS_MAX_CONCURRENT_STREAMS = 100;
        private const uint DEFAULT_SETTINGS_INITIAL_WINDOW_SIZE = 1048576;
        private const uint DEFAULT_WIN_SIZE_INCR = 983041;

        private const int REPLY_CHANNEL = 3;
        private const int ROOT_CHANNEL = 1;

        private static readonly byte[] HTTP2_MAGIC = Encoding.UTF8.GetBytes("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n");

        private TcpClient _client;
        private NetworkStream _stream;
        private Dictionary<int, int> _nextMessageId;

        public RemoteXPCConnection(string ip, ushort port)
        {
            _client = new TcpClient(ip, port);
            _stream = _client.GetStream();
            _nextMessageId = new Dictionary<int, int> {
                { ROOT_CHANNEL, 0 },
                { REPLY_CHANNEL, 0 }
            };
        }

        private async Task DoHandshake()
        {
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
            await SendRequestAsync(new Dictionary<string, object>()).ConfigureAwait(false);
            await SendFrameAsync(new DataFrame() {
                StreamIdentifier = ROOT_CHANNEL,
                Data = XpcWrapper.Create(new Dictionary<string, object> { { "size", 0 }, { "flags", 0x0201 }, { "payload", null } }).Serialise()
            }).ConfigureAwait(false);
            _nextMessageId[ROOT_CHANNEL]++;

            // Open reply channel
            await OpenChannelAsync(REPLY_CHANNEL, XpcFlags.InitHandshake).ConfigureAwait(false);
            _nextMessageId[REPLY_CHANNEL]++;

            Frame reply = await ReceiveFrame().ConfigureAwait(false);
            if (reply is not SettingsFrame) {
                throw new NetimobiledeviceException($"Unknown frame found expected Settings frame got {reply.Type}");
            }

            // Acknowledge settings
            await SendFrameAsync(new SettingsFrame() {
                Ack = true
            }).ConfigureAwait(false);
        }

        private async Task OpenChannelAsync(uint streamId, XpcFlags flags)
        {
            flags |= XpcFlags.AlwaysSet;
            await SendFrameAsync(new HeadersFrame() {
                StreamIdentifier = streamId,
                EndHeaders = true
            }).ConfigureAwait(false);
            await SendFrameAsync(new DataFrame() {
                StreamIdentifier = streamId,
                Data = XpcWrapper.Create(new Dictionary<string, object> { { "size", 0 }, { "flags", flags }, { "payload", null } }).Serialise()
            });
        }

        private async Task<Frame> ReceiveFrame()
        {
            byte[] headerBuffer = new byte[FrameHeader.FrameHeaderLength];
            await _stream.ReadAsync(headerBuffer).ConfigureAwait(false);
            FrameHeader frameHeader = Frame.ParseFrameHeader(headerBuffer);

            byte[] frameBuffer = new byte[frameHeader.Length];
            await _stream.ReadAsync(frameBuffer).ConfigureAwait(false);
            Frame frame = Frame.Create(frameHeader.Type);
            frame.ParsePayload(frameBuffer, frameHeader);

            return frame;
        }

        private async Task SendFrameAsync(Frame frame)
        {
            IEnumerable<byte> data = frame.ToBytes();
            await _stream.WriteAsync(data.ToArray()).ConfigureAwait(false);
        }

        private async Task SendRequestAsync(Dictionary<string, object> data, bool wantingReply = false)
        {
            XpcWrapper xpcWrapper = XpcWrapper.Create(data, _nextMessageId[ROOT_CHANNEL], wantingReply);
            await SendFrameAsync(new DataFrame() {
                StreamIdentifier = ROOT_CHANNEL,
                Data = xpcWrapper.Serialise()
            }).ConfigureAwait(false);
        }

        public async Task Connect()
        {
            await DoHandshake().ConfigureAwait(false);
        }
    }
}