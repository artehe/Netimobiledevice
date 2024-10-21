using System;

namespace Netimobiledevice.Remoted.Xpc
{
    public abstract class XpcObject : IEquatable<XpcObject>
    {
        public abstract bool IsAligned { get; }

        public abstract bool IsPrefixed { get; }

        public abstract XpcMessageType Type { get; }

        public abstract bool Equals(XpcObject? other);
    }

    public abstract class XpcObject<T> : XpcObject
    {
        public virtual T? Data { get; set; }
    }
}
