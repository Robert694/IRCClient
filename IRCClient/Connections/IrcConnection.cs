using System;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace IRCClient.Connections
{
    public class IrcConnection : IConnection, IDisposable
    {
        public event EventHandler<IrcMessageArgs> OnReceive;
        public event EventHandler<IrcMessageArgs> OnSent;
        public event EventHandler OnConnect;
        public event EventHandler OnDisconnect;
        public string Host { get; }
        public int Port { get; }
        public TcpClient Client { get; private set; }
        public NetworkStream Stream { get; private set; }
        public StreamReader Reader { get; private set; }
        public StreamWriter Writer { get; private set; }
        public TimeSpan? ConnectTimeout { get; private set; }
        private BackgroundWorker BackgroundWorker { get; set; }

        public IrcConnection(string host, int port, TimeSpan? connectTimeout = null)
        {
            Host = host;
            Port = port;
            ConnectTimeout = connectTimeout;
        }

        public async Task Connect()
        {
            Client = new TcpClient();
            var connect = Client.ConnectAsync(Host, Port);
            if (ConnectTimeout.HasValue)
            {
                if (await Task.WhenAny(connect, Task.Delay(ConnectTimeout.Value)) == connect)
                {
                    await connect;
                }
                else
                {
                    Client.Close();
                    throw new TimeoutException($"Failed to connect to '{Host}:{Port}'");
                }
            }
            else
            {
                await connect;
            }
            Stream = Client.GetStream();
            Writer = new StreamWriter(Stream){AutoFlush = true};
            Reader = new StreamReader(Stream);
            BackgroundWorker = new BackgroundWorker();
            BackgroundWorker.DoWork += BackgroundWorkerOnDoWork;
            BackgroundWorker.RunWorkerAsync();
            OnConnect?.Invoke(this, EventArgs.Empty);
        }

        private void BackgroundWorkerOnDoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                string line;
                while ((line = Reader.ReadLine()) != null)
                {
                    if(BackgroundWorker.CancellationPending) break;
                    OnReceive?.Invoke(this, new IrcMessageArgs(line));
                }
            }
            catch
            {
            }
            OnDisconnect?.Invoke(this, EventArgs.Empty);
        }

        public async Task Disconnect()
        {
            Dispose();
        }

        public async Task Send(string data)
        {
            await Writer.WriteLineAsync(data);
            OnSent?.Invoke(this, new IrcMessageArgs(data));
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Client?.Dispose();
                this.BackgroundWorker?.Dispose();
                this.Stream?.Dispose();
                this.Reader?.Dispose();
                this.Writer?.Dispose();
            }
        }
    }
}