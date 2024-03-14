namespace Hyperstellar.Clash;

internal sealed class Violator(string id)
{
    internal readonly string _id = id;
    internal uint? _donated;
    internal uint? _raided;
}
