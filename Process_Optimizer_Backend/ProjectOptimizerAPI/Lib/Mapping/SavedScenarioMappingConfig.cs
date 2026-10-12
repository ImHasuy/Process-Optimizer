using Mapster;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Mapping
{
	public sealed class SavedScenarioMappingConfig : IRegister
	{
		public const string CurrentIndexParameter = "currentIndex";

		public void Register(TypeAdapterConfig config)
		{
			config.NewConfig<SavedScenario, SavedScenarioListItemDTO>()
				.Ignore(destination => destination.IsAvailable);

			config.NewConfig<SavedScenario, SavedScenarioDTO>()
				.Map(destination => destination.CurrentIndex, source => CurrentIndex())
				.Map(destination => destination.IsAvailable, source => CurrentIndex().HasValue)
				.Map(destination => destination.Snapshot, source => ScenarioSnapshotSerializer.Deserialize(source.SnapshotJson));
		}

		private static long? CurrentIndex() =>
			MapContext.Current?.Parameters.GetValueOrDefault(CurrentIndexParameter) as long?;
	}
}
