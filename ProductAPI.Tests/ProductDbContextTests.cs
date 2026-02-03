using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProductAPI.Data;
using Xunit;

namespace ProductAPI.Tests
{
    public sealed class ProductDbContextTests
    {
        [Fact]
        public void ProductsDbSet_AllowsCrudOperations()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<ProductDbContext>()
                .UseSqlite(connection)
                .Options;

            using (var context = new ProductDbContext(options))
            {
                context.Database.EnsureCreated();

                context.Products.Add(new Product
                {
                    Name = "Initial",
                    Description = "Seed",
                    Price = 50,
                    ProductType = ProductType.CPU
                });

                context.SaveChanges();
            }

            using (var context = new ProductDbContext(options))
            {
                var product = context.Products.Single();
                Assert.Equal("Initial", product.Name);

                product.Name = "Updated";
                product.Price = 75;
                context.SaveChanges();
            }

            using (var context = new ProductDbContext(options))
            {
                var updated = context.Products.Single();
                Assert.Equal("Updated", updated.Name);
                Assert.Equal(75, updated.Price);

                context.Products.Remove(updated);
                context.SaveChanges();
            }

            using (var context = new ProductDbContext(options))
            {
                Assert.Empty(context.Products);
            }
        }

        [Fact]
        public void ProductsDbSet_UsesPrimaryKeyForLookup()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<ProductDbContext>()
                .UseSqlite(connection)
                .Options;

            int id;

            using (var context = new ProductDbContext(options))
            {
                context.Database.EnsureCreated();

                var product = new Product
                {
                    Name = "Lookup",
                    Description = "Find me",
                    Price = 10,
                    ProductType = ProductType.MONITOR
                };

                context.Products.Add(product);
                context.SaveChanges();
                id = product.Id;
            }

            using (var context = new ProductDbContext(options))
            {
                var found = context.Products.Find(id);
                Assert.NotNull(found);
                Assert.Equal("Lookup", found!.Name);
            }
        }
    }
}
