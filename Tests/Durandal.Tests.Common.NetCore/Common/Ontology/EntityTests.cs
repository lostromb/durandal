using Durandal.Common.Dialog.Services;
using Durandal.Common.IO;
using Durandal.Common.Ontology;
using Durandal.Common.Statistics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using Durandal.Tests.EntitySchemas;

namespace Durandal.Tests.Common.Ontology
{
    [TestClass]
    public class EntityTests
    {
        [TestMethod]
        public void TestEntityHistoryBasicStore()
        {
            KnowledgeContext context = new KnowledgeContext();
            Person person = new Person(context);
            person.Name.Value = "Mr. Wazanski";
            person.BirthDate.Value = new Durandal.Common.Ontology.DateTimeEntity()
            {
                Year = 1955
            };
            person.Url.Value = "http://griffin-space-jam.com";

            InMemoryEntityHistory history = new InMemoryEntityHistory();
            history.AddOrUpdateEntity(person);
            IList<Hypothesis<Person>> people = history.FindEntities<Person>();
            Assert.AreEqual(1, people.Count);
            person = people[0].Value;
            Assert.AreEqual("Mr. Wazanski", person.Name.Value);
            Assert.AreEqual(1955, person.BirthDate.Value.Year.Value);
            Assert.AreEqual("http://griffin-space-jam.com", person.Url.Value);
        }

        [TestMethod]
        public void TestEntityHistoryBasicStoreGeneric()
        {
            KnowledgeContext context = new KnowledgeContext();
            Person person = new Person(context, "bing://myentity");
            person.Name.Value = "Mr. Wazanski";
            person.BirthDate.Value = new Durandal.Common.Ontology.DateTimeEntity()
            {
                Year = 1955
            };
            person.Url.Value = "http://griffin-space-jam.com";

            InMemoryEntityHistory history = new InMemoryEntityHistory();
            history.AddOrUpdateEntity(person);
            IList<Hypothesis<Entity>> entities = history.FindEntities<Entity>();
            Assert.AreEqual(1, entities.Count);
            Entity entity = entities[0].Value;
            Assert.AreEqual("bing://myentity", entity.EntityId);
        }

        [TestMethod]
        public void TestEntityHistoryRetrieveAfterTurns()
        {
            KnowledgeContext context = new KnowledgeContext();
            Person person = new Person(context);
            person.Name.Value = "Mr. Wazanski";
            person.BirthDate.Value = new Durandal.Common.Ontology.DateTimeEntity()
            {
                Year = 1955
            };
            person.Url.Value = "http://griffin-space-jam.com";

            InMemoryEntityHistory history = new InMemoryEntityHistory(10);
            history.AddOrUpdateEntity(person);

            for (int c = 0; c < 5; c++)
            {
                InvokeTurn(history);
            }

            IList<Hypothesis<Person>> people = history.FindEntities<Person>();
            Assert.AreEqual(1, people.Count);
            person = people[0].Value;
            Assert.AreEqual("Mr. Wazanski", person.Name.Value);
            Assert.AreEqual(1955, person.BirthDate.Value.Year.Value);
            Assert.AreEqual("http://griffin-space-jam.com", person.Url.Value);
        }

        [TestMethod]
        public void TestEntityHistoryExpiresAfterTurns()
        {
            KnowledgeContext context = new KnowledgeContext();
            Person person = new Person(context);
            person.Name.Value = "Mr. Wazanski";
            person.BirthDate.Value = new Durandal.Common.Ontology.DateTimeEntity()
            {
                Year = 1955
            };
            person.Url.Value = "http://griffin-space-jam.com";

            InMemoryEntityHistory history = new InMemoryEntityHistory(10);
            history.AddOrUpdateEntity(person);

            for (int c = 0; c < 25; c++)
            {
                InvokeTurn(history);
            }
            
            Assert.AreEqual(0, history.Count);
        }

        [TestMethod]
        public void TestEntityHistoryEmptySerialization()
        {
            InMemoryEntityHistory history = new InMemoryEntityHistory(10);
            using (PooledBuffer<byte> serialized = history.Serialize())
            {
                history = InMemoryEntityHistory.Deserialize(serialized.Buffer, 0, serialized.Length, 10);
                Assert.IsNotNull(history);
                Assert.AreEqual(0, history.Count);
            }
        }

