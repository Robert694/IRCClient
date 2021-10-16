using System.Collections.Generic;
using System.Threading.Tasks;

namespace IRCClient.Modules
{
    public class CapRequestModule : MessageModule
    {
        public List<string> CapReqList = new List<string>();
        public List<string> CapSupportList = new List<string>();
        public List<string> CapList = new List<string>();
        public bool RequestAll { get; set; }
        public bool Enabled { get; set; } = true;

        protected override async Task OnConnect()
        {
            if (Enabled)
            {
                await Client.Send(new IrcMessage().SetCommand("CAP").AddParam("LS"));
                if (!RequestAll)
                {
                    await Client.Send(new IrcMessage().SetCommand("CAP").AddParam("REQ").AddParam(string.Join(" ", CapReqList)));
                }
            }
            else
            {
                await Client.Send(new IrcMessage().SetCommand("CAP").AddParam("END"));
            }
        }

        public override async Task Process(IrcMessage data)
        {
            if (!Enabled) return;
            switch (data.Command.ToUpper())
            {
                case "CAP":
                    {
                        if (data.Params.Count < 2) return;
                        switch (data.Params[1].ToUpper())
                        {
                            case "LS":
                                if (data.Params.Count < 3) return;
                                CapSupportList = new List<string>(data.Params[2].Split());
                                if (RequestAll)
                                {
                                    CapReqList = CapSupportList;
                                    await Client.Send(new IrcMessage().SetCommand("CAP").AddParam("REQ").AddParam(string.Join(" ", CapReqList)));
                                }
                                break;
                            case "ACK":
                            case "LIST":
                                if (data.Params.Count < 3) return;
                                CapList = new List<string>(data.Params[2].Split());
                                break;
                        }
                    }
                    break;
            }
        }

        public CapRequestModule AddCapRequestList(IEnumerable<string> list)
        {
            CapReqList.AddRange(list);
            return this;
        }

        public CapRequestModule AddCapRequestList(params string[] param)
        {
            CapReqList.AddRange(param);
            return this;
        }
    }
}
