using Mapster;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Mapping
{
	public sealed class SavedScenarioMappingConfig : IRegister
	{
		public const string LatestRevisionIndexParameter = "latestRevisionIndex";

		public void Register(TypeAdapterConfig config)
		{
			config.NewConfig<SavedScenario, SavedScenarioListItemDTO>()
				.Ignore(destination => destination.IsAvailableInLatestRevision);

			config.NewConfig<SavedScenario, SavedScenarioDTO>()
				.Map(destination => destination.LatestRevisionIndex, source => LatestRevisionIndex())
				.Map(destination => destination.IsAvailableInLatestRevision, source => LatestRevisionIndex().HasValue)
				.Map(destination => destination.Snapshot, source => ScenarioSnapshotSerializer.Deserialize(source.SnapshotJson));
		}

		private static long? LatestRevisionIndex() =>
			MapContext.Current?.Parameters.GetValueOrDefault(LatestRevisionIndexParameter) as long?;
	}
}
