using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace Deckland.Tools.AssemblyAudit;

internal static class Program
{
	private const string AssemblyMetadataAttributeName = "AssemblyMetadataAttribute";
	private const string AssemblyMetadataAttributeNamespace = "System.Reflection";

	public static int Main(string[] args)
	{
		try
		{
			AuditOptions options = AuditOptions.Parse(args);
			AssemblyAuditReport report = Audit(options);
			string json = JsonSerializer.Serialize(report, new JsonSerializerOptions
			{
				WriteIndented = true,
				PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
			});
			if (!string.IsNullOrWhiteSpace(options.ReportPath))
			{
				string reportPath = Path.GetFullPath(options.ReportPath);
				Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
				File.WriteAllText(reportPath, json + Environment.NewLine);
			}
			Console.WriteLine(json);
			return report.Ok ? 0 : 1;
		}
		catch (Exception exception)
		{
			Console.Error.WriteLine($"ASSEMBLY_AUDIT_ERROR: {exception.Message}");
			return 2;
		}
	}

	private static AssemblyAuditReport Audit(AuditOptions options)
	{
		string assemblyPath = Path.GetFullPath(options.AssemblyPath);
		using FileStream stream = File.OpenRead(assemblyPath);
		using var peReader = new PEReader(stream);
		if (!peReader.HasMetadata)
		{
			throw new InvalidDataException($"Assembly has no CLI metadata: {assemblyPath}");
		}

		MetadataReader reader = peReader.GetMetadataReader();
		Dictionary<string, string> metadata = ReadAssemblyMetadata(reader);
		List<string> forbiddenMembers = ReadForbiddenPublicMembers(reader);
		List<string> harnessTypes = ReadHarnessTypes(reader);
		List<string> issues = new();

		RequireMetadata(metadata, "DecklandBuildKind", options.ExpectedBuildKind, issues);
		RequireMetadata(metadata, "Shipping", FormatBoolean(options.ExpectedShipping), issues);
		RequireMetadata(metadata, "TestHooks", FormatBoolean(options.ExpectedTestHooks), issues);

		if (options.ExpectedShipping)
		{
			if (forbiddenMembers.Count > 0)
			{
				issues.Add($"Shipping assembly exposes {forbiddenMembers.Count} Debug/test members.");
			}
			if (harnessTypes.Count > 0)
			{
				issues.Add($"Shipping assembly contains {harnessTypes.Count} Harness types.");
			}
		}
		else if (options.RequireTestSurface && forbiddenMembers.Count == 0 && harnessTypes.Count == 0)
		{
			issues.Add("Validation assembly does not contain any test surface.");
		}

		return new AssemblyAuditReport(
			Ok: issues.Count == 0,
			AssemblyPath: assemblyPath,
			Sha256: Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assemblyPath))).ToLowerInvariant(),
			Metadata: metadata,
			ForbiddenPublicMembers: forbiddenMembers,
			HarnessTypes: harnessTypes,
			Issues: issues);
	}

	private static Dictionary<string, string> ReadAssemblyMetadata(MetadataReader reader)
	{
		var result = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (CustomAttributeHandle handle in reader.GetAssemblyDefinition().GetCustomAttributes())
		{
			CustomAttribute attribute = reader.GetCustomAttribute(handle);
			if (!IsAssemblyMetadataAttribute(reader, attribute.Constructor))
			{
				continue;
			}

			BlobReader valueReader = reader.GetBlobReader(attribute.Value);
			if (valueReader.ReadUInt16() != 1)
			{
				continue;
			}
			string? key = valueReader.ReadSerializedString();
			string? value = valueReader.ReadSerializedString();
			if (!string.IsNullOrEmpty(key))
			{
				result[key] = value ?? string.Empty;
			}
		}
		return result;
	}

	private static bool IsAssemblyMetadataAttribute(MetadataReader reader, EntityHandle constructor)
	{
		EntityHandle declaringType = constructor.Kind switch
		{
			HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle)constructor).Parent,
			HandleKind.MethodDefinition => reader.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType(),
			_ => default,
		};
		return TryReadTypeName(reader, declaringType, out string typeNamespace, out string typeName)
			&& typeNamespace == AssemblyMetadataAttributeNamespace
			&& typeName == AssemblyMetadataAttributeName;
	}

	private static bool TryReadTypeName(
		MetadataReader reader,
		EntityHandle handle,
		out string typeNamespace,
		out string typeName)
	{
		switch (handle.Kind)
		{
			case HandleKind.TypeReference:
				TypeReference reference = reader.GetTypeReference((TypeReferenceHandle)handle);
				typeNamespace = reader.GetString(reference.Namespace);
				typeName = reader.GetString(reference.Name);
				return true;
			case HandleKind.TypeDefinition:
				TypeDefinition definition = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
				typeNamespace = reader.GetString(definition.Namespace);
				typeName = reader.GetString(definition.Name);
				return true;
			default:
				typeNamespace = string.Empty;
				typeName = string.Empty;
				return false;
		}
	}

	private static List<string> ReadForbiddenPublicMembers(MetadataReader reader)
	{
		var result = new List<string>();
		foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions)
		{
			TypeDefinition type = reader.GetTypeDefinition(typeHandle);
			string typeName = QualifiedTypeName(reader, type);
			foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
			{
				MethodDefinition method = reader.GetMethodDefinition(methodHandle);
				string memberName = reader.GetString(method.Name);
				if (IsPublic(method.Attributes) && IsForbiddenMemberName(memberName))
				{
					result.Add($"{typeName}.{memberName}()");
				}
			}
			foreach (FieldDefinitionHandle fieldHandle in type.GetFields())
			{
				FieldDefinition field = reader.GetFieldDefinition(fieldHandle);
				string memberName = reader.GetString(field.Name);
				if (IsPublic(field.Attributes) && IsForbiddenMemberName(memberName))
				{
					result.Add($"{typeName}.{memberName}");
				}
			}
			foreach (PropertyDefinitionHandle propertyHandle in type.GetProperties())
			{
				PropertyDefinition property = reader.GetPropertyDefinition(propertyHandle);
				string memberName = reader.GetString(property.Name);
				PropertyAccessors accessors = property.GetAccessors();
				if (IsForbiddenMemberName(memberName)
					&& (IsPublic(reader, accessors.Getter) || IsPublic(reader, accessors.Setter)))
				{
					result.Add($"{typeName}.{memberName}");
				}
			}
		}
		result.Sort(StringComparer.Ordinal);
		return result;
	}

	private static List<string> ReadHarnessTypes(MetadataReader reader)
	{
		var result = new List<string>();
		foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
		{
			TypeDefinition type = reader.GetTypeDefinition(handle);
			string typeName = reader.GetString(type.Name);
			if (typeName.EndsWith("Harness", StringComparison.Ordinal))
			{
				result.Add(QualifiedTypeName(reader, type));
			}
		}
		result.Sort(StringComparer.Ordinal);
		return result;
	}

	private static string QualifiedTypeName(MetadataReader reader, TypeDefinition type)
	{
		string typeNamespace = reader.GetString(type.Namespace);
		string typeName = reader.GetString(type.Name);
		return string.IsNullOrEmpty(typeNamespace) ? typeName : $"{typeNamespace}.{typeName}";
	}

	private static bool IsForbiddenMemberName(string memberName)
	{
		return memberName.StartsWith("Debug", StringComparison.Ordinal)
			|| memberName.StartsWith("debug_", StringComparison.Ordinal);
	}

	private static bool IsPublic(MethodAttributes attributes)
	{
		return (attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;
	}

	private static bool IsPublic(FieldAttributes attributes)
	{
		return (attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public;
	}

	private static bool IsPublic(MetadataReader reader, MethodDefinitionHandle handle)
	{
		return !handle.IsNil && IsPublic(reader.GetMethodDefinition(handle).Attributes);
	}

	private static void RequireMetadata(
		IReadOnlyDictionary<string, string> metadata,
		string key,
		string expectedValue,
		ICollection<string> issues)
	{
		if (!metadata.TryGetValue(key, out string? actualValue))
		{
			issues.Add($"Assembly metadata is missing {key}={expectedValue}.");
		}
		else if (!string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase))
		{
			issues.Add($"Assembly metadata {key} is {actualValue}, expected {expectedValue}.");
		}
	}

	private static string FormatBoolean(bool value) => value ? "true" : "false";

	private sealed record AuditOptions(
		string AssemblyPath,
		string ExpectedBuildKind,
		bool ExpectedShipping,
		bool ExpectedTestHooks,
		bool RequireTestSurface,
		string? ReportPath)
	{
		public static AuditOptions Parse(string[] args)
		{
			var values = new Dictionary<string, string>(StringComparer.Ordinal);
			var flags = new HashSet<string>(StringComparer.Ordinal);
			for (int index = 0; index < args.Length; index++)
			{
				string argument = args[index];
				if (argument == "--require-test-surface")
				{
					flags.Add(argument);
					continue;
				}
				if (!argument.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
				{
					throw new ArgumentException($"Invalid assembly audit argument: {argument}");
				}
				values[argument] = args[++index];
			}

			return new AuditOptions(
				Required(values, "--assembly"),
				Required(values, "--expected-build-kind"),
				ParseBoolean(values, "--expected-shipping"),
				ParseBoolean(values, "--expected-test-hooks"),
				flags.Contains("--require-test-surface"),
				values.GetValueOrDefault("--report"));
		}

		private static string Required(IReadOnlyDictionary<string, string> values, string key)
		{
			return values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
				? value
				: throw new ArgumentException($"Missing required argument {key}.");
		}

		private static bool ParseBoolean(IReadOnlyDictionary<string, string> values, string key)
		{
			return bool.TryParse(Required(values, key), out bool value)
				? value
				: throw new ArgumentException($"{key} must be true or false.");
		}
	}

	private sealed record AssemblyAuditReport(
		bool Ok,
		string AssemblyPath,
		string Sha256,
		IReadOnlyDictionary<string, string> Metadata,
		IReadOnlyList<string> ForbiddenPublicMembers,
		IReadOnlyList<string> HarnessTypes,
		IReadOnlyList<string> Issues);
}
