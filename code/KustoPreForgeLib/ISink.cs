namespace KustoPreForgeLib
{
    internal interface ISink
    {
        Task ProcessSourceAsync();
    }
}