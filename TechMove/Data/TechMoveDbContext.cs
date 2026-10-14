using Microsoft.EntityFrameworkCore;
using TechMove.Models;

namespace TechMove.Data;

public class TechMoveDbContext(DbContextOptions<TechMoveDbContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>()
            .HasMany(client => client.Contracts)
            .WithOne(contract => contract.Client)
            .HasForeignKey(contract => contract.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Contract>()
            .HasMany(contract => contract.ServiceRequests)
            .WithOne(serviceRequest => serviceRequest.Contract)
            .HasForeignKey(serviceRequest => serviceRequest.ContractId)
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
