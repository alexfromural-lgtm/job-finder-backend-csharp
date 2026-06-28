using Npgsql;

namespace JobFinder.Api.Utils
{
    /// <summary>
    /// Custom Npgsql name translator that bridges the naming gap between C# enums
    /// and the PostgreSQL enum types created by Prisma.
    ///
    /// Prisma creates PostgreSQL enum TYPE names in PascalCase (e.g. "Role", "ApplicationStatus")
    /// but stores enum LABELS in lowercase (e.g. "recruiter", "job_seeker").
    ///
    /// The default NpgsqlSnakeCaseNameTranslator converts everything to snake_case, breaking
    /// the PascalCase type name match. The NpgsqlNullNameTranslator preserves case exactly,
    /// which fixes the type name but breaks value matching (C# "RECRUITER" ≠ PostgreSQL "recruiter").
    ///
    /// This translator applies the correct rule for each context:
    ///   - TranslateTypeName  → preserve as-is (PascalCase matches Prisma type names)
    ///   - TranslateMemberName → lowercase     (matches Prisma's stored enum labels)
    /// </summary>
    public class NpgsqlPrismaNameTranslator : INpgsqlNameTranslator
    {
        public static readonly NpgsqlPrismaNameTranslator Instance = new();

        /// <summary>
        /// Enum type name: preserve exactly as the C# class name.
        /// e.g. C# "Role" → PostgreSQL type "Role"
        /// </summary>
        public string TranslateTypeName(string clrName) => clrName;

        /// <summary>
        /// Enum member/value name: convert to lowercase invariant.
        /// e.g. C# "RECRUITER"   → PostgreSQL label "recruiter"
        ///      C# "JOB_SEEKER"  → PostgreSQL label "job_seeker"
        ///      C# "submitted"   → PostgreSQL label "submitted"  (unchanged)
        /// </summary>
        public string TranslateMemberName(string clrName) => clrName.ToLowerInvariant();
    }
}
