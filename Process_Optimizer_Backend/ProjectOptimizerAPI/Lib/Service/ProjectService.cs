using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Lib.Service
{
	public sealed class ProjectService(IUnitOfWork unitOfWork, IProjectAccessGuard projectAccessGuard, IDiagramFactory diagramFactory, TimeProvider timeProvider) : IProjectService
	{
		private const string NameTakenMessage = "A project with this name already exists.";

		public Task<List<ProjectListItemDTO>> ListProjectsAsync() => unitOfWork.ProjectRepository.ListAsync();

		public Task<ProjectDetailsDTO> GetProjectAsync(Guid id) => GetDetailsAsync(id);

		public async Task<ProjectDetailsDTO> CreateProjectAsync(ProjectCreateDTO request)
		{
			var name = request.Name.Trim();
			await EnsureNameIsAvailableAsync(NameNormalizer.Normalize(name), null);

			var project = ProjectFactory.Create(name, request.Description, request.TimeUnit ?? ProjectFactory.DefaultTimeUnit, request.Currency ?? ProjectFactory.DefaultCurrency, timeProvider.UtcNow());
			project.Diagram = diagramFactory.Create(project.Id, DefaultDiagram.Xml, project.CreatedAt);
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

		private async Task EnsureNameIsAvailableAsync(string normalizedName, Guid? excludedProjectId)
		{
			if (await unitOfWork.ProjectRepository.IsNameTakenAsync(normalizedName, excludedProjectId))
			{
				throw new ConflictException(NameTakenMessage);
			}
		}

		private Task<ProjectDetailsDTO> GetDetailsAsync(Guid id) => unitOfWork.ProjectRepository.GetRequiredAsync<ProjectDetailsDTO>(id);
	}
}
