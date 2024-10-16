using System;
using System.Collections.Generic;

namespace Netimobiledevice.Remoted.Xpc
{
    public class XpcWrapper
    {
        public uint Magic => 0x29b00b92;
        public XpcFlags Flags { get; private set; }
        public XpcMessage Message { get; private set; }

        private XpcWrapper() { }

        public static XpcWrapper Create(Dictionary<string, object> data, ulong messageId = 0, bool wantingReply = false)
        {
            XpcFlags flags = XpcFlags.AlwaysSet;
            if (data.Count > 0) {
                flags |= XpcFlags.DataPresent;
            }
            if (wantingReply) {
                flags |= XpcFlags.WantingReply;
            }

            return new XpcWrapper() {
                Flags = flags,
                Message = new XpcMessage() {
                    MessageId = (uint) messageId,
                    Payload = new XpcPayload() {
                        Obj = XpcObject.Parse(data)
                    }
                }
            };
        }

        public byte[] Serialise()
        {
            return [
                .. BitConverter.GetBytes(Magic),
                .. BitConverter.GetBytes((uint) Flags),
                .. Message.Serialise()
            ];
        }
    }
}
