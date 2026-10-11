using System.Linq.Expressions;
using Mapster;
using Microsoft.EntityFrameworkCore;
using ProjectOptimizerAPI.Lib.Database;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Repository
{
	public sealed class ProjectRepository(ProjectOptimizerDbContext dbContext) : Repository<Project>(dbContext), IProjectRepository
	{
		public async Task<PagedResultDTO<ProjectListItemDTO>> ListAsync(ProjectFilterDTO filter, PageRequest paging)
		{
			var query = ApplySearch(Entities.AsNoTracking(), filter.Search);
			var totalCount = await query.LongCountAsync();
			var items = paging.Offset >= totalCount ? [] : await ApplySorting(query, filter)
				.Skip((int)paging.Offset)
				.Take(paging.PageSize)
				.ProjectToType<ProjectListItemDTO>()
				.ToListAsync();

			return new PagedResultDTO<ProjectListItemDTO> { Items = items, Page = paging.Page, PageSize = paging.PageSize, TotalCount = totalCount };
		}

		public Task<bool> IsNameTakenAsync(string normalizedName, Guid? excludedProjectId) =>
			Entities.AnyAsync(project => project.NormalizedName == normalizedName && project.Id != excludedProjectId);

		public async Task<string> GetLatestBpmnXmlAsync(Guid projectId) =>
			await Entities.AsNoTracking()
				.Where(project => project.Id == projectId)
				.SelectMany(project => project.Revisions
					.Where(revision => revision.RevisionNumber == project.LatestRevisionNumber)
					.Select(revision => revision.BpmnXml))
				.FirstOrDefaultAsync()
			?? throw NotFoundException.For<Project>(projectId);

		private static IQueryable<Project> ApplySearch(IQueryable<Project> query, string? search)
		{
			if (string.IsNullOrWhiteSpace(search))
			{
				return query;
			}

			var normalizedSearch = NameNormalizer.Normalize(search);
			return query.Where(project => project.NormalizedName.Contains(normalizedSearch));
		}

		private static IOrderedQueryable<Project> ApplySorting(IQueryable<Project> query, ProjectFilterDTO filter)
		{
			var direction = filter.SortDirection ?? (filter.SortBy is null ? ESortDirection.Desc : ESortDirection.Asc);
			var ordered = (filter.SortBy ?? EProjectSortBy.UpdatedAt) switch
			{
				EProjectSortBy.Name => OrderBy(query, project => project.NormalizedName, direction),
				EProjectSortBy.CreatedAt => OrderBy(query, project => project.CreatedAt, direction),
				_ => OrderBy(query, project => project.UpdatedAt, direction)
			};

			return ordered.ThenBy(project => project.Id);
		}

		private static IOrderedQueryable<Project> OrderBy<TKey>(IQueryable<Project> query, Expression<Func<Project, TKey>> keySelector, ESortDirection direction) =>
			direction == ESortDirection.Desc ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);
	}
}
