using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Database.Configuration
{
	public sealed class DiagramRevisionConfiguration : IEntityTypeConfiguration<DiagramRevision>
	{
		public void Configure(EntityTypeBuilder<DiagramRevision> builder)
		{
			builder.ToTable("DiagramRevisions");
			builder.HasKey(revision => revision.Id);
			builder.Property(revision => revision.BpmnXml).IsRequired();
			builder.Property(revision => revision.ContentSha256).IsRequired().HasMaxLength(64);
			builder.HasIndex(revision => new { revision.ProjectId, revision.RevisionNumber }).IsUnique();

			builder.HasOne(revision => revision.Project)
				.WithMany(project => project.Revisions)
				.HasForeignKey(revision => revision.ProjectId)
				.IsRequired()
				.OnDelete(DeleteBehavior.Cascade);
		}
	}
}
