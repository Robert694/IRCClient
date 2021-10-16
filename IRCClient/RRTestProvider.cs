using System;
using System.Collections.Generic;
using IRCClient.Connections;

namespace IRCClient
{
    public class RRTestProvider : IConnectionSupplier
    {
        private readonly object lck = new object();
        private List<(string Host, int Port)> Addresses = new List<(string Host, int Port)>();
        private int Index = 0;

        private IConnection GetConnection()
        {
            if (Addresses.Count == 0) throw new Exception($"No Addresses in {nameof(RRTestProvider)}");
            lock (lck)
            {
                var i = Index;
                Index = (Index + 1) % Addresses.Count;
                return new IrcConnection(Addresses[i].Host, Addresses[i].Port, null);//Can configure custom provider to connect on create
            }
        }

        IConnection IConnectionSupplier.GetConnection() => GetConnection();

        public RRTestProvider AddAddress((string Host, int Port) address)
        {
            lock (lck)
            {
                Addresses.Add(address);
            }

            return this;
        }

        public RRTestProvider AddAddresses(IEnumerable<(string Host, int Port)> addresses)
        {
            lock (lck)
            {
                Addresses.AddRange(addresses);
            }

            return this;
        }
    }
}