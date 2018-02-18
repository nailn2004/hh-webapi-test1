using Microsoft.EntityFrameworkCore;
using System;

namespace VacancyLoader.Models
{
    public class VacancyDbContext : DbContext
    {
        public VacancyDbContext(DbContextOptions<VacancyDbContext> options)
            : base(options)
        {
            Database.EnsureCreated();
        }

        public DbSet<Vacancy> Vacancy { get; set; }
    }
}