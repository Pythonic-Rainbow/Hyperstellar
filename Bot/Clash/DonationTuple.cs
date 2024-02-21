namespace Hyperstellar.Clash;

internal readonly struct DonationTuple(int donated, int received)
{
    internal readonly int _donated = donated;
    internal readonly int _received = received;

    internal DonationTuple Add(DonationTuple dt) => new(_donated + dt._donated, _received + dt._received);
}
