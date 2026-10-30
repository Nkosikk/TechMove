using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TechMove.Api.Models;

namespace TechMove.Api.Data;

public class TechMoveDbContext(DbContextOptions<TechMoveDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Client>()
            .HasMany(client => client.Contracts)
            .WithOne(contract => contract.Client)
            .HasForeignKey(contract => contract.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Client>()
            .HasOne(client => client.Owner)
            .WithMany()
            .HasForeignKey(client => client.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Contract>()
            .HasMany(contract => contract.ServiceRequests)
            .WithOne(serviceRequest => serviceRequest.Contract)
            .HasForeignKey(serviceRequest => serviceRequest.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Contract>()
            .HasOne(contract => contract.Owner)
            .WithMany()
            .HasForeignKey(contract => contract.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Contract>()
            .HasOne(contract => contract.ApprovedBy)
            .WithMany()
            .HasForeignKey(contract => contract.ApprovedById)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ServiceRequest>()
            .HasOne(serviceRequest => serviceRequest.Owner)
            .WithMany()
            .HasForeignKey(serviceRequest => serviceRequest.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ServiceRequest>()
            .HasOne(serviceRequest => serviceRequest.ApprovedBy)
            .WithMany()
            .HasForeignKey(serviceRequest => serviceRequest.ApprovedById)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Contract>()
            .Property(contract => contract.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<ServiceRequest>()
            .Property(serviceRequest => serviceRequest.Status)
            .HasConversion<string>()
            .HasMaxLength(20);
    }
}

