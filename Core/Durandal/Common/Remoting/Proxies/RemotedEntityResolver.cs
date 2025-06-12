using Durandal.Common.Dialog.Services;
using System;
using System.Collections.Generic;
using System.Text;
using Durandal.API;
using Durandal.Common.Statistics;
using Durandal.Common.Logger;
using System.Threading.Tasks;
using Durandal.Common.Time;
using Durandal.Common.NLP;
using Durandal.Common.NLP.Language;
using System.Threading;

namespace Durandal.Common.Remoting.Proxies
{
    public class RemotedEntityResolver : IEntityResolver
    {
        private readonly RemoteDialogMethodDispatcher _dispatcher;

        public RemotedEntityResolver(RemoteDialogMethodDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public Task<IList<Hypothesis<T>>> ResolveEntity<T>(LexicalString input, IList<NamedEntity<T>> possibleValues, LanguageCode locale, ILogger traceLogger)
        {
            return _dispatcher.Utility_ResolveEntity(input, possibleValues, locale, traceLogger, DefaultRealTimeProvider.Singleton, CancellationToken.None);
        }
    }
}
