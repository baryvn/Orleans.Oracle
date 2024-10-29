using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace Orleans.Reminders.Oracle
{
    public static class OracleHostingExtensions
    {

        public static ISiloBuilder UseOracleReminder(this ISiloBuilder builder)
        {
            return builder.ConfigureServices(services =>
            {
                services.AddReminders();
                services.AddOptions<OracleReminderStorageOptions>();
                services.AddSingleton<IReminderTable, OracleReminderTable>();
            });
        }

        public static IServiceCollection UseOracleReminder(this IServiceCollection services)
        {
            services.AddReminders();
            services.AddSingleton<IReminderTable, OracleReminderTable>();
            return services;
        }

    }
}
