using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IAnalysisService
	{
		Task<ProcessGraphDTO> GetGraphAsync(Guid projectId, int? revision);
		Task<ValidationReportDTO> GetValidationReportAsync(Guid projectId, int? revision);
	}
}
