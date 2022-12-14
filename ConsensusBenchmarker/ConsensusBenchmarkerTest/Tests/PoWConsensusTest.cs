using ConsensusBenchmarker.Consensus.PoW;
using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks.ConsensusBlocks;
using Newtonsoft.Json;
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
            PoWConsensus consensus = new PoWConsensus(1);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "HashNewBlock", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);
            string previousBlockHash = "ABC";
            string transactions = "5;1;2022-01-01:00:00:00,6;1;2022-01-01:00:00:00"; // Two transactions
            byte[] previousHashAndTransactions = Encoding.UTF8.GetBytes(previousBlockHash + transactions);
            int nonce = 0;

            // Act
            object[] parameters = { previousHashAndTransactions, nonce };
            string result = (string)methodInfo!.Invoke(consensus, parameters)!;

            // Assert
            Assert.AreEqual(((256 / 8) * 2), result.Length);
        }

        [TestMethod]
        public void HashConformsToDifficulty_ReturnsTrue()
        {
            // Arrange
            PoWConsensus consensus = new PoWConsensus(1);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "HashConformsToDifficulty", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);
            string hash = "0000000001BFA03384B2B3093E269E916DBDB5CC1168DF5ED148B88978609775";

            // Act
            object[] parameters = { hash };
            bool result = (bool)methodInfo!.Invoke(consensus, parameters)!;

            // Assert
            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void HashConformsToDifficulty_ReturnsFalse()
        {
            // Arrange
            PoWConsensus consensus = new PoWConsensus(1);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "HashConformsToDifficulty", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);
            string hash = "00FBBF0E41BFA03384B2B3093E269E916DBDB5CC1168DF5ED148B88978609775";

            // Act
            object[] parameters = { hash };
            bool result = (bool)methodInfo!.Invoke(consensus, parameters)!;

            // Assert
            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void HashConformsToDifficulty_ReturnsTrue2()
        {
            // Arrange
            PoWConsensus consensus = new PoWConsensus(1);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "HashConformsToDifficulty", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);
            string hash = "0000000000000000000000093E269E916DBDB5CC1168DF5ED148B88978609775";

            // Act
            object[] parameters = { hash };
            bool result = (bool)methodInfo!.Invoke(consensus, parameters)!;

            // Assert
            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void MineNewBlock()
        {
            // Arrange
            PoWConsensus consensus = new PoWConsensus(1);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "MineNewBlock", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);
            List<Transaction> transactions = new List<Transaction>()
            {
                { new Transaction(2, 1, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 1, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions;

            // Act
            PoWBlock result = (PoWBlock)methodInfo!.Invoke(consensus, null)!;

            // Assert
            Assert.AreEqual(true, result.BlockHash.Length > 0);
        }

        [TestMethod]
        public void RecieveBlock_AddGenesisBlock()
        {
            // Arrange
            PoWConsensus consensus = new PoWConsensus(1);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "MineNewBlock", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);
            List<Transaction> transactions = new List<Transaction>()
            {
                { new Transaction(2, 1, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 1, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions;
            PoWBlock hashBlocked = (PoWBlock)methodInfo!.Invoke(consensus, null)!;
            string hashBlockedSerialized = JsonConvert.SerializeObject(hashBlocked);

            // Act
            consensus.RecieveBlock(hashBlockedSerialized);

            // Assert
            Assert.AreEqual(1, consensus.Blocks.Count);
        }

        [TestMethod]
        public void RecieveBlock_AddGenesisBlockAndTheNextBlock()
        {
            // Arrange
            PoWConsensus consensus = new PoWConsensus(1);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "MineNewBlock", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Block 1:
            List<Transaction> transactions1 = new List<Transaction>()
            {
                { new Transaction(2, 1, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 1, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions1;
            PoWBlock blocked1 = (PoWBlock)methodInfo!.Invoke(consensus, null)!;
            string block1Serialized = JsonConvert.SerializeObject(blocked1);
            consensus.RecieveBlock(block1Serialized); // This method clears the list fyi: consensus.RecievedTransactionsSinceLastBlock

            // Block 2:
            List<Transaction> transactions2 = new List<Transaction>()
            {
                { new Transaction(2, 2, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 2, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions2;
            PoWBlock blocked2 = (PoWBlock)methodInfo!.Invoke(consensus, null)!;
            string block2Serialized = JsonConvert.SerializeObject(blocked2);

            // Act
            consensus.RecieveBlock(block2Serialized);

            // Assert
            Assert.AreEqual(2, consensus.Blocks.Count);
            Assert.AreEqual(0, consensus.RecievedTransactionsSinceLastBlock.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(Exception), "A invalid block was wrongfully determined to be valid and added to the chain.")]
        public void RecieveBlock_AddGenesisBlockAndTheNextInvalidBlock()
        {
            // Arrange
            PoWConsensus consensus = new PoWConsensus(1);
            MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "MineNewBlock", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);

            // Block 1:
            List<Transaction> transactions1 = new List<Transaction>()
            {
                { new Transaction(2, 1, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 1, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions1;
            PoWBlock blocked1 = (PoWBlock)methodInfo!.Invoke(consensus, null)!;
            string block1Serialized = JsonConvert.SerializeObject(blocked1);
            consensus.RecieveBlock(block1Serialized); // This method clears the list fyi: consensus.RecievedTransactionsSinceLastBlock

            // Block 2:
            List<Transaction> transactions2 = new List<Transaction>()
            {
                { new Transaction(2, 2, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 2, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions1;
            PoWBlock invalidBlock2 = new PoWBlock(42, DateTime.Now.ToLocalTime(), transactions2, "000000DJKHSDG000SOME0000HASH0QQQ", blocked1.BlockHash, 696969);
            string invalidBlock2Serialized = JsonConvert.SerializeObject(invalidBlock2);

            // Act
            consensus.RecieveBlock(invalidBlock2Serialized);

            // Assert
            Assert.AreEqual(1, consensus.Blocks.Count);
            Assert.AreEqual(2, consensus.RecievedTransactionsSinceLastBlock.Count);
        }

    }
}
