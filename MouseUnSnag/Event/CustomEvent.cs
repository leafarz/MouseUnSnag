using System;

namespace MouseUnSnag.Event
{
    public class CustomEvent<T> : EventArgs
    {
        public T Payload { get; set; }
    }
}
