using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal class SphinxLogger
    {
        private ILogger _logger = NullLogger.Singleton;

        internal SphinxLogger(ILogger logger)
        {
            _logger = logger;
        }

        internal void E_ERROR(string message)
        {
            _logger.Log(message.TrimEnd('\n'), LogLevel.Err);
        }

        internal void E_WARN(string message)
        {
            _logger.Log(message.TrimEnd('\n'), LogLevel.Wrn);
        }

        internal void E_INFO(string message)
        {
            _logger.Log(message.TrimEnd('\n'), LogLevel.Vrb);
        }

        internal void E_INFO_NOFN(string message)
        {
            _logger.Log(message.TrimEnd('\n'), LogLevel.Vrb);
        }

        internal void E_DEBUG(string message)
        {
            _logger.Log(message.TrimEnd('\n'), LogLevel.Vrb);
        }

        internal void E_INFOCONT(string message)
        {
            _logger.Log(message.TrimEnd('\n'), LogLevel.Vrb);
        }

        internal void E_FATAL(string message)
        {
            _logger.Log(message.TrimEnd('\n'), LogLevel.Err);
            throw new Exception("Fatal program error: " + message);
        }

        internal void E_ERROR_SYSTEM(string message)
        {
            _logger.Log(message.TrimEnd('\n'), LogLevel.Err);
        }

        internal void E_FATAL_SYSTEM(string message)
        {
            _logger.Log(message.TrimEnd('\n'), LogLevel.Err);
            throw new Exception("Fatal program error: " + message);
        }
    }
}
