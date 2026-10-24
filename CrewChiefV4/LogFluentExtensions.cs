using System;

namespace CrewChiefV4
{
    /// <summary>
    /// Extension methods to allow fluent logging like: Log.DontSpam("...").Warning();
    /// Works for string and for arbitrary objects (falls back to ToString()).
    /// Also supports calling Exception with an Exception instance: Log.DontSpam("...").Exception(ex);
    /// </summary>
    public static class LogFluentExtensions
    {
        public static void Warning(this string message)
        {
            try { Log.Warning(message); } catch { }
        }

        public static void Error(this string message)
        {
            try { Log.Error(message); } catch { }
        }

        public static void Verbose(this string message)
        {
            try { Log.Verbose(message); } catch { }
        }

        public static void Commentary(this string message)
        {
            try { Log.Commentary(message); } catch { }
        }

        public static void Exception(this string message, Exception ex)
        {
            try { Log.Exception(ex, message); } catch { }
        }

        // Fallback overloads accepting object so callers where DontSpam returns a non-string type also work
        public static void Warning(this object obj)
        {
            try { Log.Warning(obj?.ToString()); } catch { }
        }

        public static void Error(this object obj)
        {
            try { Log.Error(obj?.ToString()); } catch { }
        }

        public static void Verbose(this object obj)
        {
            try { Log.Verbose(obj?.ToString()); } catch { }
        }

        public static void Commentary(this object obj)
        {
            try { Log.Commentary(obj?.ToString()); } catch { }
        }

        public static void Exception(this object obj, Exception ex)
        {
            try { Log.Exception(ex, obj?.ToString()); } catch { }
        }
    }
}
