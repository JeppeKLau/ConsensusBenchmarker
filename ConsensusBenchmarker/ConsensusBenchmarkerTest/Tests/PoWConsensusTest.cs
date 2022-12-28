using ConsensusBenchmarker.Consensus.PoW;
using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks;
using ConsensusBenchmarker.Models.Blocks.ConsensusBlocks;
using ConsensusBenchmarker.Models.DTOs;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
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
            var consensus = new PoWConsensus(1, 10);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "HashNewBlock", bindingAttr: BindingFlags.NonPublic | BindingFlags.Static);
            string previousBlockHash = "ABC";
            string transactions = "5;1;2022-01-01:00:00:00,6;1;2022-01-01:00:00:00"; // Two transactions
            byte[] previousHashAndTransactions = Encoding.UTF8.GetBytes(previousBlockHash + transactions);
            long nonce = 0;

            using SHA256 sha256 = SHA256.Create();
            object[] parameters = { sha256, previousHashAndTransactions, nonce };

            // Act
            var result = (string)methodInfo!.Invoke(consensus, parameters)!;

            // Assert
            Assert.AreEqual(((256 / 8) * 2), result.Length);
        }

        [TestMethod]
        public void HashNewBlock_UseTwoDifferentInstances()
        {
            // Arrange
            var consensus1 = new PoWConsensus(1, 10);
            var consensus2 = new PoWConsensus(2, 10);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "HashNewBlock", bindingAttr: BindingFlags.NonPublic | BindingFlags.Static);
            string previousBlockHash = "ABC";
            string transactions = "5;1;2022-01-01:00:00:00,6;1;2022-01-01:00:00:00"; // Two transactions
            byte[] previousHashAndTransactions = Encoding.UTF8.GetBytes(previousBlockHash + transactions);
            long nonce = 0;
            string result1;
            string result2;

            // Act
            using (SHA256 sha256 = SHA256.Create())
            {
                object[] parameters = { sha256, previousHashAndTransactions, nonce };
                result1 = (string)methodInfo!.Invoke(consensus1, parameters)!;
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                object[] parameters = { sha256, previousHashAndTransactions, nonce };
                result2 = (string)methodInfo!.Invoke(consensus2, parameters)!;
            }

            // Assert
            Assert.AreEqual(result1, result2);
        }

        [TestMethod]
        public void HashConformsToDifficulty_ReturnsFalse()
        {
            // Arrange
            var consensus = new PoWConsensus(1, 10);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "HashConformsToDifficulty", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);
            string hash = "00FBBF0E41BFA03384B2B3093E269E916DBDB5CC1168DF5ED148B88978609775";
            object[] parameters = { hash };

            // Act
            bool result = (bool)methodInfo!.Invoke(consensus, parameters)!;

            // Assert
            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void HashConformsToDifficulty_ReturnsTrue()
        {
            // Arrange
            var consensus = new PoWConsensus(1, 10);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "HashConformsToDifficulty", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);
            string hash = "0000000001BFA03384B2B3093E269E916DBDB5CC1168DF5ED148B88978609775";
            object[] parameters = { hash };

            // Act
            bool result = (bool)methodInfo!.Invoke(consensus, parameters)!;

            // Assert
            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void HashConformsToDifficulty_ReturnsTrue2()
        {
            // Arrange
            var consensus = new PoWConsensus(1, 10);
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

            var consensus = new PoWConsensus(1, 10);
            consensus.BeginConsensus();

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
            Assert.AreEqual(true, result.BlockHash.Length > 0);
        }

        [TestMethod]
        public void GenerateNextBlock_TryToAddOneMoreBlockThanTotal()
        {
            // Arrange
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var consensus = new PoWConsensus(1, 1);
            consensus.BeginConsensus();

            var transactions1 = new List<Transaction>()
            {
                { new Transaction(2, 1, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 1, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions1;
            PoWBlock? block1 = consensus.GenerateNextBlock(ref stopWatch);
            bool block2Result = consensus.RecieveBlock(new BlockDTO(block1!, 1), ref stopWatch);

            var transactions2 = new List<Transaction>()
            {
                { new Transaction(2, 2, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 2, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions2;

            // Act
            PoWBlock? block2 = consensus.GenerateNextBlock(ref stopWatch);

            // Assert
            Assert.AreEqual(false, consensus.ExecutionFlag);
            Assert.AreEqual(null, block2);
        }

        [TestMethod]
        public void RecieveBlock_AddGenesisBlockAndTheNextBlock()
        {
            // Arrange
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var consensus = new PoWConsensus(1, 10);
            consensus.BeginConsensus();

            // Block 1:
            var transactions1 = new List<Transaction>()
            {
                { new Transaction(2, 1, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 1, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions1.ToList();
            PoWBlock? block1 = consensus.GenerateNextBlock(ref stopWatch);
            bool block1Result = consensus.RecieveBlock(new BlockDTO(block1!, 1), ref stopWatch);

            // Block 2:
            var transactions2 = new List<Transaction>()
            {
                { new Transaction(2, 2, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 2, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions2.ToList();
            PoWBlock? block2 = consensus.GenerateNextBlock(ref stopWatch);

            // Act
            bool block2Result = consensus.RecieveBlock(new BlockDTO(block2!, 2), ref stopWatch);

            // Assert
            Assert.AreEqual(true, block1Result);
            Assert.AreEqual(true, block2Result);
            Assert.AreEqual(2, consensus.Blocks.Count);
            Assert.AreEqual(0, consensus.RecievedTransactionsSinceLastBlock.Count);
        }

        [TestMethod]
        public void RecieveBlock_AddGenesisBlockAndTheNextInvalidBlock()
        {
            // Arrange
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var consensus = new PoWConsensus(1, 10);
            consensus.BeginConsensus();

            // Block 1:
            var transactions1 = new List<Transaction>()
            {
                { new Transaction(2, 1, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 1, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions1.ToList();
            PoWBlock? block1 = consensus.GenerateNextBlock(ref stopWatch);
            bool block1Result = consensus.RecieveBlock(new BlockDTO(block1!, 1), ref stopWatch);

            // Block 2:
            var transactions2 = new List<Transaction>()
            {
                { new Transaction(2, 2, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 2, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions2.ToList();
            var invalidBlock2 = new PoWBlock(42, DateTime.Now.ToLocalTime(), transactions2.ToList(), "000000DJKHSDG000SOME0000HASH0QQQ", block1!.BlockHash, 12345); // It is HIGHLY unlikely that this is the correct nonce.

            // Act
            bool block2Result = consensus.RecieveBlock(new BlockDTO(invalidBlock2, 2), ref stopWatch);

            // Assert
            Assert.AreEqual(true, block1Result);
            Assert.AreEqual(false, block2Result);
            Assert.AreEqual(1, consensus.Blocks.Count);
            Assert.AreEqual(2, consensus.RecievedTransactionsSinceLastBlock.Count);
        }

        [TestMethod]
        public void RecieveBlock_AddExtraTransaction()
        {
            // Arrange
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var consensus = new PoWConsensus(1, 10);
            consensus.BeginConsensus();

            // Block 1:
            var transactions1 = new List<Transaction>()
            {
                { new Transaction(2, 1, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 1, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions1.ToList();
            PoWBlock? block1 = consensus.GenerateNextBlock(ref stopWatch);
            bool block1Result = consensus.RecieveBlock(new BlockDTO(block1!, 1), ref stopWatch);

            // Block 2:
            var transactions2 = new List<Transaction>()
            {
                { new Transaction(2, 2, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 2, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions2.ToList();
            PoWBlock? block2 = consensus.GenerateNextBlock(ref stopWatch);
            consensus.AddNewTransaction(new Transaction(42, 3, DateTime.Now.ToLocalTime()));

            // Act
            bool block2Result = consensus.RecieveBlock(new BlockDTO(block2!, 2), ref stopWatch);

            // Assert
            Assert.AreEqual(true, block1Result);
            Assert.AreEqual(true, block2Result);
            Assert.AreEqual(2, consensus.Blocks.Count);
            Assert.AreEqual(1, consensus.RecievedTransactionsSinceLastBlock.Count);
        }

        [TestMethod]
        public void RecieveBlock_SerializeAndDeSerializeBlock()
        {
            // Arrange
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var consensus = new PoWConsensus(1, 10);
            consensus.BeginConsensus();

            // Block 1:
            var transactions1 = new List<Transaction>()
            {
                { new Transaction(2, 1, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 1, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions1.ToList();
            PoWBlock? block1 = consensus.GenerateNextBlock(ref stopWatch);

            // Act (1/2):
            bool result1 = consensus.RecieveBlock(new BlockDTO(block1!, 1), ref stopWatch);

            // Block 2:
            var transactions2 = new List<Transaction>()
            {
                { new Transaction(2, 2, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 2, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions2.ToList();
            PoWBlock? block2 = consensus.GenerateNextBlock(ref stopWatch);

            // Serialize and deserialize:
            string block2Serialized = JsonConvert.SerializeObject(block2!);
            if (JsonConvert.DeserializeObject<Block>(block2Serialized) is not Block block2Deserialized)
            {
                throw new ArgumentException("Block could not be deserialized correctly", nameof(block2Deserialized));
            }

            // Act (2/2):
            bool result2 = consensus.RecieveBlock(new BlockDTO(block2Deserialized!, 2), ref stopWatch);

            // Assert
            Assert.AreEqual(true, result1);
            Assert.AreEqual(true, result2);
        }

    }
}
