using LUC.DiscoveryService.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace DiscoveryService.Test
{
    //TODO rename methods to format <Test_method>_<Scenery>_<Expected_behavior>
    [TestClass]
    public class RecentMessagesTest
    {
        [TestMethod]
        public void Prunning()
        {
            var messages = new RecentMessages();
            var timeNow = DateTime.Now;

            messages.Messages.TryAdd("a", timeNow.AddSeconds(value: -2));
            messages.Messages.TryAdd("b", timeNow.AddSeconds(value: -3));
            messages.Messages.TryAdd("c", timeNow);

            Assert.AreEqual(expected: 2, actual: messages.Prune());
            Assert.AreEqual(expected: 1, actual: messages.Messages.Count);
            Assert.IsTrue(messages.Messages.ContainsKey("c"));
        }

        [TestMethod]
        public void MessageId()
        {
            var messages = new RecentMessages();

            var idClassA_1 = messages.GetId(new Byte[] { 1 });
            var idClassA_2 = messages.GetId(new Byte[] { 1 });
            var idClassB_1 = messages.GetId(new Byte[] { 2 });

            Assert.AreEqual(expected: idClassA_1, actual: idClassA_2);
            Assert.AreNotEqual(notExpected: idClassB_1, actual: idClassA_1);
        }

        [TestMethod]
        public async Task DuplicateCheck()
        {
            var messages = new RecentMessages
            {
                Interval = TimeSpan.FromMilliseconds(value: 100)
            };
            var byteOfClassA = new Byte[] { 1 };
            var byteOfClassB = new Byte[] { 2 };

            Assert.IsTrue(condition: messages.TryAdd(byteOfClassA));
            Assert.IsTrue(messages.TryAdd(byteOfClassB));
            Assert.IsFalse(messages.TryAdd(byteOfClassA));

            await Task.Delay(millisecondsDelay: 200);

            Assert.IsTrue(messages.TryAdd(byteOfClassA));
        }
    }
}
