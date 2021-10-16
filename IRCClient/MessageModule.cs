using System;
using System.Threading.Tasks;

namespace IRCClient
{
    public abstract class MessageModule : IDisposable
    {
        protected virtual IrcClient Client { get; private set; }
        protected virtual IConnection Connection { get; private set; }
        public virtual bool SkipOtherModules { get; private set; }

        public virtual async Task Init(IrcClient client, IConnection connection)
        {
            Client = client;
            Connection = connection;
            await OnConnect();
        }

        protected virtual async Task OnConnect() { }
        public virtual async Task Process(IrcMessage data) { }
        public virtual void Dispose() { }
    }
}