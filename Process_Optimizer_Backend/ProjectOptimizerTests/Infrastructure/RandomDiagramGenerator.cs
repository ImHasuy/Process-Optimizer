using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerTests.Infrastructure
{
	public static class RandomDiagramGenerator
	{
		private const int MaxDepth = 3;
		private const long MaxScenarioCount = 100_000;
		private const long CountCeiling = MaxScenarioCount * 10;
		private const double BlockProbability = 0.4;
		private const double ExclusiveTaskProbability = 0.1;

		private static readonly EFlowNodeKind[] BlockKinds =
		[
			EFlowNodeKind.ExclusiveGateway,
			EFlowNodeKind.ParallelGateway,
			EFlowNodeKind.InclusiveGateway,
			EFlowNodeKind.EventBasedGateway
		];

		public static string Generate(int seed)
		{
			var random = new Random(seed);
			var generator = new Generator(random);
			var sequence = generator.CreateSequence(0);
			return generator.Emit(sequence);
		}

		private abstract record Part
		{
			public abstract long Count { get; }
		}

		private sealed record TaskPart(int Duration, int Cost, bool IsParallelizable) : Part
		{
			public override long Count => 1;
		}

		private sealed record BlockPart(EFlowNodeKind Kind, IReadOnlyList<IReadOnlyList<Part>> Branches) : Part
		{
			public override long Count => Kind switch
			{
				EFlowNodeKind.ParallelGateway => Branches.Aggregate(1L, (product, branch) => Capped(product * SequenceCount(branch))),
				EFlowNodeKind.InclusiveGateway => Branches.Aggregate(1L, (product, branch) => Capped(product * (1 + SequenceCount(branch)))) - 1,
				_ => Capped(Branches.Sum(SequenceCount))
			};
		}

		private static long SequenceCount(IReadOnlyList<Part> sequence) => sequence.Aggregate(1L, (product, part) => Capped(product * part.Count));

		private static long Capped(long count) => Math.Min(count, CountCeiling);

		private sealed class Generator(Random random)
		{
			private readonly BpmnDiagramBuilder _builder = new();
			private int _taskCounter;
			private int _gatewayCounter;
			private int _flowCounter;

			public IReadOnlyList<Part> CreateSequence(int depth)
			{
				var parts = new List<Part>();
				var partCount = random.Next(1, 4);
				for (var index = 0; index < partCount; index++)
				{
					var part = CreatePart(depth);
					parts.Add(Capped(SequenceCount(parts) * part.Count) > MaxScenarioCount ? CreateTask() : part);
				}

				return parts;
			}

			public string Emit(IReadOnlyList<Part> sequence)
			{
				_builder.StartEvent("Start", "Start").EndEvent("End", "End");
				var (entryId, exitId) = EmitSequence(sequence);
				AddFlow("Start", entryId);
				AddFlow(exitId, "End");
				return _builder.Build();
			}

			private Part CreatePart(int depth)
			{
				if (depth >= MaxDepth || random.NextDouble() >= BlockProbability)
				{
					return CreateTask();
				}

				var block = CreateBlock(depth + 1);
				return block.Count > MaxScenarioCount ? CreateTask() : block;
			}

			private BlockPart CreateBlock(int depth)
			{
				var kind = BlockKinds[random.Next(BlockKinds.Length)];
				var branchCount = random.Next(2, 5);
				var branches = Enumerable.Range(0, branchCount).Select(_ => CreateSequence(depth)).ToList();
				return new BlockPart(kind, branches);
			}

			private TaskPart CreateTask() => new(random.Next(0, 21), random.Next(0, 5001), random.NextDouble() >= ExclusiveTaskProbability);

			private (string EntryId, string ExitId) EmitSequence(IReadOnlyList<Part> sequence)
			{
				var emitted = sequence.Select(EmitPart).ToList();
				for (var index = 1; index < emitted.Count; index++)
				{
					AddFlow(emitted[index - 1].ExitId, emitted[index].EntryId);
				}

				return (emitted[0].EntryId, emitted[^1].ExitId);
			}

			private (string EntryId, string ExitId) EmitPart(Part part) => part switch
			{
				TaskPart task => EmitTask(task),
				BlockPart block => EmitBlock(block),
				_ => throw new InvalidOperationException("Unknown diagram part.")
			};

			private (string EntryId, string ExitId) EmitTask(TaskPart task)
			{
				var taskId = $"Task_{++_taskCounter}";
				_builder.Task(taskId, $"Task {_taskCounter}", task.Duration, task.Cost, task.IsParallelizable ? null : false);
				return (taskId, taskId);
			}

			private (string EntryId, string ExitId) EmitBlock(BlockPart block)
			{
				var splitId = $"Gateway_{++_gatewayCounter}";
				var joinId = $"Gateway_{++_gatewayCounter}";
				_builder.Gateway(splitId, block.Kind).Gateway(joinId, JoinKind(block.Kind));
				foreach (var branch in block.Branches)
				{
					var (entryId, exitId) = EmitSequence(branch);
					AddFlow(splitId, entryId);
					AddFlow(exitId, joinId);
				}

				return (splitId, joinId);
			}

			private static EFlowNodeKind JoinKind(EFlowNodeKind splitKind) =>
				splitKind == EFlowNodeKind.EventBasedGateway ? EFlowNodeKind.ExclusiveGateway : splitKind;

			private void AddFlow(string sourceId, string targetId) => _builder.Flow($"Flow_{++_flowCounter}", sourceId, targetId);
		}
	}
}
