using ConsensusBenchmarker.Consensus.PoW;
using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks;
using ConsensusBenchmarker.Models.Blocks.ConsensusBlocks;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace ConsensusBenchmarkerTest.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void HashNewBlock()
        {
            // Arrange
            var consensus = new PoWConsensus(1);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "HashNewBlock", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);
            string previousBlockHash = "ABC";
            string transactions = "5;1;2022-01-01:00:00:00,6;1;2022-01-01:00:00:00"; // Two transactions
            byte[] previousHashAndTransactions = Encoding.UTF8.GetBytes(previousBlockHash + transactions);
            int nonce = 0;
            object[] parameters = { previousHashAndTransactions, nonce };

            // Act
            var (result, _) = ((byte[], byte[]))methodInfo!.Invoke(consensus, parameters)!;
            string stringres = Convert.ToHexString(result);

            // Assert
            Assert.AreEqual(((256 / 8) * 2), stringres.Length);
        }

        [TestMethod]
        public void HashConformsToDifficulty_ReturnsTrue()
        {
            // Arrange
            var consensus = new PoWConsensus(1);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "HashConformsToDifficulty", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);
            string hash = "0000000001BFA03384B2B3093E269E916DBDB5CC1168DF5ED148B88978609775";
            object[] parameters = { hash };

            // Act
            bool result = (bool)methodInfo!.Invoke(consensus, parameters)!;

            // Assert
            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void HashConformsToDifficulty_ReturnsFalse()
        {
            // Arrange
            var consensus = new PoWConsensus(1);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "HashConformsToDifficulty", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);
            string hash = "00FBBF0E41BFA03384B2B3093E269E916DBDB5CC1168DF5ED148B88978609775";
            object[] parameters = { hash };

            // Act
            bool result = (bool)methodInfo!.Invoke(consensus, parameters)!;

            // Assert
            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void HashConformsToDifficulty_ReturnsTrue2()
        {
            // Arrange
            var consensus = new PoWConsensus(1);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "HashConformsToDifficulty", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);
            string hash = "0000000000000000000000093E269E916DBDB5CC1168DF5ED148B88978609775";
            object[] parameters = { hash };

            // Act
            bool result = (bool)methodInfo!.Invoke(consensus, parameters)!;

            // Assert
            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void MineNewBlock()
        {
            // Arrange
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            ref var stopWatchRef = ref stopWatch;
            object[] parameters = { stopWatchRef };

            var consensus = new PoWConsensus(1);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "MineNewBlock", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);
            var transactions = new List<Transaction>()
            {
                { new Transaction(2, 1, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 1, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions;

            // Act
            PoWBlock result = (PoWBlock)methodInfo!.Invoke(consensus, parameters)!;

            // Assert
            Console.WriteLine("It took: " + stopWatch.Elapsed.Seconds + " seconds.");
            Assert.AreEqual(true, result.BlockHash.Length > 0);
        }

        [TestMethod]
        public void RecieveBlock_AddGenesisBlockAndTheNextBlock()
        {
            // Arrange
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            ref var stopWatchRef = ref stopWatch;
            object[] parameters = { stopWatchRef };

            var consensus = new PoWConsensus(1);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "MineNewBlock", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);

            // Block 1:
            var transactions1 = new List<Transaction>()
            {
                { new Transaction(2, 1, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 1, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions1.ToList();
            _ = (PoWBlock)methodInfo!.Invoke(consensus, parameters)!;

            // Block 2:
            var transactions2 = new List<Transaction>()
            {
                { new Transaction(2, 2, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 2, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions2.ToList();
            PoWBlock blocked2 = (PoWBlock)methodInfo!.Invoke(consensus, parameters)!;
            consensus.Blocks.Remove(blocked2); // It is added in "MineNewBlock" when it is 'mined'.
            consensus.TotalBlocksInChain--;
            consensus.RecievedTransactionsSinceLastBlock = transactions2.ToList(); // Simulates that it recieves a 'mined block'.

            // Act
            consensus.RecieveBlock(blocked2);

            // Assert
            Console.WriteLine("It took: " + stopWatch.Elapsed.Seconds + " seconds.");
            Assert.AreEqual(2, consensus.Blocks.Count);
            Assert.AreEqual(0, consensus.RecievedTransactionsSinceLastBlock.Count);
        }

        [TestMethod]
        public void RecieveBlock_AddGenesisBlockAndTheNextInvalidBlock()
        {
            // Arrange
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            ref var stopWatchRef = ref stopWatch;
            object[] parameters = { stopWatchRef };

            var consensus = new PoWConsensus(1);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "MineNewBlock", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);

            // Block 1:
            var transactions1 = new List<Transaction>()
            {
                { new Transaction(2, 1, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 1, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions1.ToList();
            PoWBlock blocked1 = (PoWBlock)methodInfo!.Invoke(consensus, parameters)!;

            // Block 2:
            var transactions2 = new List<Transaction>()
            {
                { new Transaction(2, 2, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 2, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions2.ToList();
            var invalidBlock2 = new PoWBlock(42, DateTime.Now.ToLocalTime(), transactions2.ToList(), "000000DJKHSDG000SOME0000HASH0QQQ", blocked1.BlockHash, 12345); // It HIGHLY unlikely that this is the correct nonce.

            // Act
            consensus.RecieveBlock(invalidBlock2);

            // Assert
            Console.WriteLine("It took: " + stopWatch.Elapsed.Seconds + " seconds.");
            Assert.AreEqual(1, consensus.Blocks.Count);
            Assert.AreEqual(2, consensus.RecievedTransactionsSinceLastBlock.Count);
        }

        [TestMethod]
        public void RecieveBlock_AddExtraTransaction()
        {
            // Arrange
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            ref var stopWatchRef = ref stopWatch;
            object[] parameters = { stopWatchRef };

            var consensus = new PoWConsensus(1);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "MineNewBlock", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);

            // Block 1:
            var transactions1 = new List<Transaction>()
            {
                { new Transaction(2, 1, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 1, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions1.ToList();
            _ = (PoWBlock)methodInfo!.Invoke(consensus, parameters)!;

            // Block 2:
            var transactions2 = new List<Transaction>()
            {
                { new Transaction(2, 2, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 2, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions2.ToList();
            PoWBlock blocked2 = (PoWBlock)methodInfo!.Invoke(consensus, parameters)!;
            consensus.Blocks.Remove(blocked2); // It is added in "MineNewBlock" when it is 'mined'.
            consensus.TotalBlocksInChain--;
            consensus.RecievedTransactionsSinceLastBlock = transactions2.ToList(); // Simulates that it recieves a 'mined block'.
            consensus.AddNewTransaction(new Transaction(42, 3, DateTime.Now.ToLocalTime()));

            // Act
            consensus.RecieveBlock(blocked2);

            // Assert
            Console.WriteLine("It took: " + stopWatch.Elapsed.Seconds + " seconds.");
            Assert.AreEqual(2, consensus.Blocks.Count);
            Assert.AreEqual(1, consensus.RecievedTransactionsSinceLastBlock.Count);
        }

        [TestMethod]
        public void RecieveBlock_AreTwoBlockHashesEqual()
        {
            // Arrange
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            ref var stopWatchRef = ref stopWatch;
            object[] parameters = { stopWatchRef };

            var consensus = new PoWConsensus(1);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "MineNewBlock", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);

            // Block 1:
            var transactions1 = new List<Transaction>()
            {
                { new Transaction(2, 1, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 1, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions1.ToList();
            PoWBlock block1 = (PoWBlock)methodInfo!.Invoke(consensus, parameters)!;

            // Block 2:
            var transactions2 = new List<Transaction>()
            {
                { new Transaction(2, 2, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 2, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions2.ToList();
            PoWBlock block2 = (PoWBlock)methodInfo!.Invoke(consensus, parameters)!;
            consensus.RecievedTransactionsSinceLastBlock = transactions2.ToList();
            consensus.Blocks.Remove(block2);

            // Serialize and deserialize:
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

            string block2Serialized = JsonConvert.SerializeObject(block2, settings);
            if (JsonConvert.DeserializeObject<Block>(block2Serialized, settings) is not Block block2Deserialized)
            {
                throw new ArgumentException("Block could not be deserialized correctly", nameof(block2Deserialized));
            }

            // Set up validate block:
            MethodInfo? methodInfo2 = typeof(PoWConsensus).GetMethod(name: "IsBlockValid", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);
            object[] parameters2 = { block1, block2Deserialized };

            // Act
            bool result = (bool)methodInfo2!.Invoke(consensus, parameters2)!;

            // Assert
            Console.WriteLine("It took: " + stopWatch.Elapsed.Seconds + " seconds.");
            Assert.AreEqual(true, result);
        }

    }
}
