using System.Diagnostics;

namespace inRiver.Connectors.EPiServer.Helpers
{
    public static class StopwatchExtentions
    {
        internal static string GetElapsedTimeFormated(this Stopwatch stopwatch)
        {
            if (stopwatch.ElapsedMilliseconds < 1000)
            {
                return string.Format("{0} ms", stopwatch.ElapsedMilliseconds);
            }

            return stopwatch.Elapsed.ToString("hh\\:mm\\:ss");
        }
    }
}
