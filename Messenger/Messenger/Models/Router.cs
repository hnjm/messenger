using Mikodev.Network;

namespace Messenger.Models
{
    public class Router
    {
        private int _src = 0;
        private int _tar = 0;
        private string _pth = null;
        private byte[] _buf = null;
        private PacketReader _und = null;
        private PacketReader _dat = null;

        public int Source => _src;
        public int Target => _tar;
        public string Path => _pth;
        public byte[] Buffer => _buf;
        public PacketReader Origin => _und;
        public PacketReader Data => _dat;

        public Router() { }

        public void Load(byte[] buf)
        {
            _buf = buf;
            _und = new PacketReader(buf);
            _src = _und["source"].Pull<int>();
            _tar = _und["target"].Pull<int>();
            _pth = _und["path"].Pull<string>();
            _dat = _und["data", true];
        }
    }
}
