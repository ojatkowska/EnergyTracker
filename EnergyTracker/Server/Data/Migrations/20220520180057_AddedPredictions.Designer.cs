﻿// <auto-generated />
using System;
using EnergyTracker.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace EnergyTracker.Server.Data.Migrations
{
    [DbContext(typeof(DatabaseContext))]
    [Migration("20220520180057_AddedPredictions")]
    partial class AddedPredictions
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder, 1L, 1);

            modelBuilder.Entity("EnergyTracker.Shared.Models.Forecast", b =>
                {
                    b.Property<DateTime>("Date")
                        .HasColumnType("datetime2");

                    b.Property<int>("Humidity")
                        .HasColumnType("int");

                    b.Property<bool>("IsWorkingDay")
                        .HasColumnType("bit");

                    b.Property<double>("Pressure")
                        .HasColumnType("float");

                    b.Property<double>("Temperature")
                        .HasColumnType("float");

                    b.Property<int>("WindSpeed")
                        .HasColumnType("int");

                    b.HasKey("Date");

                    b.ToTable("Forecasts");
                });

            modelBuilder.Entity("EnergyTracker.Shared.Models.Kse", b =>
                {
                    b.Property<DateTime>("Date")
                        .HasColumnType("datetime2");

                    b.Property<int>("Power")
                        .HasColumnType("int");

                    b.HasKey("Date");

                    b.ToTable("Kses");
                });

            modelBuilder.Entity("EnergyTracker.Shared.Models.Prediction", b =>
                {
                    b.Property<DateTime>("Date")
                        .HasColumnType("datetime2");

                    b.Property<int>("Power")
                        .HasColumnType("int");

                    b.HasKey("Date");

                    b.ToTable("Predictions");
                });

            modelBuilder.Entity("EnergyTracker.Shared.Models.Weather", b =>
                {
                    b.Property<DateTime>("Date")
                        .HasColumnType("datetime2");

                    b.Property<int>("Humidity")
                        .HasColumnType("int");

                    b.Property<bool>("IsWorkingDay")
                        .HasColumnType("bit");

                    b.Property<double>("Pressure")
                        .HasColumnType("float");

                    b.Property<double>("Temperature")
                        .HasColumnType("float");

                    b.Property<int>("WindSpeed")
                        .HasColumnType("int");

                    b.HasKey("Date");

                    b.ToTable("Weathers");
                });
#pragma warning restore 612, 618
        }
    }
}
