using Durandal.Common.Ontology;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Tests.Common.Ontology;
using Durandal.Tests.TestSchemas;

namespace Durandal.Tests.Common.Ontology
{
    [TestClass]
    public class OntologyTests
    {
        [TestMethod]
        public async Task TestOntologyEntityCreate()
        {
            KnowledgeContext context = new KnowledgeContext();
            Person entity = new Person(context);
            entity.BirthDate.Value = new Durandal.Common.Ontology.DateTimeEntity()
            {
                Year = 1987,
                Month = 12,
                DayOfMonth = 11
            };
            entity.Name.Value = "Malcolm";
            entity.Description.Value = "Cool dude";
            QuantitativeValue height = new QuantitativeValue(context);
            height.Value_as_number.Value = 60M;
            entity.Height_as_QuantitativeValue.SetValue(height);

            Assert.AreEqual("Malcolm", entity.Name.Value);
            Assert.AreEqual("Cool dude", entity.Description.Value);
            Assert.AreEqual(60M, (await entity.Height_as_QuantitativeValue.GetValue()).Value_as_number.Value);
        }

        [TestMethod]
        public void TestOntologyEntityListOfStrings()
        {
            KnowledgeContext context = new KnowledgeContext();
            Person entity = new Person(context);
            entity.Description.Add("One");
            entity.Description.Add("Two");
            entity.Description.Add("Three");
            List<string> items = entity.Description.List.ToList();
            Assert.AreEqual(3, items.Count);
            Assert.IsTrue(items.Contains("One"));
            Assert.IsTrue(items.Contains("Two"));
            Assert.IsTrue(items.Contains("Three"));
            Assert.IsFalse(string.IsNullOrEmpty(entity.Description.Value));
        }

        [TestMethod]
        public void TestOntologyEntityListOfNumbers()
        {
            KnowledgeContext context = new KnowledgeContext();
            QuantitativeValue entity = new QuantitativeValue(context);
            entity.Value_as_number.Add(1M);
            entity.Value_as_number.Add(2M);
            entity.Value_as_number.Add(3M);
            List<decimal> items = entity.Value_as_number.List.ToList();
            Assert.AreEqual(3, items.Count);
            Assert.IsTrue(items.Contains(1M));
            Assert.IsTrue(items.Contains(2M));
            Assert.IsTrue(items.Contains(3M));
            Assert.IsTrue(entity.Value_as_number.Value.HasValue);
        }

        [TestMethod]
        public async Task TestOntologyEntityListOfEntities()
        {
            KnowledgeContext context = new KnowledgeContext();
            QuantitativeValue entity = new QuantitativeValue(context);
            StructuredValue val = new StructuredValue(context);
            val.Name.Value = "One";
            entity.Value_as_StructuredValue.Add(val);
            val = new StructuredValue(context);
            val.Name.Value = "Two";
            entity.Value_as_StructuredValue.Add(val);
            val = new StructuredValue(context);
            val.Name.Value = "Three";
            entity.Value_as_StructuredValue.Add(val);
            List<StructuredValue> items = (await entity.Value_as_StructuredValue.List()).ToList();
            Assert.AreEqual(3, items.Count);
            Assert.IsTrue(items.Any((i) => i.Name.Value == "One"));
            Assert.IsTrue(items.Any((i) => i.Name.Value == "Two"));
            Assert.IsTrue(items.Any((i) => i.Name.Value == "Three"));
        }

        [TestMethod]
        public void TestOntologyUnionTypeIsolationOnAdd()
        {
            KnowledgeContext context = new KnowledgeContext();
            QuantitativeValue entity = new QuantitativeValue(context);
            entity.Value_as_bool.Add(true);
            Assert.AreEqual(1, entity.Value_as_bool.List.ToList().Count);
            Assert.AreEqual(true, entity.Value_as_bool.Value);
            entity.Value_as_number.Add(10M);
            Assert.AreEqual(1, entity.Value_as_number.List.ToList().Count);
            Assert.AreEqual(1, entity.Value_as_bool.List.ToList().Count);
            Assert.AreEqual(10M, entity.Value_as_number.Value);
            entity.Value_as_string.Add("Million");
            Assert.AreEqual(1, entity.Value_as_string.List.ToList().Count);
            Assert.AreEqual(1, entity.Value_as_number.List.ToList().Count);
            Assert.AreEqual(1, entity.Value_as_bool.List.ToList().Count);
            Assert.AreEqual("Million", entity.Value_as_string.Value);
        }

