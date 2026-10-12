using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Database.Configuration
{
	public sealed class DiagramConfiguration : IEntityTypeConfiguration<Diagram>
	{
		public void Configure(EntityTypeBuilder<Diagram> builder)
		{
			builder.ToTable("Diagrams");
			builder.HasKey(diagram => diagram.ProjectId);
			builder.Property(diagram => diagram.BpmnXml).IsRequired();
			builder.Property(diagram => diagram.ContentSha256).IsRequired().HasMaxLength(64);

			builder.HasOne(diagram => diagram.Project)
				.WithOne(project => project.Diagram)
				.HasForeignKey<Diagram>(diagram => diagram.ProjectId)
				.OnDelete(DeleteBehavior.Cascade);
		}
	}
}
