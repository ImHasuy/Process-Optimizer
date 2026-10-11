using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerTests.Infrastructure
{
	public static class ScenarioAssertions
	{
		public static void AssertReference(ScenarioRefDTO? reference, long index, string key)
		{
			Assert.NotNull(reference);
			Assert.Equal(index, reference.Index);
			Assert.Equal(key, reference.Key);
		}
	}
}
