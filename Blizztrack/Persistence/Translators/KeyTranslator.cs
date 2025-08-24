using Blizztrack.Framework.TACT;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

using System.Reflection;

using IKey = Blizztrack.Framework.TACT.IKey;

namespace Blizztrack.Persistence.Translators
{
    public class KeyTranslator(ISqlExpressionFactory sqlExpressionFactory) : IMethodCallTranslator
    {
        private readonly ISqlExpressionFactory _sqlExpressionFactory = sqlExpressionFactory ?? throw new ArgumentNullException(nameof(sqlExpressionFactory));

        public SqlExpression? Translate(SqlExpression? instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            if (method.Name == nameof(IKey.AsHexString) && instance != null)
            {
                return _sqlExpressionFactory.Function(
                    "ENCODE",
                    [instance, _sqlExpressionFactory.Constant("hex")],
                    false,
                    [false, false],
                    typeof(string),
                    null);
            }

            if (method.Name == nameof(EncodingKeyExtensions.SequenceEqual) && arguments.Count == 2)
            {
                return _sqlExpressionFactory.Equal(arguments[0], arguments[1]);
            }

            return null;
        }
    }

    public class CustomTranslators : RelationalMethodCallTranslatorProvider
    {
        public CustomTranslators(RelationalMethodCallTranslatorProviderDependencies dependencies, IEnumerable<IMethodCallTranslator> additionalTranslators)
            : base(dependencies)
        {
            var mySqlTranslator = new KeyTranslator(dependencies.SqlExpressionFactory);
            AddTranslators([mySqlTranslator, ..additionalTranslators]);
        }
    }

    public class DatabaseContextOptionsExtension : IDbContextOptionsExtension
    {
        private DbContextOptionsExtensionInfo? _info;
        public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

        public void ApplyServices(IServiceCollection services)
        {
            services.AddSingleton<IMethodCallTranslatorProvider, CustomTranslators>();
        }

        public void Validate(IDbContextOptions options) { }

        private sealed class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
        {
            public override bool IsDatabaseProvider => false;

            public override int GetServiceProviderHashCode() => 0;

            public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => true;

            public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
                => debugInfo["Blizztrack:Extensions"] = "1";

            public override string LogFragment
                => "using BlizztrackExtensions ";
        }
    }

}
