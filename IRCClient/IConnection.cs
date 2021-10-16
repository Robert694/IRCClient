using System;
using System.Threading.Tasks;

namespace IRCClient
{
    public interface IConnection
    {
        Task Connect();
        Task Disconnect();
        Task Send(string data);
        event EventHandler<IrcMessageArgs> OnReceive;
        event EventHandler<IrcMessageArgs> OnSent;
        event EventHandler OnConnect;
        event EventHandler OnDisconnect;
    }
}