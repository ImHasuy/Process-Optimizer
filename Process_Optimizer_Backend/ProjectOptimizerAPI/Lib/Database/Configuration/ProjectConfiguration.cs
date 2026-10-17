using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Database.Configuration
{
	public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
	{
		public void Configure(EntityTypeBuilder<Project> builder)
		{
			builder.ToTable("Projects");
			builder.HasKey(project => project.Id);
			builder.Property(project => project.Name).IsRequired().HasMaxLength(FieldLimits.NameMaxLength);
			builder.Property(project => project.NormalizedName).IsRequired().HasMaxLength(FieldLimits.NameMaxLength);
			builder.Property(project => project.Description).HasMaxLength(FieldLimits.TextMaxLength);
			builder.Property(project => project.TimeUnit).HasConversion<string>().HasMaxLength(30);
			builder.Property(project => project.Currency).IsRequired().HasMaxLength(3);
			builder.HasIndex(project => project.NormalizedName).IsUnique();
			builder.HasIndex(project => project.UpdatedAt);
		}
	}
}