        [TestMethod]
        public void TestOntologyUnionTypeIsolationOnSet()
        {
            KnowledgeContext context = new KnowledgeContext();
            QuantitativeValue entity = new QuantitativeValue(context);
            entity.Value_as_bool.Value = true;
            Assert.AreEqual(1, entity.Value_as_bool.List.ToList().Count);
            Assert.AreEqual(true, entity.Value_as_bool.Value);
            entity.Value_as_number.Value = 10M;
            Assert.AreEqual(1, entity.Value_as_number.List.ToList().Count);
            Assert.AreEqual(0, entity.Value_as_bool.List.ToList().Count);
            Assert.AreEqual(10M, entity.Value_as_number.Value);
            entity.Value_as_string.Value = "Million";
            Assert.AreEqual(1, entity.Value_as_string.List.ToList().Count);
            Assert.AreEqual(0, entity.Value_as_number.List.ToList().Count);
            Assert.AreEqual(0, entity.Value_as_bool.List.ToList().Count);
            Assert.AreEqual("Million", entity.Value_as_string.Value);
        }

        [TestMethod]
        public void TestOntologyUnionTypeEntityFieldFiltersProperly()
        {
            KnowledgeContext context = new KnowledgeContext();
            Place entity = new Place(context);
            GeoCoordinates coords = new GeoCoordinates(context);
            coords.Latitude_as_number.Value = 10;
            entity.Geo_as_GeoCoordinates.Add(coords);

            GeoShape shape = new GeoShape(context);
            shape.Circle.Value = "a circle";
            entity.Geo_as_GeoShape.Add(shape);

            Assert.AreEqual(1, entity.Geo_as_GeoCoordinates.ListInMemory().Count);
            Assert.AreEqual(1, entity.Geo_as_GeoShape.ListInMemory().Count);

            coords = entity.Geo_as_GeoCoordinates.ValueInMemory;
            Assert.IsNotNull(coords);
            Assert.AreEqual(10, coords.Latitude_as_number.Value);
            shape = entity.Geo_as_GeoShape.ValueInMemory;
            Assert.IsNotNull(shape);
            Assert.AreEqual("a circle", shape.Circle.Value);
        }

        [TestMethod]
        public async Task TestOntologyEntityValuesAfterSerialization()
        {
            KnowledgeContext context = new KnowledgeContext();
            Person entity = new Person(context);
            entity.BirthDate.Value = new Durandal.Common.Ontology.DateTimeEntity()
            {
                Year = 1987,
                Month = 12,
                DayOfMonth = 11,
                Hour = 3,
                Minute = 22,
                Second = 55
            };
            entity.Name.Value = "Malcolm";
            entity.Description.Value = "Cool dude";
            QuantitativeValue height = new QuantitativeValue(context);
            height.Value_as_number.Value = 60M;
            entity.Height_as_QuantitativeValue.SetValue(height);
            QuantitativeValue weight = new QuantitativeValue(context);
            weight.Value_as_bool.Value = true;
            entity.Weight.SetValue(weight);
            entity.Gender_as_GenderType.SetValue(new Male(context));

            byte[] payload;
            using (MemoryStream writeStream = new MemoryStream())
            {
                context.Serialize(writeStream, false);
                payload = writeStream.ToArray();
            }

            KnowledgeContext newContext;
            using (MemoryStream readStream = new MemoryStream(payload, false))
            {
                newContext = KnowledgeContext.Deserialize(readStream, false);
            }

            Person parsedEntity = await newContext.GetEntity<Person>(entity.EntityId);
            Assert.AreEqual("Malcolm", parsedEntity.Name.Value);
            Assert.AreEqual("Cool dude", parsedEntity.Description.Value);
            Assert.AreEqual(60M, (await parsedEntity.Height_as_QuantitativeValue.GetValue()).Value_as_number.Value);
            Assert.AreEqual(true, (await parsedEntity.Weight.GetValue()).Value_as_bool.Value.GetValueOrDefault(false));
            Assert.IsTrue((await parsedEntity.Gender_as_GenderType.GetValue()).IsA<Male>());
            Assert.AreEqual(1987, parsedEntity.BirthDate.Value.Year.GetValueOrDefault());
            Assert.AreEqual(12, parsedEntity.BirthDate.Value.Month.GetValueOrDefault());
            Assert.AreEqual(11, parsedEntity.BirthDate.Value.DayOfMonth.GetValueOrDefault());
            Assert.AreEqual(3, parsedEntity.BirthDate.Value.Hour.GetValueOrDefault());
            Assert.AreEqual(22, parsedEntity.BirthDate.Value.Minute.GetValueOrDefault());
            Assert.AreEqual(55, parsedEntity.BirthDate.Value.Second.GetValueOrDefault());
            Assert.IsTrue(parsedEntity.IsA<Thing>());
        }

