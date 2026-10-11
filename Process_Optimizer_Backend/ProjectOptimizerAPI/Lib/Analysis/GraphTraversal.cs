namespace ProjectOptimizerAPI.Lib.Analysis
{
	public static class GraphTraversal
	{
		public static HashSet<string> CollectReachable(IEnumerable<string> seedIds, ILookup<string, string> neighbours)
		{
			var reachedIds = new HashSet<string>(seedIds, StringComparer.Ordinal);
			var pending = new Queue<string>(reachedIds);
			while (pending.TryDequeue(out var currentId))
			{
				foreach (var neighbourId in neighbours[currentId].Where(reachedIds.Add))
				{
					pending.Enqueue(neighbourId);
				}
			}
			return reachedIds;
		}
	}
}
