using Mikodev.Binary;

namespace Mikodev.Network
{
    public class LinkPacket
    {
        public int Source { get; internal set; } = 0;

        public int Target { get; internal set; } = 0;

        public string Path { get; internal set; } = null;

        public Token Data { get; internal set; } = null;

        public Token Origin { get; internal set; } = null;

        public byte[] Buffer { get; internal set; } = null;
    }
}
