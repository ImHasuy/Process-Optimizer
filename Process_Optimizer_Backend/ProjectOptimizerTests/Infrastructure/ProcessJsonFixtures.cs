using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerTests.Infrastructure
{
	public static class ProcessJsonFixtures
	{
		public const string FrontendReadmeExportJson = """
{
  "baseRevision": 1,
  "states": [
    { "id": "StartEvent_1", "name": "Start", "type": "bpmn:StartEvent", "time": "", "price": "", "Depends_on": [], "Connects_to": ["Activity_1"] },
    { "id": "Activity_1", "name": "Sample Task", "type": "bpmn:Task", "time": "15m", "price": "25 EUR", "Depends_on": ["StartEvent_1"], "Connects_to": ["EndEvent_1"] },
    { "id": "EndEvent_1", "name": "End", "type": "bpmn:EndEvent", "time": "", "price": "", "Depends_on": ["Activity_1"], "Connects_to": [] }
  ],
  "branches": [
    { "id": "Flow_1", "name": "", "source": "StartEvent_1", "target": "Activity_1", "type": "sequence", "condition": null },
    { "id": "Flow_2", "name": "", "source": "Activity_1", "target": "EndEvent_1", "type": "sequence", "condition": null }
  ]
}
""";

		public static ProcessStateDTO State(string id, string type, string? time = null, string? price = null, string? name = null) =>
			new() { Id = id, Type = type, Time = time, Price = price, Name = name };

		public static ProcessBranchDTO Branch(string id, string source, string target, string? type = "sequence", string? condition = null) =>
			new() { Id = id, Source = source, Target = target, Type = type, Condition = condition };

		public static List<ProcessStateDTO> SampleStates() =>
		[
			State("StartEvent_1", "bpmn:StartEvent", "", "", "Start"),
			State("Activity_1", "bpmn:Task", "15m", "25 EUR", "Sample Task"),
			State("EndEvent_1", "bpmn:EndEvent", "", "", "End")
		];

		public static List<ProcessBranchDTO> SampleBranches() =>
		[
			Branch("Flow_1", "StartEvent_1", "Activity_1"),
			Branch("Flow_2", "Activity_1", "EndEvent_1")
		];

		public static List<ProcessStateDTO> ExclusiveChoiceStates() =>
		[
			State("Start", "bpmn:StartEvent"),
			State("Split", "bpmn:ExclusiveGateway"),
			State("Cheap", "bpmn:Task", "2d", "100"),
			State("Fast", "bpmn:Task", "1", "300 EUR"),
			State("Join", "bpmn:ExclusiveGateway"),
			State("End", "bpmn:EndEvent")
		];

		public static List<ProcessBranchDTO> ExclusiveChoiceBranches() =>
		[
			Branch("F1", "Start", "Split"),
			Branch("F2", "Split", "Cheap", "nor"),
			Branch("F3", "Split", "Fast", "xor", "budget > 1000"),
			Branch("F4", "Cheap", "Join"),
			Branch("F5", "Fast", "Join"),
			Branch("F6", "Join", "End")
		];
	}
}
