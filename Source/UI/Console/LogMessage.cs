namespace VRBuilder.UI.Console
{
    /// <summary>
    /// A message logged in an <see cref="ILogConsole"/>.
    /// </summary>
    public struct LogMessage
    {
        /// <summary>
        /// The main message.
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Additional information provided in the message.
        /// </summary>
        public string Details { get; private set; }

        /// <summary>
        /// Severity of the message logged.
        /// </summary>
        public LogSeverity Severity { get; private set; }

        public LogMessage(string message, string details, LogSeverity severity)
        {
            Message = message;
            Details = details;
            Severity = severity;
        }
    }
}
