using System.Diagnostics;
using System.Reflection;

namespace Hyperstellar;

public static class Program
{
    private static void DefaultTryForeverExceptionFunc(Exception ex) => Console.Error.WriteLine(GetExceptionStackTraceString(ex));

    internal static string GetExceptionStackTraceString(Exception ex)
    {
        string msg = "";
        StackTrace stackTrace = new(ex, true);

        for (int i = 0; i < stackTrace.FrameCount; i++)
        {
            string frameMsg = "<Unknown frame>";
            StackFrame? frame = stackTrace.GetFrame(i);
            if (frame != null)
            {

                string methodMsg = "???::??";
                MethodBase? method = frame.GetMethod();
                if (method != null)
                {

                    string typeMsg = "<Unknown type>";
                    Type? type = method.DeclaringType;
                    if (type != null)
                    {
                        typeMsg = type.FullName ?? $"???.{type.Name}";
                    }

                    methodMsg = typeMsg + "::" + method.Name;
                }

                string fileNameMsg = $"{frame.GetFileName()?.Split("/").Last() ?? "??.cs"}";
                string lineNumberMsg = $"{frame.GetFileLineNumber()}";
                string columnNumberMsg = $"{frame.GetFileColumnNumber()}";


                frameMsg = $"at {methodMsg}() in {fileNameMsg}:{lineNumberMsg}.{columnNumberMsg}";
            }
            msg += $"{frameMsg}\n";
        }

        return msg;
    }

    internal static async Task<T> TryForeverAsync<T>(Func<Task<T>> tryFunc, Func<Exception, Task>? repeatFunc = null)
    {
        while (true)
        {
            try
            {
                return await tryFunc();
            }
            catch (Exception ex)
            {
                if (repeatFunc != null)
                {
                    await repeatFunc(ex);
                }
                else
                {
                    DefaultTryForeverExceptionFunc(ex);
                }
            }
        }
    }

    internal static async Task TryForeverAsync(Func<Task> tryFunc, Func<Exception, Task>? repeatFunc = null)
    {
        while (true)
        {
            try
            {
                await tryFunc();
                return;
            }
            catch (Exception ex)
            {
                if (repeatFunc != null)
                {
                    await repeatFunc(ex);
                }
                else
                {
                    DefaultTryForeverExceptionFunc(ex);
                }
            }
        }
    }

    public static async Task Main() => await Task.WhenAll(Discord.Dc.InitAsync(), Clash.Coc.InitAsync(), Sql.Db.InitAsync());
}
