using System;

namespace IRCClient
{
    public class IrcMessageArgs : EventArgs
    {
        public IrcMessageArgs(string data)
        {
            Data = data;
        }
        public string Data { get; }
    }
}