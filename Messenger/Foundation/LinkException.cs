using System;

namespace Messenger.Foundation
{
    [Serializable]
    public sealed class LinkException : ApplicationException
    {
        private ErrorCode _error = ErrorCode.None;

        public ErrorCode ErrorCode => _error;

        public override string Message => GetMessage(_error);

        public LinkException(ErrorCode code) => _error = code;

        public static string GetMessage(ErrorCode code)
        {
            switch (code)
            {
                case ErrorCode.Success:
                    return "Operation successful.";
                case ErrorCode.Filled:
                    return "Server is full now.";
                case ErrorCode.Invalid:
                    return "User id out of range.";
                case ErrorCode.Conflict:
                    return "User id conflict.";
                case ErrorCode.Shutdown:
                    return "Server has been shutdown.";
                case ErrorCode.InnerError:
                    return "Inner error.";
                default:
                    return "Undefined error.";
            }
        }
    }
}
