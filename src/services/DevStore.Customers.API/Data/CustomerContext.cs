using System.Linq;
using System.Threading.Tasks;
using DevStore.Core.Data;
using DevStore.Core.DomainObjects;
using DevStore.Core.Messages;
using DevStore.Customers.API.Models;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DevStore.Customers.API.Data
{
    public sealed class CustomerContext : DbContext, IUnitOfWork
    {
        //private readonly IMediatorHandler _mediatorHandler;
        private readonly IMediator _mediator;

        public CustomerContext(DbContextOptions<CustomerContext> options, IMediator mediator)
            : base(options)
        {
            _mediator = mediator;
            ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            ChangeTracker.AutoDetectChangesEnabled = false;
        }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<Address> Addresses { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Ignore<ValidationResult>();
            modelBuilder.Ignore<Event>();

            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(
                e => e.GetProperties().Where(p => p.ClrType == typeof(string))))
                property.SetColumnType("varchar(100)");

            foreach (var relationship in modelBuilder.Model.GetEntityTypes()
                .SelectMany(e => e.GetForeignKeys())) relationship.DeleteBehavior = DeleteBehavior.ClientSetNull;

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomerContext).Assembly);
        }

        public async Task<bool> Commit()
        {
            var sucesso = await base.SaveChangesAsync() > 0;
            if (sucesso) await _mediator.PublicarEventos(this);

            return sucesso;
        }
    }

    public static class MediatorExtension
    {
        public static async Task PublicarEventos<T>(this IMediator mediator, T ctx) where T : DbContext
        {
            var domainEntities = ctx.ChangeTracker
                .Entries<Entity>()
                .Where(x => x.Entity.Notificacoes != null && x.Entity.Notificacoes.Any());

            var domainEvents = domainEntities
                .SelectMany(x => x.Entity.Notificacoes)
                .ToList();

            domainEntities.ToList()
                .ForEach(entity => entity.Entity.ClearEvents());

            //var tasks = domainEvents
            //    .Select(async (domainEvent) => {
            //        await mediator.Publish(domainEvent);
            //    });

            foreach (var task in domainEvents)
            {
                await mediator.Publish(task);
            }
        }
    }
}