        [TestMethod]
        public void TestOntologyExtraPropertyEntityIds()
        {
            KnowledgeContext context = new KnowledgeContext();
            Person entity = new Person(context);
            entity.Name.Value = "Quote";
            QuantitativeValue height = new QuantitativeValue(context);
            height.Value_as_number.Value = 60M;
            entity.Height_as_QuantitativeValue.SetValue(height);

            Distance height2 = new Distance(context);
            height2.Description.Value = "the distance";
            entity.Height_as_Distance.Add(height2);
            QuantitativeValue weight = new QuantitativeValue(context);
            weight.Value_as_bool.Value = true;
            entity.Weight.SetValue(weight);
            entity.Gender_as_GenderType.SetValue(new Male(context));

            Assert.AreEqual(2, entity.ExtraPropertyEntityIds("height").Count());
            Assert.AreEqual(1, entity.ExtraPropertyEntityIds("weight").Count());
            Assert.AreEqual(1, entity.ExtraPropertyEntityIds("gender").Count());
            Assert.AreEqual(0, entity.ExtraPropertyEntityIds("name").Count());

            Console.WriteLine(entity.ToDebugJson());
        }

        [TestMethod]
        public async Task TestOntologyIsA()
        {
            KnowledgeContext context = new KnowledgeContext();
            City entity = new City(context);
            entity.Name.Value = "Seattle";

            byte[] payload;
            using (MemoryStream writeStream = new MemoryStream())
            {
                context.Serialize(writeStream, false);
                payload = writeStream.ToArray();
            }

            KnowledgeContext newContext;
            using (MemoryStream readStream = new MemoryStream(payload, false))
            {
                newContext = KnowledgeContext.Deserialize(readStream, false);
            }

            City parsedCity = await newContext.GetEntity<City>(entity.EntityId);
            Assert.AreEqual("Seattle", parsedCity.Name.Value);
            Assert.IsTrue(parsedCity.IsA<Thing>());
            Assert.IsTrue(parsedCity.IsA<Place>());
            Assert.IsTrue(parsedCity.IsA<City>());
            Assert.IsTrue(parsedCity.IsA<Entity>());

            Assert.IsTrue(parsedCity.IsA(typeof(Thing)));
            Assert.IsTrue(parsedCity.IsA(typeof(Place)));
            Assert.IsTrue(parsedCity.IsA(typeof(City)));
            Assert.IsTrue(parsedCity.IsA(typeof(Entity)));

            Place parsedPlace = await newContext.GetEntity<Place>(entity.EntityId);
            Assert.AreEqual("Seattle", parsedPlace.Name.Value);
            Assert.IsTrue(parsedPlace.IsA<Thing>());
            Assert.IsTrue(parsedPlace.IsA<Place>());
            Assert.IsTrue(parsedPlace.IsA<City>());
            Assert.IsTrue(parsedPlace.IsA<Entity>());

            Assert.IsTrue(parsedPlace.IsA(typeof(Thing)));
            Assert.IsTrue(parsedPlace.IsA(typeof(Place)));
            Assert.IsTrue(parsedPlace.IsA(typeof(City)));
            Assert.IsTrue(parsedPlace.IsA(typeof(Entity)));
        }

