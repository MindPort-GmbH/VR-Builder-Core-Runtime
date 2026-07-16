namespace VRBuilder.UI.Console
{
    /// <summary>
    /// A console for logging debug messages.
    /// </summary>
    public interface ILogConsole
    {
        /// <summary>
        /// True if the console is currently visible.
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// Add the provided message to the log.
        /// </summary>
        /// <param name="message">Main message.</param>
        /// <param name="details">Extra details, e.g. stack trace.</param>
        /// <param name="severity">Severity of the message logged.</param>
        void LogMessage(string message, string details, LogSeverity severity);

        /// <summary>
        /// Clears the console of all messages.
        /// </summary>
        void Clear();

        /// <summary>
        /// Makes the console visible.
        /// </summary>
        void Show();

        /// <summary>
        /// Hides the console.
        /// </summary>
        void Hide();

        /// <summary>
        /// Toggles the console between visible and hidden.
        /// </summary>
        void Toggle();

        /// <summary>
        /// Manually sets the console dirty, so it knows it has to be refreshed.
        /// </summary>
        void SetDirty();
    }
}
