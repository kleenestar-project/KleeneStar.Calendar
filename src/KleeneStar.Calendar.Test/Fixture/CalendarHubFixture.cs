using KleeneStar.Calendar.WebManager;
using KleeneStar.Core;
using KleeneStar.Model;
using KleeneStar.Model.Settings;
using System.Reflection;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Calendar.Test
{
    /// <summary>
    /// Builds the core's managers and the calendar's manager without a host, on an in-memory
    /// database per test.
    /// </summary>
    /// <remarks>
    /// The hubs are process-wide, so the tests that use the fixture run one after the other in
    /// the <c>NonParallelTests</c> collection.
    /// </remarks>
    internal static class CalendarHubFixture
    {
        /// <summary>
        /// Points the model at an in-memory database and builds the managers.
        /// </summary>
        /// <param name="connectionString">The name of the database.</param>
        public static void Initialize(string connectionString)
        {
            KleeneStar.Core.WebManager.ObjectPathIndex.Disable();

            ModelHub.DatabaseSettings = new DatabaseSettings
            {
                Assembly = "KleeneStar.Calendar.Test",
                ConnectionString = connectionString
            };

            // every manager of the core, found by reflection over CoreHub's backing fields
            foreach (var field in typeof(CoreHub).GetFields(BindingFlags.NonPublic | BindingFlags.Static))
            {
                var ctor = field.FieldType.IsClass && !field.FieldType.IsAbstract && typeof(IComponentManager).IsAssignableFrom(field.FieldType)
                    ? field.FieldType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(IComponentHub), typeof(IHttpServerContext)], null)
                    : null;

                if (ctor is not null)
                {
                    field.SetValue(null, ctor.Invoke([null, null]));
                }
            }

            var manager = typeof(AppointmentManager)
                .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(IComponentHub), typeof(IHttpServerContext)], null)?
                .Invoke([null, null])
                ?? throw new InvalidOperationException("Private ctor not found on AppointmentManager.");

            (typeof(CalendarHub).GetField("_appointmentManager", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("Backing field _appointmentManager not found on CalendarHub."))
                .SetValue(null, manager);

            CalendarHub.Configuration = null;
        }

        /// <summary>
        /// Creates a context on the in-memory database.
        /// </summary>
        /// <param name="connectionString">The name of the database.</param>
        /// <returns>The context.</returns>
        public static KleeneStarDbContext CreateDbContext(string connectionString)
        {
            return InMemoryDbContextFactory.Create(connectionString);
        }
    }

    /// <summary>
    /// The tests that share the process-wide hubs.
    /// </summary>
    [CollectionDefinition("NonParallelTests", DisableParallelization = true)]
    public sealed class NonParallelTests
    {
    }
}
