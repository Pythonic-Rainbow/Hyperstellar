using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Hyperstellar;

public static class Program
{
    private static void DefaultTryForeverExceptionFunc(Exception ex)
    {
        ex.Demystify();
        Console.WriteLine(ex);
    }

    internal static string GetExceptionStackTraceString(Exception ex)
    {
        string msg = "";

        EnhancedStackTrace stackTrace = new(ex);
        for (int i = 0; i < stackTrace.FrameCount; i++)
        {
            StackFrame frame = stackTrace.GetFrame(i);


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


            string frameMsg = $"at {methodMsg}() in {fileNameMsg}:{lineNumberMsg}.{columnNumberMsg}";

            msg += $"{frameMsg}\n";
        }

        return msg;
    }

    internal static async Task<T> TryUntilAsync<T>(Func<Task<T>> tryFunc, Func<Exception, Task>? repeatFunc = null,
        int msRetryWaitInterval = 0)
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

                await Task.Delay(msRetryWaitInterval);
            }
        }
    }

    internal static async Task TryUntilAsync(Func<Task> tryFunc, Func<Exception, Task>? repeatFunc = null,
        bool runForever = false)
    {
        while (true)
        {
            try
            {
                await tryFunc();
                if (!runForever)
                {
                    return;
                }
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

    public static async Task Main()
    {
        List<Task> tasks = [Discord.Dc.InitAsync(), Clash.Coc.InitAsync(), Sql.Db.InitAsync()];
        while (tasks.Count != 0)
        {
            Task completedTask = await Task.WhenAny(tasks);
            if (completedTask.IsCompletedSuccessfully)
            {
                tasks.Remove(completedTask);
            }
            else
            {
                ExceptionDispatchInfo.Capture(completedTask.Exception!.InnerException!).Throw();
            }
        }
    }
}
