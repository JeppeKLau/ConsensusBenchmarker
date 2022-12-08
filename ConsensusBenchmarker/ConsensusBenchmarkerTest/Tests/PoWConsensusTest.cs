using ConsensusBenchmarker.Consensus.PoW;
using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks.ConsensusBlocks;
using System.Net;
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
            string transactions = "123.4.5.6.7;5;1;2022-01-01:00:00:00,123.4.5.6.7;6;1;2022-01-01:00:00:00"; // Two transactions
            byte[] previousHashAndTransactions = Encoding.UTF8.GetBytes(previousBlockHash + transactions);
            uint nonce = 0;

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
            string hash = "000BBF0E41BFA03384B2B3093E269E916DBDB5CC1168DF5ED148B88978609775";

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
            string hash = "0000BF0E41BFA03384B2B3093E269E916DBDB5CC1168DF5ED148B88978609775";

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
                { new Transaction(new IPAddress(new byte[] { 192, 0, 0, 1}), 2, 1, DateTime.Now.ToLocalTime()) },
                { new Transaction(new IPAddress(new byte[] { 192, 0, 0, 1}), 3, 1, DateTime.Now.ToLocalTime()) },
            };

            // Act
            object[] parameters = { transactions };
            PoWBlock result = (PoWBlock)methodInfo!.Invoke(consensus, parameters)!;

            // Assert
            Assert.AreEqual(true, result.BlockHash.Length > 0);
        }

    }
}
