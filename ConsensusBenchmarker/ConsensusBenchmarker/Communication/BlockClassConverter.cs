using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConsensusBenchmarker.Models.Blocks;
using ConsensusBenchmarker.Models.Blocks.ConsensusBlocks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace ConsensusBenchmarker.Communication
{
    public class BlockClassConverter : DefaultContractResolver
    {
        protected override JsonConverter? ResolveContractConverter(Type objectType)
        {
            if (typeof(Block).IsAssignableFrom(objectType) && !objectType.IsAbstract)
                return null; // pretend TableSortRuleConvert is not specified (thus avoiding a stack overflow)
            return base.ResolveContractConverter(objectType);
        }
    }

    public class BlockConverter : JsonConverter
    {
        static JsonSerializerSettings SpecifiedSubclassConversion = new JsonSerializerSettings() { ContractResolver = new BlockClassConverter() };

        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(Block));
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);
            switch (jo["BlockType"]!.Value<int>())
            {
                case (int)BlockTypes.PoWBlock:
                    return JsonConvert.DeserializeObject<PoWBlock>(jo.ToString(), SpecifiedSubclassConversion);
                case 2:
                    return JsonConvert.DeserializeObject<RaftBlock>(jo.ToString(), SpecifiedSubclassConversion);
                default:
                    throw new Exception("Could not find the correct derived class when reading json.");
            }
            throw new Exception("Could not find the BlockType in the recieved json.");
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotImplementedException(); // won't be called because CanWrite returns false
        }

    }
}
