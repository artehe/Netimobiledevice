namespace Netimobiledevice.Remoted.Xpc
{
    public abstract class XpcObject
    {
        public abstract bool IsAligned { get; }

        public abstract bool IsPrefixed { get; }

        public abstract XpcMessageType Type { get; }
    }

    public abstract class XpcObject<T> : XpcObject
    {
        public virtual T? Data { get; set; }
    }
}
