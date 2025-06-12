namespace Stromberg.Logger
{
    using System;

    public class ConsoleLogger : ILogger
    {
        public const LogLevel DEFAULT_LOG_LEVELS = LogLevel.Std | LogLevel.Err | LogLevel.Wrn | LogLevel.Ins;

        private ConsoleLoggerCore _loggerImpl;

        private string _thisComponent;
        private string _thisTraceId;

        public ConsoleLogger(string componentName = "Main", LogLevel logLevels = DEFAULT_LOG_LEVELS, bool keepHistory = false)
        {
            this._loggerImpl = new ConsoleLoggerCore(logLevels, keepHistory);
            this._thisComponent = componentName ?? "Main";
            this._thisTraceId = null;
            this.Log("Console logger initialized");
        }

        /// <summary>
        /// Private constructor for creating inherited logger objects
        /// </summary>
        /// <param name="componentName"></param>
        /// <param name="stream"></param>
        private ConsoleLogger(string componentName, string traceId, ConsoleLoggerCore core)
        {
            this._thisComponent = componentName;
            this._loggerImpl = core;
            this._thisTraceId = traceId;
        }

        public ILogger Clone(string newComponentName, string traceId = null)
        {
            if (newComponentName != null && !newComponentName.Equals(this._thisComponent) ||
                (this._thisTraceId == null && traceId != null) || (this._thisTraceId != null && traceId == null) ||
                (this._thisTraceId != null && traceId != null && this._thisTraceId.Equals(traceId)))
            {
                return new ConsoleLogger(newComponentName, traceId, this._loggerImpl);
            }
            return this;
        }

        public void SuppressAllOutput()
        {
            this._loggerImpl.SuppressAllOutput();
        }

        public void UnsuppressAllOutput()
        {
            this._loggerImpl.UnsuppressAllOutput();
        }

        public string GetComponentName()
        {
            return _thisComponent;
        }

        public string GetTraceId()
        {
            return _thisTraceId;
        }

        public void Log(object value, LogLevel level = LogLevel.Std, string traceId = null)
        {
            if (value == null)
                this.Log("null", level, traceId);
            else
                this.Log(value.ToString(), level, traceId);
        }

        public void Log(string value, LogLevel level = LogLevel.Std, string traceId = null)
        {
            // Convert it into an event
            LogEvent newEvent = new LogEvent(this._thisComponent, value ?? "null", level, DateTime.Now, traceId ?? _thisTraceId);
            this.Log(newEvent);
        }

        public void Log(LogEvent value)
        {
            this._loggerImpl.Log(value);
        }

        public LoggingHistory GetHistory()
        {
            return this._loggerImpl.GetHistory();
        }

        public event EventHandler<LogUpdatedEventArgs> LogUpdated
        {
            add { this._loggerImpl.LogUpdated += value; }
            remove { this._loggerImpl.LogUpdated -= value; }
        }
        
        /// <summary>
        /// This is the context object shared between all clones of the console logger
        /// </summary>
        private class ConsoleLoggerCore
        {
            private LogLevel _levelsToLog = DEFAULT_LOG_LEVELS;
            private LoggingHistory _history = null;
            private volatile bool _suppressed = false;
            private LogEventEmitter _eventEmitter;
            
            public ConsoleLoggerCore(LogLevel levels, bool keepHistory)
            {
                this._levelsToLog = levels;
                if (keepHistory)
                {
                    this._history = new LoggingHistory();
                }
                this._eventEmitter = new LogEventEmitter();
            }

            public void SuppressAllOutput()
            {
                this._suppressed = true;
            }

            public void UnsuppressAllOutput()
            {
                this._suppressed = false;
            }

            public LoggingHistory GetHistory()
            {
                return this._history;
            }

            public void Log(LogEvent value)
            {
                if (!this._suppressed)
                {
                    if (this._levelsToLog.HasFlag(value.Level))
                    {
                        switch (value.Level)
                        {
                            case LogLevel.Err:
                                Console.ForegroundColor = ConsoleColor.Red;
                                break;
                            case LogLevel.Wrn:
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                break;
                            case LogLevel.Ins:
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                break;
                        }
                        Console.WriteLine(value.ToShortString());
                        Console.ResetColor();
                        if (this._history != null)
                            this._history.Add(value);
                        this.OnLogUpdated(value);
                    }
                }
            }

            public event EventHandler<LogUpdatedEventArgs> LogUpdated
            {
                add { this._eventEmitter.Add(value); }
                remove { this._eventEmitter.Remove(value); }
            }

            private void OnLogUpdated(LogEvent e)
            {
                LogUpdatedEventArgs args = new LogUpdatedEventArgs(e);
                if (this._eventEmitter != null)
                {
                    this._eventEmitter.Fire(this, args);
                }
            }
        }
    }
}
