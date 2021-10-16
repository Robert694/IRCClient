# IRCClient
An extensible IRC library written in C# with some Twitch.tv CAP support

Example:
```cs
var client = new IrcClient(new IrcConnection("host", 6667));
client.AddModule<PingModule>(x => x.SendOnConnect = false);
client.AddModule<CapRequestModule>(x => x.AddCapRequestList(TwitchModule.CapTags, TwitchModule.CapCommands, TwitchModule.CapMembership));
client.AddModule<TwitchModule>(x => {  }); // Add logic here
client.OnConnect += (sender, args) => { }; // Add logic here
client.OnDisconnect += (sender, args) => { }; // Add logic here
client.OnReceived += (sender, args) => { }; // Add logic here
await client.Connect();
```
