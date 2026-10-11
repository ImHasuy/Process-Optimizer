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
			var indexedRevision = await scenarioCatalog.GetIndexedRevisionAsync(projectId, request.Revision);
			var index = request.ScenarioIndex!.Value;
			EnsureIndexIsWithinCount(indexedRevision, index);
			var normalizedName = NameNormalizer.Normalize(request.Name);
			await EnsureNameIsAvailableAsync(projectId, normalizedName, null);

			var snapshot = BuildSnapshot(indexedRevision, index);
			var savedScenario = CreateEntity(projectId, request, normalizedName, snapshot);
			unitOfWork.SavedScenarioRepository.Add(savedScenario);
			await unitOfWork.SaveChangesOrConflictAsync(DuplicateNameMessage);

			return await GetSavedScenarioAsync(projectId, savedScenario.Id);
		}

		public async Task<List<SavedScenarioListItemDTO>> ListSavedScenariosAsync(Guid projectId)
		{
			var ranker = await scenarioCatalog.CreateLatestRevisionRankerAsync(projectId);
			var savedScenarios = await unitOfWork.SavedScenarioRepository.ListForProjectAsync(projectId);
			return savedScenarios
				.Select(savedScenario => savedScenario with { IsAvailableInLatestRevision = ranker(savedScenario.ScenarioKey).HasValue })
				.ToList();
		}

		public async Task<SavedScenarioDTO> GetSavedScenarioAsync(Guid projectId, Guid id)
		{
			var ranker = await scenarioCatalog.CreateLatestRevisionRankerAsync(projectId);
			var savedScenario = await GetRequiredSavedScenarioAsync(projectId, id);
			return MapWithLatestIndex(savedScenario, ranker);
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

		private ScenarioDTO BuildSnapshot(IndexedRevision indexedRevision, long index)
		{
			var computation = diagramAnalyzer.GetScenario(indexedRevision.Analysis, index);
			return scenarioBuilder.ToDto(computation, indexedRevision.Revision.Context, indexedRevision.Count);
		}

		private SavedScenario CreateEntity(Guid projectId, SavedScenarioCreateDTO request, string normalizedName, ScenarioDTO snapshot) => new()
		{
			Id = Guid.NewGuid(),
			ProjectId = projectId,
			Name = request.Name.Trim(),
			NormalizedName = normalizedName,
			Note = request.Note,
			RevisionNumber = snapshot.RevisionNumber,
			ScenarioIndex = snapshot.Index,
			OrderingVersion = snapshot.OrderingVersion,
			ScenarioKey = snapshot.Key,
			SnapshotJson = ScenarioSnapshotSerializer.Serialize(snapshot),
			TotalDuration = snapshot.TotalDuration,
			TotalCost = snapshot.TotalCost,
			CreatedAt = timeProvider.UtcNow()
		};

		private static SavedScenarioDTO MapWithLatestIndex(SavedScenario savedScenario, Func<string, long?> ranker)
		{
			var adapter = savedScenario.BuildAdapter();
			if (ranker(savedScenario.ScenarioKey) is long latestRevisionIndex)
			{
				adapter = adapter.AddParameters(SavedScenarioMappingConfig.LatestRevisionIndexParameter, latestRevisionIndex);
			}

			return adapter.AdaptToType<SavedScenarioDTO>();
		}

		private async Task<SavedScenario> GetRequiredSavedScenarioAsync(Guid projectId, Guid id) =>
			await unitOfWork.SavedScenarioRepository.FindForProjectAsync(projectId, id)
			?? throw NotFoundException.For<SavedScenario>(id);

		private static void EnsureIndexIsWithinCount(IndexedRevision indexedRevision, long index)
		{
			if (!indexedRevision.Contains(index))
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
