using Durandal.Common.Ontology;
using Durandal.Common.Statistics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Dialog.Services
{
    public interface IEntityHistory
    {
        /// <summary>
        /// Inserts an entity into the global conversation context to make it available to everyone.
        /// </summary>
        /// <param name="entity"></param>
        void AddOrUpdateEntity(Entity entity);

        /// <summary>
        /// Attempts to find an entity in the context specified by ID, and returns it if found,
        /// Otherwise, return null. The entity will also attempt to be type casted to the requested type;
        /// if the actual entity does not implement that type, it will also become null.
        /// </summary>
        /// <typeparam name="T">The type of entity that is expected, or just "Entity" for any entity.</typeparam>
        /// <param name="entityId">The ID of the entity to retrieve</param>
        /// <returns></returns>
        T GetEntityById<T>(string entityId) where T : Entity;

        /// <summary>
        /// Returns the number of epochs since the specified entity has been touched (written) in the history.
        /// 0 = current turn, 1 = 1 turn ago, etc.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        int? GetEntityAge(Entity entity);

        /// <summary>
        /// Attempts to search for entities of a specific type, returning a list of n-best hypotheses
        /// as determined by a ranker of some sort. Typically, the most recently updated entities will appear
        /// with highest confidence.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="nbest">The minimum number of entities to retrieve</param>
        /// <returns>A list of entity hypotheses</returns>
        IList<Hypothesis<T>> FindEntities<T>(int nbest = 1) where T : Entity;
    }
}
