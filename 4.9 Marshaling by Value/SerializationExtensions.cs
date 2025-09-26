using System.Linq;
using CreationGrowing;
using Creations.Types;
using Unity.Collections;
using Unity.Netcode;

public static class SerializationExtensions
{
    public static void ReadValueSafe(this FastBufferReader reader, out GrowingCreationRequirements requirements)
    {
        reader.ReadValueSafe(out int requiredCreation);
        reader.ReadValueSafe(out bool bindCreations);
        reader.ReadValueSafe(out NativeArray<GrowingRequirement> growingRequirements, Allocator.Temp);
        reader.ReadValueSafe(out bool isCardinalityRequirementRequired);
        requirements = new GrowingCreationRequirements(
            (SpecificCreationType)requiredCreation,
            bindCreations,
            growingRequirements.ToList(),
            isCardinalityRequirementRequired
        );
    }

    public static void WriteValueSafe(this FastBufferWriter writer, in GrowingCreationRequirements requirements)
    {
        writer.WriteValueSafe((int)requirements.RequiredCreation);
        writer.WriteValueSafe(requirements.BindCreations);
        writer.WriteValueSafe(requirements.GrowingRequirements.ToArray());
        writer.WriteValueSafe(requirements.IsCardinalityRequirementRequired);
    }
}