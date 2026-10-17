using Mapster;
using Microsoft.EntityFrameworkCore;
using ProjectOptimizerAPI.Lib.Database;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Interface;

namespace ProjectOptimizerAPI.Lib.Repository
{
	public abstract class Repository<TEntity>(ProjectOptimizerDbContext dbContext) : IRepository<TEntity> where TEntity : class
	{
		protected ProjectOptimizerDbContext DbContext { get; } = dbContext;
		protected DbSet<TEntity> Entities => DbContext.Set<TEntity>();

		public Task<TEntity?> FindAsync(Guid id) => Entities.FirstOrDefaultAsync(entity => EF.Property<Guid>(entity, "Id") == id);

		public async Task<TEntity> GetRequiredAsync(Guid id) => await FindAsync(id) ?? throw NotFoundException.For<TEntity>(id);

		public Task<bool> ExistsAsync(Guid id) => Entities.AnyAsync(entity => EF.Property<Guid>(entity, "Id") == id);

		public async Task<TDto> GetRequiredAsync<TDto>(Guid id) =>
			await Entities.AsNoTracking()
				.Where(entity => EF.Property<Guid>(entity, "Id") == id)
				.ProjectToType<TDto>()
				.FirstOrDefaultAsync()
			?? throw NotFoundException.For<TEntity>(id);

		public void Add(TEntity entity) => Entities.Add(entity);

		public void Remove(TEntity entity) => Entities.Remove(entity);
	}
}