        [TestMethod]
        public void TestOntologySerializeEmptyContext()
        {
            KnowledgeContext context = new KnowledgeContext();
            Assert.IsTrue(context.IsEmpty);

            byte[] payload;
            using (MemoryStream writeStream = new MemoryStream())
            {
                context.Serialize(writeStream, false);
                payload = writeStream.ToArray();
            }

            KnowledgeContext newContext;
            using (MemoryStream readStream = new MemoryStream(payload, false))
            {
                newContext = KnowledgeContext.Deserialize(readStream, false);
            }

            Assert.IsNotNull(newContext);
        }

        [TestMethod]
        public void TestOntologyIsAnEnumerable()
        {
            KnowledgeContext context = new KnowledgeContext();
            Person person = new Person(context);
            person.Gender_as_GenderType.SetValue(new Male(context));
            GenderType gender = person.Gender_as_GenderType.ValueInMemory;
            Assert.IsTrue(gender.IsA<Entity>());
            Assert.IsTrue(gender.IsA<GenderType>());
            Assert.IsTrue(gender.IsA<Male>());
            Assert.IsFalse(gender.IsA<Female>());

            Assert.IsTrue(gender.IsA(typeof(Entity)));
            Assert.IsTrue(gender.IsA(typeof(GenderType)));
            Assert.IsTrue(gender.IsA(typeof(Male)));
            Assert.IsFalse(gender.IsA(typeof(Female)));
        }

        [TestMethod]
        public async Task TestOntologyCloudyEntityResolver()
        {
            PersonChainSource externalGraph = new PersonChainSource();
            KnowledgeContext context = new KnowledgeContext(new IEntitySource[] { externalGraph });
            Person person = new Person(context);
            person.Description.Value = person.EntityId;
            person.Parent.SetValue(new EntityReference<Person>("people://" + Guid.NewGuid().ToString()));
            // Now walk the parent graph a few times
            for (int c = 0; c < 10; c++)
            {
                Person parent = await person.Parent.GetValue();
                Assert.IsNotNull(parent);
                Assert.AreNotEqual(person.EntityId, parent.EntityId);
                Assert.AreEqual(person.EntityId, person.Description.Value);
                person = parent;
            }
        }

        public class PersonChainSource : IEntitySource
        {
            public string EntityIdScheme => "people";

            public async Task ResolveEntity(KnowledgeContext targetContext, string entityId)
            {
                // Add whatever requested person to the context, and create a link to their parent
                Person newPerson = new Person(targetContext, entityId);
                newPerson.Description.Value = entityId;
                newPerson.Parent.SetValue(new EntityReference<Person>("people://" + Guid.NewGuid().ToString()));
                await Task.Delay(0);
            }
        }

        [TestMethod]
        public async Task TestOntologyCopyTo()
        {
            KnowledgeContext sourceContext = new KnowledgeContext();
            Person sourcePerson = new Person(sourceContext);
            sourcePerson.Description.Value = "This is a person!";

            KnowledgeContext targetContext = new KnowledgeContext();
            sourcePerson.CopyTo(targetContext);

            Person targetPerson = await targetContext.GetEntity<Person>(sourcePerson.EntityId);
            Assert.IsNotNull(targetPerson);
            Assert.AreEqual("This is a person!", targetPerson.Description.Value);
        }

