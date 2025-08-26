using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MBTP.Services;
using MBTP.Logins;
using Microsoft.AspNetCore.Authentication.Cookies;
using MBTP.Retrieval;
using SQLStuff;
using MBTP.Interfaces;

namespace MBTP
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IDatabaseConnectionService, DatabaseConnectionService>();
            services.AddHttpContextAccessor();
            services.AddSingleton<DailyService>();
            services.AddScoped<DailyBookingsService>();
            services.AddScoped<OccupancyService>();
            services.AddScoped<DailyReport>();
            services.AddSingleton<WeatherService>();
            services.AddScoped<LoginClass>();
            services.AddScoped<NewBookService>();
            services.AddScoped<BookingRepository>();
            services.AddScoped<TrailerMovesReport>();
            services.AddScoped<ExpressCheckinsReport>();
            services.AddScoped<AccessLevelsActions>();
            services.AddScoped<AdministrationService>();
            services.AddScoped<RetailService>();
            services.AddScoped<SpecialAddonsService>();
            services.AddScoped<SQLSupport>();
            services.AddScoped<BlackoutService>();

            // Auth
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Home/Login";
                    options.LogoutPath = "/Home/Logout";
                });

            // Session
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            services.AddRazorPages();
            services.AddControllersWithViews();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            var dbService = app.ApplicationServices.GetRequiredService<IDatabaseConnectionService>();
            GenericSupport.GenericRoutines.Initialize(dbService);

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}