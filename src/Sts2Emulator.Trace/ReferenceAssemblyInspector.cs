using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Sts2Emulator.Trace;

public sealed record ReferenceMetadataMatch(
    string TypeName,
    string? MethodName,
    int ParameterCount);

public static class ReferenceAssemblyInspector
{
    public static ReferenceMetadataMatch[] Search(
        string assemblyPath,
        string pattern)
    {
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException(
                $"Assembly does not exist: {assemblyPath}",
                assemblyPath);
        }

        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException(
                "Metadata search pattern must be non-empty.",
                nameof(pattern));
        }

        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        if (!peReader.HasMetadata)
        {
            throw new InvalidOperationException(
                $"Assembly has no managed metadata: {assemblyPath}");
        }

        var metadata = peReader.GetMetadataReader();
        var matches = new List<ReferenceMetadataMatch>();

        foreach (var typeHandle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(typeHandle);
            var ns = metadata.GetString(type.Namespace);
            var name = metadata.GetString(type.Name);
            var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
            var typeMatches = fullName.Contains(
                pattern,
                StringComparison.OrdinalIgnoreCase);

            if (typeMatches)
            {
                matches.Add(new ReferenceMetadataMatch(
                    fullName,
                    MethodName: null,
                    ParameterCount: 0));
            }

            foreach (var methodHandle in type.GetMethods())
            {
                var method = metadata.GetMethodDefinition(methodHandle);
                var methodName = metadata.GetString(method.Name);
                if (!typeMatches
                    && !methodName.Contains(
                        pattern,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var signature = method.DecodeSignature(
                    new ParameterCountSignatureProvider(),
                    genericContext: null);

                matches.Add(new ReferenceMetadataMatch(
                    fullName,
                    methodName,
                    signature.ParameterTypes.Length));
            }
        }

        return matches
            .OrderBy(match => match.TypeName, StringComparer.Ordinal)
            .ThenBy(match => match.MethodName, StringComparer.Ordinal)
            .ToArray();
    }

    private sealed class ParameterCountSignatureProvider
        : ISignatureTypeProvider<int, object?>
    {
        public int GetArrayType(int elementType, ArrayShape shape) => 0;

        public int GetByReferenceType(int elementType) => 0;

        public int GetFunctionPointerType(MethodSignature<int> signature) => 0;

        public int GetGenericInstantiation(
            int genericType,
            System.Collections.Immutable.ImmutableArray<int> typeArguments) => 0;

        public int GetGenericMethodParameter(object? genericContext, int index) => 0;

        public int GetGenericTypeParameter(object? genericContext, int index) => 0;

        public int GetModifiedType(
            int modifier,
            int unmodifiedType,
            bool isRequired) => 0;

        public int GetPinnedType(int elementType) => 0;

        public int GetPointerType(int elementType) => 0;

        public int GetPrimitiveType(PrimitiveTypeCode typeCode) => 0;

        public int GetSZArrayType(int elementType) => 0;

        public int GetTypeFromDefinition(
            MetadataReader reader,
            TypeDefinitionHandle handle,
            byte rawTypeKind) => 0;

        public int GetTypeFromReference(
            MetadataReader reader,
            TypeReferenceHandle handle,
            byte rawTypeKind) => 0;

        public int GetTypeFromSpecification(
            MetadataReader reader,
            object? genericContext,
            TypeSpecificationHandle handle,
            byte rawTypeKind) => 0;
    }
}
