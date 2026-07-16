namespace VRBuilder.UI.Console
{
    /// <summary>
    /// Severity of a message logged in an <see cref="ILogConsole"/>. Mirrors the engine log
    /// levels without depending on UnityEngine, keeping the console contract pure C#.
    /// </summary>
    public enum LogSeverity
    {
        Log,
        Warning,
        Error,
        Exception,
        Assert
    }
}
