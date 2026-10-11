using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerTests.Infrastructure
{
	public static class BpmnFixtures
	{
		private const string FixturesFolder = "Fixtures";

		public static string WorkedExample => Load("worked-example.bpmn");

		public static string Load(string relativePath) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, FixturesFolder, relativePath));

		public static string DisconnectedDiagram() =>
			new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Task("Lonely", "Lonely task", 1, 100)
				.EndEvent("End")
				.Build();

		public static string InclusiveSplitDiagram() =>
			new BpmnDiagramBuilder()
				.StartEvent("Start")
				.EndEvent("End")
				.Gateway("Or", EFlowNodeKind.InclusiveGateway)
				.Gateway("OrJoin", EFlowNodeKind.InclusiveGateway)
				.Task("A", "A", 1, 100)
				.Task("B", "B", 2, 100)
				.Task("C", "C", 4, 300)
				.Flow("F0", "Start", "Or")
				.Flow("Fa", "Or", "A")
				.Flow("Fb", "Or", "B")
				.Flow("Fc", "Or", "C")
				.Flow("Ja", "A", "OrJoin")
				.Flow("Jb", "B", "OrJoin")
				.Flow("Jc", "C", "OrJoin")
				.Flow("F9", "OrJoin", "End")
				.Build();
	}
}
