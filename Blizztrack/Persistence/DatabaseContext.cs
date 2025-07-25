﻿using Blizztrack.Framework.TACT;
using Blizztrack.Persistence.Entities;
using Blizztrack.Persistence.Translators;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Blizztrack.Persistence
{
    using Endpoint = Entities.Endpoint;

    public class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options)
    {
        public DbSet<ProductConfig> Configs { get; private set; }
        public DbSet<Product> Products { get; private set; }
        public DbSet<Endpoint> Endpoints { get; private set; }
        public DbSet<KnownResource> KnownResources { get; private set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>()
                .HasMany(e => e.Endpoints)
                .WithMany(e => e.Products);

            modelBuilder.Entity<ProductConfig>(e => e.HasIndex(nameof(ProductConfig.BuildConfig),
                nameof(ProductConfig.CDNConfig),
                nameof(ProductConfig.KeyRing),
                nameof(ProductConfig.Config),
                nameof(ProductConfig.Name),
                nameof(ProductConfig.BuildID)).IsUnique(true));

            modelBuilder.Entity<KnownResource>().HasIndex(e => e.EncodingKey).HasMethod("hash");
            modelBuilder.Entity<KnownResource>().HasIndex(e => e.ContentKey).HasMethod("hash");
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);

            configurationBuilder.Properties<EncodingKey>().HaveConversion<EncodingKeyConverter>();
            configurationBuilder.Properties<ContentKey>().HaveConversion<ContentKeyConverter>();
        }

        private class EncodingKeyConverter() : ValueConverter<EncodingKey, byte[]>(v => v.AsSpan().ToArray(), v => new(v));
        private class ContentKeyConverter() : ValueConverter<ContentKey, byte[]>(v => v.AsSpan().ToArray(), v => new(v));
    }
}
