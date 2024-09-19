using Discord.Rest;

namespace Hyperstellar.Discord;

internal sealed class ReadyHandler(int msTryInterval, int msExceptionInterval, Func<Task> tryFunc)
{
    private Exception? _exception;
    private RestUserMessage? _msg;
    private int _repeatCount;

    internal async Task RunAsync()
    {
        while (true)
        {
            try
            {
                await tryFunc();
                _exception = null;
                _msg = null;
                _repeatCount = 0;
                await Task.Delay(msTryInterval);
            }
            catch (Exception ex)
            {
                if (_exception == null || _exception.Message != ex.Message || _exception.StackTrace != ex.StackTrace)
                {
                    _exception = ex;
                    _repeatCount = 0;
                    _msg = await Dc.NewExceptionLogAsync(ex);
                }
                else if (_repeatCount < 10)
                {
                    _repeatCount++;
                    await Program.TryUntilAsync(async () =>
                    {
                        await _msg!.ModifyAsync(mp => mp.Content = _msg.Content + "\n" + DateTime.Now.ToString("HH:mm:ss"));
                    });
                }
                await Task.Delay(msExceptionInterval);
            }
        }
    }
}
