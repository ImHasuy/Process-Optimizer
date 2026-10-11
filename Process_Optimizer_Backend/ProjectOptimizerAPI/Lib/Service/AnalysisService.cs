using Mapster;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerAPI.Lib.Mapping;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Lib.Service
{
	public sealed class AnalysisService(IScenarioCatalog scenarioCatalog) : IAnalysisService
	{
		public async Task<ProcessGraphDTO> GetGraphAsync(Guid projectId, int? revision)
		{
			var analyzed = await scenarioCatalog.AnalyzeRevisionAsync(projectId, revision);
			return MapWithRevisionNumber<ProcessGraph, ProcessGraphDTO>(analyzed.Analysis.Graph, analyzed.Revision.Context.RevisionNumber);
		}

		public async Task<ValidationReportDTO> GetValidationReportAsync(Guid projectId, int? revision)
		{
			var analyzed = await scenarioCatalog.AnalyzeRevisionAsync(projectId, revision);
			return MapWithRevisionNumber<DiagramAnalysis, ValidationReportDTO>(analyzed.Analysis, analyzed.Revision.Context.RevisionNumber);
		}

		private static TDestination MapWithRevisionNumber<TSource, TDestination>(TSource source, int revisionNumber) =>
			source.BuildAdapter()
				.AddParameters(AnalysisMappingConfig.RevisionNumberParameter, revisionNumber)
				.AdaptToType<TDestination>();
	}
}
