using System;
using System.Collections.Generic;
using System.Text;
using Durandal.Common.Ontology;
using Durandal.Common.Logger;
using Durandal.Common.Statistics;

namespace Durandal.Common.Dialog.Services
{
    public class ReadOnlyEntityHistory : IEntityHistory
    {
        private IEntityHistory _wrapped;
        private ILogger _logger;

        public ReadOnlyEntityHistory(IEntityHistory wrapped, ILogger logger)
        {
            _wrapped = wrapped;
            _logger = logger;
        }

        public void AddOrUpdateEntity(Entity entity)
        {
            _logger.Log("Attempting to add entities to a read-only entity history", LogLevel.Wrn);
        }

        public IList<Hypothesis<T>> FindEntities<T>(int nbest = 1) where T : Entity
        {
            return _wrapped.FindEntities<T>(nbest);
        }

        public int? GetEntityAge(Entity entity)
        {
            return _wrapped.GetEntityAge(entity);
        }

        public T GetEntityById<T>(string entityId) where T : Entity
        {
            return _wrapped.GetEntityById<T>(entityId);
        }
    }
}
