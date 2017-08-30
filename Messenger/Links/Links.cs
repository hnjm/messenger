namespace Mikodev.Network
{
    public static class Links
    {
        public const string Protocol = "mikodev.messenger.v1.10";

        public const int Port = 7550;

        public const int BroadcastPort = 7550;

        public const int Timeout = 3 * 1000;

        public const int KeepAliveBefore = 20 * 1000;

        public const int KeepAliveInterval = Timeout;

        public const int ID = 0;

        public const int Count = 256;

        public const int Buffer = 4 * 1024;

        public const int BufferLimit = 1024 * 1024;

        public const int Queue = 4 * 1024 * 1024;

        public const int Delay = 4;
    }
}
