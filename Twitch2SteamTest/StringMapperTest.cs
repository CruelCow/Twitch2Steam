using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using Twitch2Steam;

namespace Twitch2SteamTest
{
    [TestClass]
    public class StringMapperTest
    {
        [TestMethod]
        public void TestWordBounderies()
        {
            var mapper = new StringMapper(new Dictionary<String, String>()
            {
                {"Kappa", ":steammocking:" },
                {"FailFish", ":steamfacepalm:" }
            });
            Assert.AreEqual("KappaRoss :steammocking: Kappa!", mapper.Map("KappaRoss Kappa Kappa!"));
        }

        [TestMethod]
        public void TestEmptyDictionary()
        {
            var mapper = new StringMapper(new Dictionary<String, String>());
            Assert.AreEqual(String.Empty, mapper.Map(String.Empty));
            Assert.AreEqual("abcdefg", mapper.Map("abcdefg"));
        }
    }
}
