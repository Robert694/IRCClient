using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;

namespace IRCClient.Modules
{
    public class PingModule : MessageModule
    {
        private Timer PingTimer { get; set; }
        private readonly Dictionary<string, Stopwatch> _watches = new Dictionary<string, Stopwatch>();
        public long Ping { get; private set; }
        public bool SendOnConnect { get; set; } = true;
        public bool ReplyToServerPings { get; set; }

        public double Interval
        {
            get => PingTimer.Interval;
            set => PingTimer.Interval = value;
        }

        public event EventHandler<long> OnPingUpdate;

        protected override async Task OnConnect()
        {
            PingTimer?.Dispose();
            PingTimer = new Timer(30000);
            PingTimer.Elapsed += TimerOnElapsed;
            PingTimer.Start();
            if (SendOnConnect)
            {
                await SendPing();
            }
        }

        private async void TimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            await SendPing();
        }

        public override async Task Process(IrcMessage data)
        {
            switch (data.Command.ToUpper())
            {
                case "PONG":
                    if (data.Params.Count > 1)
                    {
                        if (_watches.TryGetValue(data.Params[1], out var watch))
                        {
                            watch.Stop();
                            _watches.Remove(data.Params[0]);
                            Ping = watch.ElapsedMilliseconds;
                            OnPingUpdate?.Invoke(this, watch.ElapsedMilliseconds);
                        }
                    }
                    break;
                case "PING":
                    if(!ReplyToServerPings)return;
                    Client.SendPong(data.Params[0]);
                    break;
            }
        }

        public async Task SendPing([CallerMemberName] string name = "")
        {
            try
            {
                string g = Guid.NewGuid().ToString();
                if (_watches.Count >= 10)
                {
                    _watches.Clear();
                }
                _watches.Add(g, Stopwatch.StartNew());
                await Client.SendPing(g);
            }
            catch
            {
                PingTimer.Stop();
            }
        }
    }
}