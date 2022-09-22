using ProtoBuf;
using Vintagestory.API.Common;

namespace CaptureAnimals
{
    [ProtoContract]
    public class CaptureEntity
    {
        [ProtoMember(1)]
        public string Code { get; set; }

        [ProtoMember(2)]
        public float CaptureChance { get; set; }

        public CaptureEntity WithCode(AssetLocation code)
        {
            return new CaptureEntity()
            {
                Code = code + "",
                CaptureChance = CaptureChance
            };
        }
    }
}