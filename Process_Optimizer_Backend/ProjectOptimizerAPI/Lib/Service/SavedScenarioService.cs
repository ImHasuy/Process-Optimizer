using Mapster;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerAPI.Lib.Mapping;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Service
{
	public sealed class SavedScenarioService(IUnitOfWork unitOfWork, IProjectAccessGuard projectAccessGuard, IScenarioCatalog scenarioCatalog, IDiagramAnalyzer diagramAnalyzer, IScenarioBuilder scenarioBuilder, TimeProvider timeProvider) : ISavedScenarioService
	{
		private const string DuplicateNameMessage = "A saved scenario with this name already exists.";

		public async Task<SavedScenarioDTO> CreateSavedScenarioAsync(Guid projectId, SavedScenarioCreateDTO request)
		{
			var indexed = await scenarioCatalog.GetIndexedDiagramAsync(projectId);
			var index = request.ScenarioIndex!.Value;
			EnsureIndexIsWithinCount(indexed, index);
			var normalizedName = NameNormalizer.Normalize(request.Name);
			await EnsureNameIsAvailableAsync(projectId, normalizedName, null);

			var snapshot = BuildSnapshot(indexed, index);
			var savedScenario = CreateEntity(projectId, request, normalizedName, snapshot);
			unitOfWork.SavedScenarioRepository.Add(savedScenario);
			await unitOfWork.SaveChangesOrConflictAsync(DuplicateNameMessage);

			return await GetSavedScenarioAsync(projectId, savedScenario.Id);
		}

		public async Task<List<SavedScenarioListItemDTO>> ListSavedScenariosAsync(Guid projectId)
		{
			var ranker = await scenarioCatalog.CreateRankerAsync(projectId);
			var savedScenarios = await unitOfWork.SavedScenarioRepository.ListForProjectAsync(projectId);
			return savedScenarios
				.Select(savedScenario => savedScenario with { IsAvailable = ranker(savedScenario.ScenarioKey).HasValue })
				.ToList();
		}

		public async Task<SavedScenarioDTO> GetSavedScenarioAsync(Guid projectId, Guid id)
		{
			var ranker = await scenarioCatalog.CreateRankerAsync(projectId);
			var savedScenario = await GetRequiredSavedScenarioAsync(projectId, id);
			return MapWithCurrentIndex(savedScenario, ranker);
		}

		public async Task UpdateSavedScenarioAsync(Guid projectId, Guid id, SavedScenarioUpdateDTO request)
		{
			await projectAccessGuard.EnsureExistsAsync(projectId);
			var savedScenario = await GetRequiredSavedScenarioAsync(projectId, id);
			var normalizedName = NameNormalizer.Normalize(request.Name);
			await EnsureNameIsAvailableAsync(projectId, normalizedName, id);

			savedScenario.Name = request.Name.Trim();
			savedScenario.NormalizedName = normalizedName;
			savedScenario.Note = request.Note;
			await unitOfWork.SaveChangesOrConflictAsync(DuplicateNameMessage);
		}

		public async Task DeleteSavedScenarioAsync(Guid projectId, Guid id)
		{
			await projectAccessGuard.EnsureExistsAsync(projectId);
			var savedScenario = await GetRequiredSavedScenarioAsync(projectId, id);
			unitOfWork.SavedScenarioRepository.Remove(savedScenario);
			await unitOfWork.SaveChangesAsync();
		}

		private ScenarioDTO BuildSnapshot(IndexedDiagram indexed, long index)
		{
			var computation = diagramAnalyzer.GetScenario(indexed.Analysis, index);
			return scenarioBuilder.ToDto(computation, indexed.Diagram.Context, indexed.Count);
		}

		private SavedScenario CreateEntity(Guid projectId, SavedScenarioCreateDTO request, string normalizedName, ScenarioDTO snapshot) => new()
		{
			Id = Guid.NewGuid(),
			ProjectId = projectId,
			Name = request.Name.Trim(),
			NormalizedName = normalizedName,
			Note = request.Note,
			ScenarioIndex = snapshot.Index,
			OrderingVersion = snapshot.OrderingVersion,
			ScenarioKey = snapshot.Key,
			SnapshotJson = ScenarioSnapshotSerializer.Serialize(snapshot),
			TotalDuration = snapshot.TotalDuration,
			TotalCost = snapshot.TotalCost,
			CreatedAt = timeProvider.UtcNow()
		};

		private static SavedScenarioDTO MapWithCurrentIndex(SavedScenario savedScenario, Func<string, long?> ranker)
		{
			var adapter = savedScenario.BuildAdapter();
			if (ranker(savedScenario.ScenarioKey) is long currentIndex)
			{
				adapter = adapter.AddParameters(SavedScenarioMappingConfig.CurrentIndexParameter, currentIndex);
			}

			return adapter.AdaptToType<SavedScenarioDTO>();
		}

		private async Task<SavedScenario> GetRequiredSavedScenarioAsync(Guid projectId, Guid id) =>
			await unitOfWork.SavedScenarioRepository.FindForProjectAsync(projectId, id)
			?? throw NotFoundException.For<SavedScenario>(id);

		private static void EnsureIndexIsWithinCount(IndexedDiagram indexed, long index)
		{
			if (!indexed.Contains(index))
			{
				throw new InvalidRequestException(ErrorMessages.ScenarioNotFound);
			}
		}

		private async Task EnsureNameIsAvailableAsync(Guid projectId, string normalizedName, Guid? excludedId)
		{
			if (await unitOfWork.SavedScenarioRepository.IsNameTakenAsync(projectId, normalizedName, excludedId))
			{
				throw new ConflictException(DuplicateNameMessage);
			}
		}
	}
}
