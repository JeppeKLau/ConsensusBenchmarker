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

        //[TestMethod]
        //public void RecieveBlock_AddGenesisBlock()
        //{
        //    // Arrange
        //    PoWConsensus consensus = new PoWConsensus(1);
        //    MethodInfo? methodInfo = typeof(PoWConsensus).GetMethod(name: "MineNewBlock", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);
        //    List<Transaction> transactions = new List<Transaction>()
        //    {
        //        { new Transaction(2, 1, DateTime.Now.ToLocalTime()) },
        //        { new Transaction(3, 1, DateTime.Now.ToLocalTime()) },
        //    };
        //    consensus.RecievedTransactionsSinceLastBlock = transactions;
        //    PoWBlock hashBlocked = (PoWBlock)methodInfo!.Invoke(consensus, null)!;

        //    // Act
        //    consensus.RecieveBlock(hashBlocked);

        //    // Assert
        //    Assert.AreEqual(1, consensus.Blocks.Count);
        //}

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
            _ = (PoWBlock)methodInfo!.Invoke(consensus, null)!;

            // Block 2:
            List<Transaction> transactions2 = new List<Transaction>()
            {
                { new Transaction(2, 2, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 2, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions2;
            PoWBlock blocked2 = (PoWBlock)methodInfo!.Invoke(consensus, null)!;
            consensus.Blocks.Remove(blocked2); // It is added in "MineNewBlock" when it is 'mined'.
            consensus.RecievedTransactionsSinceLastBlock = transactions2; // Simulates that it recieves a 'mined block'.

            // Act
            consensus.RecieveBlock(blocked2);

            // Assert
            Assert.AreEqual(2, consensus.Blocks.Count);
            Assert.AreEqual(0, consensus.RecievedTransactionsSinceLastBlock.Count);
        }

        [TestMethod]
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

            // Block 2:
            List<Transaction> transactions2 = new List<Transaction>()
            {
                { new Transaction(2, 2, DateTime.Now.ToLocalTime()) },
                { new Transaction(3, 2, DateTime.Now.ToLocalTime()) },
            };
            consensus.RecievedTransactionsSinceLastBlock = transactions2;
            PoWBlock invalidBlock2 = new PoWBlock(42, DateTime.Now.ToLocalTime(), transactions2, "000000DJKHSDG000SOME0000HASH0QQQ", blocked1.BlockHash, 12345); // It HIGHLY unlikely that this is the correct nonce.

            // Act
            consensus.RecieveBlock(invalidBlock2);

            // Assert
            Assert.AreEqual(1, consensus.Blocks.Count);
            Assert.AreEqual(2, consensus.RecievedTransactionsSinceLastBlock.Count);
        }

    }
}
