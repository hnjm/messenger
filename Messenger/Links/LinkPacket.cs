namespace Mikodev.Network
{
    public class LinkPacket
    {
        internal int _src = 0;
        internal int _tar = 0;
        internal string _pth = null;
        internal byte[] _buf = null;
        internal PacketReader _ori = null;
        internal PacketReader _dat = null;

        public int Source => _src;

        public int Target => _tar;

        public string Path => _pth;

        public byte[] Buffer => _buf;

        public PacketReader Origin => _ori;

        public PacketReader Data => _dat;
    }
}
