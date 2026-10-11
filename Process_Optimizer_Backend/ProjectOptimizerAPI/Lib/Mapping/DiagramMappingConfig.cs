using Mapster;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Mapping
{
	public sealed class DiagramMappingConfig : IRegister
	{
		public void Register(TypeAdapterConfig config)
		{
			config.NewConfig<DiagramRevision, DiagramRevisionDTO>();
			config.NewConfig<DiagramRevision, DiagramRevisionListItemDTO>();
		}
	}
}
