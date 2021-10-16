namespace IRCClient
{
    public interface IConnectionSupplier
    {
        IConnection GetConnection();
    }
}