        [TestMethod]
        public async Task TestOntologyCopyToIsolation()
        {
            KnowledgeContext sourceContext = new KnowledgeContext();
            Person sourcePerson = new Person(sourceContext);
            sourcePerson.Description.Value = "This is a person!";

            KnowledgeContext targetContext = new KnowledgeContext();
            sourcePerson.CopyTo(targetContext);

            Person targetPerson = await targetContext.GetEntity<Person>(sourcePerson.EntityId);
            Assert.IsNotNull(targetPerson);
            Assert.AreEqual("This is a person!", targetPerson.Description.Value);

            sourcePerson.Telephone.Value = "1-800-fart";
            Assert.AreEqual("1-800-fart", sourcePerson.Telephone.Value);
            Assert.IsNull(targetPerson.Telephone.Value);

            targetPerson.Name.Value = "Wazanski";
            Assert.IsNull(sourcePerson.Name.Value);
            Assert.AreEqual("Wazanski", targetPerson.Name.Value);
        }

        [TestMethod]
        public async Task TestOntologyCopyToOverwriteAttribute()
        {
            KnowledgeContext sourceContext = new KnowledgeContext();
            Person sourcePerson = new Person(sourceContext);
            sourcePerson.Description.Value = "This is a person!";

            KnowledgeContext targetContext = new KnowledgeContext();
            sourcePerson.CopyTo(targetContext);

            sourcePerson.Description.Value = "Overwritten!";
            sourcePerson.CopyTo(targetContext);

            Person targetPerson = await targetContext.GetEntity<Person>(sourcePerson.EntityId);
            Assert.IsNotNull(targetPerson);
            Assert.AreEqual(1, targetPerson.Description.List.Count());
            Assert.AreEqual("Overwritten!", targetPerson.Description.Value);
        }

        [TestMethod]
        public async Task TestOntologyCopyToOverwriteAttributeRecursive()
        {
            KnowledgeContext sourceContext = new KnowledgeContext();
            Person sourcePerson = new Person(sourceContext);
            sourcePerson.Description.Value = "This is a person!";
            Person sourceParent = new Person(sourceContext);
            sourceParent.Description.Value = "This is a parent!";
            sourcePerson.Parent.SetValue(sourceParent);

            KnowledgeContext targetContext = new KnowledgeContext();
            sourcePerson.CopyTo(targetContext, true);

            sourceParent.Description.Value = "Overwritten!";
            sourcePerson.CopyTo(targetContext, true);

            Person targetParent = await targetContext.GetEntity<Person>(sourceParent.EntityId);
            Assert.IsNotNull(targetParent);
            Assert.AreEqual(1, targetParent.Description.List.Count());
            Assert.AreEqual("Overwritten!", targetParent.Description.Value);
        }

        [TestMethod]
        public async Task TestOntologyCopyToRemoveAttribute()
        {
            KnowledgeContext sourceContext = new KnowledgeContext();
            Person sourcePerson = new Person(sourceContext);
            sourcePerson.Description.Value = "This is a person!";

            KnowledgeContext targetContext = new KnowledgeContext();
            sourcePerson.CopyTo(targetContext);

            sourcePerson.Description.Value = null;
            sourcePerson.CopyTo(targetContext);

            Person targetPerson = await targetContext.GetEntity<Person>(sourcePerson.EntityId);
            Assert.IsNotNull(targetPerson);
            Assert.AreEqual(0, targetPerson.Description.List.Count());
            Assert.IsNull(targetPerson.Description.Value);
        }

        [TestMethod]
        public async Task TestOntologyCopyToRemoveAttributeRecursive()
        {
            KnowledgeContext sourceContext = new KnowledgeContext();
            Person sourcePerson = new Person(sourceContext);
            sourcePerson.Description.Value = "This is a person!";
            Person sourceParent = new Person(sourceContext);
            sourceParent.Description.Value = "This is a parent!";
            sourcePerson.Parent.SetValue(sourceParent);

            KnowledgeContext targetContext = new KnowledgeContext();
            sourcePerson.CopyTo(targetContext, true);

            sourceParent.Description.Value = null;
            sourcePerson.CopyTo(targetContext, true);

            Person targetParent = await targetContext.GetEntity<Person>(sourceParent.EntityId);
            Assert.IsNotNull(targetParent);
            Assert.AreEqual(0, targetParent.Description.List.Count());
            Assert.IsNull(targetParent.Description.Value);
        }

