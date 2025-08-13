using Microsoft.EntityFrameworkCore;
using MBTP.Models;
using System.Data.SqlClient;
using System.Data;

namespace MBTP.Data
{
    public class ApplicationDbContext : DbContext{
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
        {
        }
        public DbSet<Booking> Bookings { get; set; }
        //public DbSet<ArcadeTransaction> ArcadeTransactions {get;set;}

        //protected override void OnModelCreating(ModelBuilder modelBuilder)
        //{
            //modelBuilder.Entity<ArcadeTransaction>().HasKey(e => e.PreparedFood);
            //modelBuilder.Entity<ArcadeTransaction>().ToTable("ArcadeTransactions");

            //modelBuilder.Entity<DailyRecord>().HasKey(a => a.Id);
            //modelBuilder.Entity<DailyRecord>().ToTable("DailyRecords");
           // modelBuilder.Entity<DailyRecord>().Property(p => p.PreparedFood);
  //  }

    }
}
