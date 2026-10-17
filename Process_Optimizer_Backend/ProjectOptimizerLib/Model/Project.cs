using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerLib.Model
{
	public class Project
	{
		public Guid Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string NormalizedName { get; set; } = string.Empty;
		public string? Description { get; set; }
		public ETimeUnit TimeUnit { get; set; }
		public string Currency { get; set; } = string.Empty;
		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }
		public Diagram Diagram { get; set; } = null!;
	}
}