        [TestMethod]
        public void TestEntityHistorySerialization()
        {
            KnowledgeContext context = new KnowledgeContext();
            Person person = new Person(context);
            person.Name.Value = "Mr. Wazanski";
            person.BirthDate.Value = new Durandal.Common.Ontology.DateTimeEntity()
            {
                Year = 1955
            };
            person.Url.Value = "http://griffin-space-jam.com";

            InMemoryEntityHistory history = new InMemoryEntityHistory(10);
            history.AddOrUpdateEntity(person);
            using (PooledBuffer<byte> serialized = history.Serialize())
            {
                history = InMemoryEntityHistory.Deserialize(serialized.Buffer, 0, serialized.Length, 10);
                IList<Hypothesis<Person>> people = history.FindEntities<Person>();
                Assert.AreEqual(1, people.Count);
                person = people[0].Value;
                Assert.AreEqual("Mr. Wazanski", person.Name.Value);
                Assert.AreEqual(1955, person.BirthDate.Value.Year.Value);
                Assert.AreEqual("http://griffin-space-jam.com", person.Url.Value);
            }
        }

        [TestMethod]
        public void TestEntityHistoryDoesntExpireIfTouched()
        {
            KnowledgeContext context = new KnowledgeContext();
            Person person = new Person(context);
            person.Name.Value = "Mr. Wazanski";
            person.BirthDate.Value = new Durandal.Common.Ontology.DateTimeEntity()
            {
                Year = 1955
            };
            person.Url.Value = "http://griffin-space-jam.com";

            InMemoryEntityHistory history = new InMemoryEntityHistory(10);
            history.AddOrUpdateEntity(person);

            for (int c = 0; c < 25; c++)
            {
                InvokeTurn(history);
                history.AddOrUpdateEntity(person);
                Assert.AreEqual(1, history.Count);
            }
            
            IList<Hypothesis<Person>> people = history.FindEntities<Person>();
            Assert.AreEqual(1, people.Count);
            person = people[0].Value;
            Assert.AreEqual("Mr. Wazanski", person.Name.Value);
            Assert.AreEqual(1955, person.BirthDate.Value.Year.Value);
            Assert.AreEqual("http://griffin-space-jam.com", person.Url.Value);
        }

        [TestMethod]
        public void TestEntityHistoryPropertiesGetAdded()
        {
            KnowledgeContext context = new KnowledgeContext();
            Person person = new Person(context);
            person.Name.Value = "Mr. Wazanski";

            InMemoryEntityHistory history = new InMemoryEntityHistory();
            history.AddOrUpdateEntity(person);

            // Modify the entity and call AddOrUpdate again
            person.Url.Value = "http://griffin-space-jam.com";
            history.AddOrUpdateEntity(person);

            // Assert that the changes we made to the entity are persisted
            IList<Hypothesis<Person>> people = history.FindEntities<Person>();
            Assert.AreEqual(1, people.Count);
            person = people[0].Value;
            Assert.AreEqual("Mr. Wazanski", person.Name.Value);
            Assert.AreEqual("http://griffin-space-jam.com", person.Url.Value);
        }

        [TestMethod]
        public void TestEntityHistoryPropertiesGetRemoved()
        {
            KnowledgeContext context = new KnowledgeContext();
            Person person = new Person(context);
            person.Name.Value = "Mr. Wazanski";
            person.Url.Value = "http://griffin-space-jam.com";

            InMemoryEntityHistory history = new InMemoryEntityHistory();
            history.AddOrUpdateEntity(person);

            // Modify the entity and call AddOrUpdate again
            person.Url.Value = null;
            history.AddOrUpdateEntity(person);

            // Assert that the changes we made to the entity are persisted
            IList<Hypothesis<Person>> people = history.FindEntities<Person>();
            Assert.AreEqual(1, people.Count);
            person = people[0].Value;
            Assert.AreEqual("Mr. Wazanski", person.Name.Value);
            Assert.AreEqual(null, person.Url.Value);
        }

        private static void InvokeTurn(InMemoryEntityHistory entityHistory)
        {
            entityHistory.GetType().GetMethod("Turn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(entityHistory, null);
        }
    }
}
