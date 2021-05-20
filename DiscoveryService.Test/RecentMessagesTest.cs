using LUC.DiscoveryService.Messages;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace DiscoveryService.Test
{
    [TestFixture]
    public class RecentMessagesTest
    {
        [Test]
        public void Prune_GeneralTest_Equals()
        {
            var messages = new RecentMessages();
            var timeNow = DateTime.Now;

            messages.Messages.TryAdd("a", timeNow.AddSeconds(value: -2));
            messages.Messages.TryAdd("b", timeNow.AddSeconds(value: -3));
            messages.Messages.TryAdd("c", timeNow);

            Assert.AreEqual(expected: 2, actual: messages.Prune());
            Assert.AreEqual(1, messages.Messages.Count);
            Assert.IsTrue(condition: messages.Messages.ContainsKey("c"));
        }

        [Test]
        public void GetId_GeneralTest_Equals()
        {
            var messages = new RecentMessages();

            var idClassA_1 = messages.GetId(new Byte[] { 1 });
            var idClassA_2 = messages.GetId(new Byte[] { 1 });
            var idClassB_1 = messages.GetId(new Byte[] { 2 });

            Assert.AreEqual(expected: idClassA_1, actual: idClassA_2);
            Assert.AreNotEqual(expected: idClassB_1, actual: idClassA_1);
        }

        [Test]
        public async Task TryAdd_Async_DuplicateCheck()
        {
            var messages = new RecentMessages
            {
                Interval = TimeSpan.FromMilliseconds(value: 100)
            };
            var byteOfClassA = new Byte[] { 1 };
            var byteOfClassB = new Byte[] { 2 };

            Assert.IsTrue(condition: messages.TryAdd(byteOfClassA));
            Assert.IsTrue(condition: messages.TryAdd(byteOfClassB));
            Assert.IsFalse(condition: messages.TryAdd(byteOfClassA));
            await Task.Delay(millisecondsDelay: 200);
            Assert.IsTrue(condition: messages.TryAdd(byteOfClassA));
        }
    }
}
