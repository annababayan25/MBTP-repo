using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MBTP.Services;
using MBTP.Logins;
using Microsoft.AspNetCore.Authentication.Cookies;
using MBTP.Retrieval;

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
            services.AddSingleton<DailyService>();
            services.AddSingleton<DailyBookingsService>();
            services.AddSingleton<OccupancyService>();
            services.AddSingleton<DailyReport>();
            services.AddScoped<OccupancyService>();
            services.AddSingleton<WeatherService>();
            services.AddScoped<LoginClass>();
            services.AddControllersWithViews();
            services.AddSingleton<NewBookService>();
            services.AddSingleton<BookingRepository>();
            services.AddSingleton<TrailerMovesReport>();
            services.AddSingleton<ExpressCheckinsReport>();
            services.AddSingleton(new BookingRepository(Configuration));
            services.AddSingleton<AccessLevelsActions>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<AdministrationService>();
            services.AddSingleton<RetailService>();
            services.AddSingleton<SpecialAddonsService>();
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Home/Login";
                    options.LogoutPath = "/Home/Logout";
                });

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            services.AddRazorPages();
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