using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Database.Configuration
{
	public sealed class SavedScenarioConfiguration : IEntityTypeConfiguration<SavedScenario>
	{
		public void Configure(EntityTypeBuilder<SavedScenario> builder)
		{
			builder.ToTable("SavedScenarios");
			builder.HasKey(savedScenario => savedScenario.Id);
			builder.Property(savedScenario => savedScenario.Name).IsRequired().HasMaxLength(FieldLimits.NameMaxLength);
			builder.Property(savedScenario => savedScenario.NormalizedName).IsRequired().HasMaxLength(FieldLimits.NameMaxLength);
			builder.Property(savedScenario => savedScenario.Note).HasMaxLength(FieldLimits.TextMaxLength);
			builder.Property(savedScenario => savedScenario.ScenarioKey).IsRequired().HasMaxLength(2000);
			builder.Property(savedScenario => savedScenario.SnapshotJson).IsRequired();
			builder.HasIndex(savedScenario => new { savedScenario.ProjectId, savedScenario.NormalizedName }).IsUnique();

			builder.HasOne(savedScenario => savedScenario.Project)
				.WithMany(project => project.SavedScenarios)
				.HasForeignKey(savedScenario => savedScenario.ProjectId)
				.IsRequired()
				.OnDelete(DeleteBehavior.Cascade);
		}
	}
}
