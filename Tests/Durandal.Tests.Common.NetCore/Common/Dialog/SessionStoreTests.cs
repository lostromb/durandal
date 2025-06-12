using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Durandal.Common.Utils;
using Durandal.Common.Dialog;
using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.Dialog.Services;
using Newtonsoft.Json;
using Durandal.Common.Cache;
using System.IO;

namespace Durandal.Tests.Common.Dialog
{
    [TestClass]

    public class SessionStoreTests
    {
        [TestMethod]
        public void TestSessionStorePrimitiveTypeRetrieval()
        {
            ConversationState mockConversationState = new ConversationState();
            InMemoryDataStore store = mockConversationState.SessionStore;
            store.Put("number", 177);
            SerializedConversationState serializedState = mockConversationState.Serialize();

            ConversationState reifiedState = ConversationState.Deserialize(serializedState, new ConsoleLogger());
            InMemoryDataStore reifiedStore = reifiedState.SessionStore;
            Assert.AreEqual(177, reifiedStore.GetInt("number"));
        }

        [TestMethod]
        public void TestSessionStoreJsonObjectRetrieval()
        {
            SecurityToken input = new SecurityToken()
            {
                Blue = "blue",
                Red = "red"
            };

            ConversationState mockConversationState = new ConversationState();
            InMemoryDataStore store = mockConversationState.SessionStore;
            store.Put("token", input);
            SerializedConversationState serializedState = mockConversationState.Serialize();

            ConversationState reifiedState = ConversationState.Deserialize(serializedState, new ConsoleLogger());
            InMemoryDataStore reifiedStore = reifiedState.SessionStore;

            SecurityToken output = reifiedStore.GetObject<SecurityToken>("token");
            Assert.AreEqual(input.Red, output.Red);
            Assert.AreEqual(input.Blue, output.Blue);
        }

        [TestMethod]
        public void TestSessionStorePrimitiveTypeMismatch()
        {
            ConversationState mockConversationState = new ConversationState();
            InMemoryDataStore store = mockConversationState.SessionStore;
            store.Put("number", 177);
            SerializedConversationState serializedState = mockConversationState.Serialize();

            ConversationState reifiedState = ConversationState.Deserialize(serializedState, new ConsoleLogger());
            InMemoryDataStore reifiedStore = reifiedState.SessionStore;
            try
            {
                reifiedStore.GetBool("number");
                Assert.Fail("Expected an InvalidOperationException");
            }
            catch (InvalidOperationException) { }
        }

        [TestMethod]
        public void TestSessionStoreStringRetrieval()
        {
            ConversationState mockConversationState = new ConversationState();
            InMemoryDataStore store = mockConversationState.SessionStore;
            store.Put("string", "hello computer");
            SerializedConversationState serializedState = mockConversationState.Serialize();

            ConversationState reifiedState = ConversationState.Deserialize(serializedState, new ConsoleLogger());
            InMemoryDataStore reifiedStore = reifiedState.SessionStore;
            Assert.AreEqual("hello computer", reifiedStore.GetString("string"));
        }

        [TestMethod]
        public void TestSerializeDialogActionCacheToJson()
        {
            InMemoryDialogActionCache dialogActionCache = new InMemoryDialogActionCache();
            dialogActionCache.Store(new DialogAction()
            {
                Domain = "mydomain",
                Intent = "myintent",
                InteractionMethod = InputMethod.Tactile,
                Slots = new List<SlotValue>()
                {
                    new SlotValue("slot", "slotvalue", SlotValueFormat.DialogActionParameter, "lexicalform")
                }
            }, TimeSpan.FromSeconds(10));
            dialogActionCache.Store(new DialogAction()
            {
                Domain = "mydomain",
                Intent = "myintent2",
                InteractionMethod = InputMethod.Tactile,
                Slots = new List<SlotValue>()
                {
                    new SlotValue("slot", "slotvalue", SlotValueFormat.DialogActionParameter, "lexicalform")
                }
            }, TimeSpan.FromSeconds(15));

            string json = JsonConvert.SerializeObject(dialogActionCache);
            InMemoryDialogActionCache deserialized = JsonConvert.DeserializeObject<InMemoryDialogActionCache>(json);
            Assert.IsNotNull(deserialized);
            List<CachedItem<DialogAction>> items = deserialized.GetAllItems().ToList();
            Assert.AreEqual(2, items.Count);
            Assert.AreEqual("mydomain", items[0].Item.Domain);
            Assert.AreEqual("myintent", items[0].Item.Intent);
            Assert.AreEqual(InputMethod.Tactile, items[0].Item.InteractionMethod);
            Assert.AreEqual(1, items[0].Item.Slots.Count);
            Assert.AreEqual("slot", items[0].Item.Slots[0].Name);
            Assert.AreEqual("slotvalue", items[0].Item.Slots[0].Value);
            Assert.AreEqual(SlotValueFormat.DialogActionParameter, items[0].Item.Slots[0].Format);
            Assert.AreEqual("lexicalform", items[0].Item.Slots[0].LexicalForm);
            Assert.AreEqual("myintent2", items[1].Item.Intent);
        }

        [TestMethod]
        public void TestSerializeInMemoryDataStore()
        {
            InMemoryDataStore store = new InMemoryDataStore();
            string json = JsonConvert.SerializeObject(store);
            store = JsonConvert.DeserializeObject<InMemoryDataStore>(json);
            Assert.IsNotNull(store);
            Assert.AreEqual(0, store.GetAllObjects().Count);

            store.Put("key1", "value1");
            store.Put("key2", false);
            store.Put("KEY3", 123456);

            json = JsonConvert.SerializeObject(store);
            store = JsonConvert.DeserializeObject<InMemoryDataStore>(json);
            Assert.IsNotNull(store);
            Assert.AreEqual(3, store.GetAllObjects().Count);
            Assert.AreEqual("value1", store.GetString("key1"));
            Assert.AreEqual(false, store.GetBool("key2"));
            Assert.AreEqual(123456, store.GetInt("KEY3"));
        }

        [TestMethod]
        public void TestUserProfileSerializerBlock()
        {
            InMemoryDataStore dataStore = new InMemoryDataStore();
            UserProfileSerializer serializer = new UserProfileSerializer();
            dataStore.Put("key1", 5);
            dataStore.Put("key2", true);
            dataStore.Put("key3", "value3");

            byte[] serialized = serializer.Encode(dataStore);
            InMemoryDataStore deserialized = serializer.Decode(serialized, 0, serialized.Length);
            Assert.AreEqual(3, deserialized.Count);
            Assert.AreEqual(5, deserialized.GetInt("key1"));
            Assert.AreEqual(true, deserialized.GetBool("key2"));
            Assert.AreEqual("value3", deserialized.GetString("key3"));
        }

        [TestMethod]
        public void TestUserProfileSerializerStream()
        {
            InMemoryDataStore dataStore = new InMemoryDataStore();
            UserProfileSerializer serializer = new UserProfileSerializer();
            dataStore.Put("key1", 5);
            dataStore.Put("key2", true);
            dataStore.Put("key3", "value3");

            using (MemoryStream stream = new MemoryStream())
            {
                int length = serializer.Encode(dataStore, stream);
                stream.Seek(0, SeekOrigin.Begin);
                InMemoryDataStore deserialized = serializer.Decode(stream, length);

                Assert.AreEqual(3, deserialized.Count);
                Assert.AreEqual(5, deserialized.GetInt("key1"));
                Assert.AreEqual(true, deserialized.GetBool("key2"));
                Assert.AreEqual("value3", deserialized.GetString("key3"));
            }
        }
    }
}
