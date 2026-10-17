using Mapster;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Mapping
{
	public sealed class ProjectMappingConfig : IRegister
	{
		public void Register(TypeAdapterConfig config)
		{
			config.NewConfig<Project, ProjectListItemDTO>()
				.Map(destination => destination.IsOptimizable, source => source.Diagram.IsOptimizable)
				.Map(destination => destination.MaxCost, source => source.Diagram.MaxCost)
				.Map(destination => destination.MaxDurationBound, source => source.Diagram.MaxDurationBound);

			config.NewConfig<Project, ProjectDetailsDTO>()
				.Map(destination => destination.MaxCost, source => source.Diagram.MaxCost)
				.Map(destination => destination.MaxDurationBound, source => source.Diagram.MaxDurationBound);
		}
	}
}
