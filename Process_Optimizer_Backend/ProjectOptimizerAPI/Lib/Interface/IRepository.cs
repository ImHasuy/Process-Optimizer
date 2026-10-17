namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IRepository<TEntity> where TEntity : class
	{
		Task<TEntity?> FindAsync(Guid id);
		Task<TEntity> GetRequiredAsync(Guid id);
		Task<bool> ExistsAsync(Guid id);
		Task<TDto> GetRequiredAsync<TDto>(Guid id);
		void Add(TEntity entity);
		void Remove(TEntity entity);
	}
}
