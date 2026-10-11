using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Lib.Service
{
	public sealed class ProjectService(IUnitOfWork unitOfWork, IProjectAccessGuard projectAccessGuard, IDiagramRevisionFactory diagramRevisionFactory, TimeProvider timeProvider) : IProjectService
	{
		private const string NameTakenMessage = "A project with this name already exists.";
		private const string CopySuffix = " (copy)";

		public Task<PagedResultDTO<ProjectListItemDTO>> ListProjectsAsync(ProjectFilterDTO filter) =>
			unitOfWork.ProjectRepository.ListAsync(filter, PageRequest.Resolve(filter.Page, filter.PageSize));

		public Task<ProjectDetailsDTO> GetProjectAsync(Guid id) => GetDetailsAsync(id);

		public async Task<ProjectDetailsDTO> CreateProjectAsync(ProjectCreateDTO request)
		{
			var name = request.Name.Trim();
			await EnsureNameIsAvailableAsync(NameNormalizer.Normalize(name), null);

			var project = ProjectFactory.Create(name, request.Description, request.TimeUnit ?? ProjectFactory.DefaultTimeUnit, request.Currency ?? ProjectFactory.DefaultCurrency, timeProvider.UtcNow());
			ProjectFactory.AddRevisions(project, diagramRevisionFactory, DefaultDiagram.Xml);
			unitOfWork.ProjectRepository.Add(project);
			await unitOfWork.SaveChangesOrConflictAsync(NameTakenMessage);

			return await GetDetailsAsync(project.Id);
		}

		public async Task UpdateProjectAsync(Guid id, ProjectUpdateDTO request)
		{
			var project = await projectAccessGuard.GetProjectAsync(id);
			var name = request.Name.Trim();
			var normalizedName = NameNormalizer.Normalize(name);
			await EnsureNameIsAvailableAsync(normalizedName, id);

			project.Name = name;
			project.NormalizedName = normalizedName;
			project.Description = request.Description;
			project.TimeUnit = request.TimeUnit!.Value;
			project.Currency = request.Currency!;
			project.UpdatedAt = timeProvider.UtcNow();
			await unitOfWork.SaveChangesOrConflictAsync(NameTakenMessage);
		}

		public async Task DeleteProjectAsync(Guid id)
		{
			var project = await projectAccessGuard.GetProjectAsync(id);
			unitOfWork.ProjectRepository.Remove(project);
			await unitOfWork.SaveChangesAsync();
		}

		public async Task<ProjectDetailsDTO> CopyProjectAsync(Guid id)
		{
			var source = await projectAccessGuard.GetProjectAsync(id);
			var copyName = await FindAvailableCopyNameAsync(source.Name);
			var bpmnXml = await unitOfWork.ProjectRepository.GetLatestBpmnXmlAsync(id);

			var copy = ProjectFactory.Create(copyName, source.Description, source.TimeUnit, source.Currency, timeProvider.UtcNow());
			ProjectFactory.AddRevisions(copy, diagramRevisionFactory, bpmnXml);
			unitOfWork.ProjectRepository.Add(copy);
			await unitOfWork.SaveChangesOrConflictAsync(NameTakenMessage);

			return await GetDetailsAsync(copy.Id);
		}

		private async Task EnsureNameIsAvailableAsync(string normalizedName, Guid? excludedProjectId)
		{
			if (await unitOfWork.ProjectRepository.IsNameTakenAsync(normalizedName, excludedProjectId))
			{
				throw new ConflictException(NameTakenMessage);
			}
		}

		private async Task<string> FindAvailableCopyNameAsync(string sourceName)
		{
			for (var copyNumber = 1; ; copyNumber++)
			{
				var suffix = copyNumber == 1 ? CopySuffix : $"{CopySuffix} {copyNumber}";
				var candidate = sourceName[..Math.Min(sourceName.Length, FieldLimits.NameMaxLength - suffix.Length)] + suffix;
				if (!await unitOfWork.ProjectRepository.IsNameTakenAsync(NameNormalizer.Normalize(candidate), null))
				{
					return candidate;
				}
			}
		}

		private Task<ProjectDetailsDTO> GetDetailsAsync(Guid id) => unitOfWork.ProjectRepository.GetRequiredAsync<ProjectDetailsDTO>(id);
	}
}