        [TestMethod]
        public async Task TestOntologyCastingBasic()
        {
            KnowledgeContext sourceContext = new KnowledgeContext();
            Person person = new Person(sourceContext);
            person.Description.Value = "This is a person!";

            Entity generic = await sourceContext.GetEntity(person.EntityId);
            Assert.IsTrue(generic.IsA<Person>());
            Person cast = generic.As<Person>();
            Assert.IsNotNull(cast);
            Assert.AreEqual(person.EntityId, cast.EntityId);
            Assert.AreEqual(person.Description.Value, cast.Description.Value);
        }

        [TestMethod]
        public async Task TestOntologyCastingToEntity()
        {
            KnowledgeContext sourceContext = new KnowledgeContext();
            Person person = new Person(sourceContext);
            person.Description.Value = "This is a person!";

            Entity generic = await sourceContext.GetEntity(person.EntityId);
            Assert.IsTrue(generic.IsA<Entity>());
            Entity cast = generic.As<Entity>();
            Assert.IsNotNull(cast);
            Assert.AreEqual(person.EntityId, cast.EntityId);
            Assert.AreEqual(person.Description.Value, cast.ExtraPropertyText("description").Value);
        }

        [TestMethod]
        public async Task TestOntologyCastingInherited()
        {
            KnowledgeContext sourceContext = new KnowledgeContext();
            Person person = new Person(sourceContext);
            person.Description.Value = "This is a person!";

            Entity generic = await sourceContext.GetEntity(person.EntityId);
            Assert.IsTrue(generic.IsA<Person>());
            Person cast = generic.As<Person>();
            Assert.IsNotNull(cast);
            Assert.AreEqual(person.EntityId, cast.EntityId);
            Assert.AreEqual(person.Description.Value, cast.Description.Value);
        }

        [TestMethod]
        public async Task TestOntologyIsAAfterDowncasting()
        {
            KnowledgeContext sourceContext = new KnowledgeContext();
            SportsActivityLocation obj = new SportsActivityLocation(sourceContext);
            obj.Description.Value = "This is a sports activity location!";

            Entity generic = await sourceContext.GetEntity(obj.EntityId);
            Assert.IsTrue(generic.IsA<Thing>());
            Assert.IsTrue(generic.IsA<LocalBusiness>());
            Assert.IsTrue(generic.IsA<Place>());
            Assert.IsTrue(generic.IsA<Organization>());
            Assert.IsTrue(generic.IsA<Entity>());
            Assert.IsFalse(generic.IsA<Person>());
            Assert.IsFalse(generic.IsA<ExerciseGym>());
        }

        [TestMethod]
        public void TestOntologyGetEntitiesOfType()
        {
            KnowledgeContext sourceContext = new KnowledgeContext();
            SportsActivityLocation obj = new SportsActivityLocation(sourceContext);
            obj.Description.Value = "This is a sports activity location!";

            ISet<string> ids = new HashSet<string>() { obj.EntityId };
            Assert.AreEqual(1, sourceContext.GetEntitiesOfType<Thing>(ids).ToList().Count);
            Assert.AreEqual(1, sourceContext.GetEntitiesOfType<LocalBusiness>(ids).ToList().Count);
            Assert.AreEqual(1, sourceContext.GetEntitiesOfType<Place>(ids).ToList().Count);
            Assert.AreEqual(1, sourceContext.GetEntitiesOfType<Organization>(ids).ToList().Count);
            Assert.AreEqual(1, sourceContext.GetEntitiesOfType<Entity>(ids).ToList().Count);
            Assert.AreEqual(0, sourceContext.GetEntitiesOfType<Person>(ids).ToList().Count);
            Assert.AreEqual(0, sourceContext.GetEntitiesOfType<ExerciseGym>(ids).ToList().Count);
        }

