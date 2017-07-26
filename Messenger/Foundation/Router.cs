using Mikodev.Network;

namespace Messenger.Foundation
{
    public class Router
    {
        private int _src = 0;
        private int _tar = 0;
        private string _pth = null;
        private byte[] _buf = null;
        private PacketReader _ori = null;
        private PacketReader _dat = null;

        public int Source => _src;
        public int Target => _tar;
        public string Path => _pth;
        public byte[] Buffer => _buf;
        public PacketReader Origin => _ori;
        public PacketReader Data => _dat;

        public Router() { }

        public Router Load(byte[] buf)
        {
            _buf = buf;
            _ori = new PacketReader(buf);
            _src = _ori["source"].Pull<int>();
            _tar = _ori["target"].Pull<int>();
            _pth = _ori["path"].Pull<string>();
            _dat = _ori["data", true];
            return this;
        }
    }
}
