using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IRCClient
{
    public class IrcClient
    {
        private IConnection Connection { get; }
        public List<MessageModule> Modules { get; }
        public event EventHandler<IrcMessageArgs> OnReceived
        {
            add => Connection.OnReceive += value;
            remove => Connection.OnReceive -= value;
        }
        public event EventHandler<IrcMessageArgs> OnSent
        {
            add => Connection.OnSent += value;
            remove => Connection.OnSent -= value;
        }

        public event EventHandler OnConnect
        {
            add => Connection.OnConnect += value;
            remove => Connection.OnConnect -= value;
        }

        public event EventHandler OnDisconnect
        {
            add => Connection.OnDisconnect += value;
            remove => Connection.OnDisconnect -= value;
        }

        public IrcClient(IConnection connection)
        {
            Connection = connection;
            Modules = new List<MessageModule>();
            OnConnect += OnOnConnect;
            OnReceived += OnReceive;
        }

        private async void OnOnConnect(object sender, EventArgs e)
        {
            for (int i = Modules.Count; i-- > 0;)
            {
                await Modules[i].Init(this, Connection);
            }
        }

        private async void OnReceive(object sender, IrcMessageArgs e)
        {
            if (IrcMessage.TryParse(e.Data, out var msg))
            {
                for (int i = Modules.Count; i-- > 0;)
                {
                    await Modules[i].Process(msg);
                    if(Modules[i].SkipOtherModules)break;
                }
            }
        }

        public Task Connect() => Connection.Connect();

        public Task Disconnect() => Connection.Disconnect();

        public Task Send(string data) => Connection.Send(data);

        public Task Send(IrcMessage data) => Send(data.ToString());

        public Task SendMessage(string channel, string message) => Send(new IrcMessage().SetCommand("PRIVMSG").AddParam(channel).AddMsgParam(message));

        public Task SendMessage(IRepliable data, string message) => SendMessage(data.Channel, message);

        public Task SendActionMessage(string channel, string message) => Send(new IrcMessage().SetCommand("PRIVMSG").AddParam(channel).AddMsgParam($"\u0001ACTION {message}\u0001"));

        public Task SendActionMessage(IRepliable data, string message) => SendActionMessage(data.Channel, message);

        public Task JoinChannel(string channel) => Send(new IrcMessage().SetCommand("JOIN").AddParam(channel));

        public Task LeaveChannel(string channel, string reason = default) => Send(new IrcMessage().SetCommand("PART").AddParam(channel).AddMsgParam(reason));

        public Task SendPass(string pass) => Send(new IrcMessage().SetCommand("PASS").AddParam(pass));

        public Task SendNick(string nick) => Send(new IrcMessage().SetCommand("NICK").AddParam(nick));

        public Task Quit(string reason = default) => Send(new IrcMessage().SetCommand("QUIT").AddMsgParam(reason));

        public Task SendPing(string message = default) => Send(new IrcMessage().SetCommand("PING").AddMsgParam(message));

        public Task SendPong(string message = default) => Send(new IrcMessage().SetCommand("PONG").AddMsgParam(message));

        public IrcClient AddModule<T>(Action<T> config = default) where T : MessageModule, new()
        {
            T t = new T();
            config?.Invoke(t);
            Modules.Add(t);
            return this;
        }

        public IrcClient AddModule(params MessageModule[] modules)
        {
            Modules.AddRange(modules);
            return this;
        }

        public IrcClient RemoveModule(MessageModule module)
        {
            Modules.Remove(module);
            module?.Dispose();
            return this;
        }

        public IrcClient RemoveModule<T>() where T : MessageModule
        {
            var type = typeof(T);
            for (int i = Modules.Count; i-- > 0;)
            {
                if (type == Modules[i].GetType())
                {
                    Modules[i]?.Dispose();
                    Modules.RemoveAt(i);
                }
            }
            return this;
        }

        public T GetModule<T>() where T : MessageModule
        {
            var type = typeof(T);
            for (int i = Modules.Count; i-- > 0;)
            {
                if (type == Modules[i].GetType())
                    return (T) Modules[i];
            }

            return default;
        }
    }
}