        [TestMethod]
        public void TestOntologyGetEntitiesOfTypeAfterCopyTo()
        {
            KnowledgeContext sourceContext = new KnowledgeContext();
            SportsActivityLocation obj = new SportsActivityLocation(sourceContext);
            obj.Description.Value = "This is a sports activity location!";

            byte[] payload;
            using (MemoryStream writeStream = new MemoryStream())
            {
                sourceContext.Serialize(writeStream, false);
                payload = writeStream.ToArray();
            }

            using (MemoryStream readStream = new MemoryStream(payload, false))
            {
                sourceContext = KnowledgeContext.Deserialize(readStream, false);
            }

            KnowledgeContext targetContext = new KnowledgeContext();
            Entity downcastEntity = sourceContext.GetEntityInMemory(obj.EntityId);
            downcastEntity.CopyTo(targetContext);

            ISet<string> ids = new HashSet<string>() { obj.EntityId };
            Assert.AreEqual(1, targetContext.GetEntitiesOfType<Thing>(ids).ToList().Count);
            Assert.AreEqual(1, targetContext.GetEntitiesOfType<LocalBusiness>(ids).ToList().Count);
            Assert.AreEqual(1, targetContext.GetEntitiesOfType<Place>(ids).ToList().Count);
            Assert.AreEqual(1, targetContext.GetEntitiesOfType<Organization>(ids).ToList().Count);
            Assert.AreEqual(1, targetContext.GetEntitiesOfType<Entity>(ids).ToList().Count);
            Assert.AreEqual(0, targetContext.GetEntitiesOfType<Person>(ids).ToList().Count);
            Assert.AreEqual(0, targetContext.GetEntitiesOfType<ExerciseGym>(ids).ToList().Count);
        }

        [TestMethod]
        public void TestOntologyEntitiesPreserveTypeNameAfterCopyTo()
        {
            KnowledgeContext sourceContext = new KnowledgeContext();
            SportsActivityLocation obj = new SportsActivityLocation(sourceContext);
            obj.Description.Value = "This is a sports activity location!";

            KnowledgeContext targetContext = new KnowledgeContext();
            Entity downcastEntity = sourceContext.GetEntityInMemory(obj.EntityId);
            downcastEntity.CopyTo(targetContext);

            Entity reifiedEntity = targetContext.GetEntityInMemory(obj.EntityId);
            Assert.AreEqual("http://schema.org/SportsActivityLocation", reifiedEntity.EntityTypeName);

            Organization reifiedOrganization = targetContext.GetEntityInMemory<Organization>(obj.EntityId);
            Assert.AreEqual("http://schema.org/SportsActivityLocation", reifiedOrganization.EntityTypeName);
        }

        [TestMethod]
        public void TestOntologyEntityIntermediateInheritance()
        {
            KnowledgeContext sourceContext = new KnowledgeContext();
            SportsActivityLocation obj = new SportsActivityLocation(sourceContext);
            obj.Description.Value = "This is a sports activity location!";

            LocalBusiness business = sourceContext.GetEntityInMemory<LocalBusiness>(obj.EntityId);
            Assert.IsNotNull(business);
            Assert.IsTrue(business.IsA<Place>());
            Assert.IsTrue(business.IsA<Organization>());
            Assert.IsTrue(business.IsA<Thing>());
            Assert.AreEqual("This is a sports activity location!", business.Description.Value);
        }

        [TestMethod]
        public void TestOntologyCopyToSameContext()
        {
            KnowledgeContext context = new KnowledgeContext();
            Person person = new Person(context);
            person.Description.Value = "This is a person!";
            PostalAddress addr = new PostalAddress(context);
            addr.AddressLocality.Value = "Space";
            person.Address_as_PostalAddress.SetValue(addr);

            person.CopyTo(context, true);

            Assert.AreEqual("This is a person!", person.Description.Value);
            Assert.AreEqual("This is a person!", context.GetEntityInMemory<Person>(person.EntityId).Description.Value);
            Assert.IsNotNull(person.Address_as_PostalAddress.ValueInMemory);
            Assert.IsNotNull(context.GetEntityInMemory<Person>(person.EntityId).Address_as_PostalAddress.ValueInMemory);
        }
    }
}
