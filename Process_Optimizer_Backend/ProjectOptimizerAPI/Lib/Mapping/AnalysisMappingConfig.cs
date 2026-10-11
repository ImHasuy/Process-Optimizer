using Mapster;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Lib.Mapping
{
	public sealed class AnalysisMappingConfig : IRegister
	{
		public const string RevisionNumberParameter = "revisionNumber";

		public void Register(TypeAdapterConfig config)
		{
			config.NewConfig<ProcessGraph, ProcessGraphDTO>()
				.Map(destination => destination.RevisionNumber, source => RevisionNumberFromContext());

			config.NewConfig<DiagramAnalysis, ValidationReportDTO>()
				.Map(destination => destination.RevisionNumber, source => RevisionNumberFromContext());
		}

		private static int RevisionNumberFromContext() => (int)MapContext.Current!.Parameters[RevisionNumberParameter];
	}
}
