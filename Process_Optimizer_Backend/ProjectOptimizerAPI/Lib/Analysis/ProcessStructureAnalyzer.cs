using System.Numerics;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Analysis
{
	public sealed class ProcessStructureAnalyzer : IProcessStructureAnalyzer
	{
		private const string NotWellStructuredMessage = "The split has no matching join of the same type, or the region between them is not single-entry single-exit.";

		public StructureAnalysisResult Analyze(ProcessGraph graph)
		{
			try
			{
				var root = new StructureTreeBuilder(StructureGraph.Create(graph)).BuildRoot();
				return new StructureAnalysisResult(root, []);
			}
			catch (StructureViolationException violation)
			{
				return new StructureAnalysisResult(null, [CreateNotWellStructuredIssue(violation.BpmnElementId)]);
			}
		}

		private static ValidationIssue CreateNotWellStructuredIssue(string? bpmnElementId) =>
			new(DiagramIssueCodes.NotWellStructured, EIssueSeverity.Error, bpmnElementId, NotWellStructuredMessage);

		private readonly record struct Arc(string FlowId, int TargetIndex);

		private sealed class StructureViolationException(string? bpmnElementId) : Exception(NotWellStructuredMessage)
		{
			public string? BpmnElementId { get; } = bpmnElementId;
		}

		private sealed class StructureGraph
		{
			private const string VirtualFlowId = "";

			private StructureGraph(IReadOnlyList<FlowNode?> nodes, IReadOnlyList<IReadOnlyList<Arc>> outgoing)
			{
				Nodes = nodes;
				Outgoing = outgoing;
				IncomingCounts = CountIncoming(outgoing);
				ImmediatePostDominators = ComputeImmediatePostDominators(outgoing);
			}

			public IReadOnlyList<FlowNode?> Nodes { get; }

			public IReadOnlyList<IReadOnlyList<Arc>> Outgoing { get; }

			public IReadOnlyList<int> IncomingCounts { get; }

			public IReadOnlyList<int> ImmediatePostDominators { get; }

			public int SourceIndex => 0;

			public int SinkIndex => Nodes.Count - 1;

			public static StructureGraph Create(ProcessGraph graph)
			{
				var orderedNodes = TopologicalOrder.Sort(graph.Nodes, graph.Edges) ?? throw new StructureViolationException(null);
				FlowNode?[] nodes = [null, .. orderedNodes, null];
				var outgoing = nodes.Select(_ => new List<Arc>()).ToArray();
				AddSequenceFlowArcs(graph.Edges, orderedNodes, outgoing);
				AddVirtualArcs(nodes, outgoing);
				return new StructureGraph(nodes, outgoing);
			}

			public string? IdOf(int index) => Nodes[index]?.BpmnId;

			public bool IsSplit(int index) => Outgoing[index].Count >= AnalysisLimits.MinBranchingFlows;

			private static void AddSequenceFlowArcs(IEnumerable<SequenceFlowEdge> edges, IReadOnlyList<FlowNode> orderedNodes, List<Arc>[] outgoing)
			{
				var indexById = orderedNodes
					.Select((node, position) => (node.BpmnId, Index: position + 1))
					.ToDictionary(entry => entry.BpmnId, entry => entry.Index, StringComparer.Ordinal);
				foreach (var edge in edges.Where(edge => indexById.ContainsKey(edge.SourceId) && indexById.ContainsKey(edge.TargetId)))
				{
					outgoing[indexById[edge.SourceId]].Add(new Arc(edge.BpmnId, indexById[edge.TargetId]));
				}
			}

			private static void AddVirtualArcs(FlowNode?[] nodes, List<Arc>[] outgoing)
			{
				var sinkIndex = nodes.Length - 1;
				for (var index = 1; index < sinkIndex; index++)
				{
					if (nodes[index]?.Kind == EFlowNodeKind.StartEvent)
					{
						outgoing[0].Add(new Arc(VirtualFlowId, index));
					}
					if (nodes[index]?.Kind == EFlowNodeKind.EndEvent)
					{
						outgoing[index].Add(new Arc(VirtualFlowId, sinkIndex));
					}
				}
			}

			private static int[] CountIncoming(IReadOnlyList<IReadOnlyList<Arc>> outgoing)
			{
				var incomingCounts = new int[outgoing.Count];
				foreach (var arc in outgoing.SelectMany(arcs => arcs))
				{
					incomingCounts[arc.TargetIndex]++;
				}
				return incomingCounts;
			}

			private static int[] ComputeImmediatePostDominators(IReadOnlyList<IReadOnlyList<Arc>> outgoing)
			{
				var sinkIndex = outgoing.Count - 1;
				var dominators = new int[outgoing.Count];
				var depths = new int[outgoing.Count];
				dominators[sinkIndex] = sinkIndex;
				for (var index = sinkIndex - 1; index >= 0; index--)
				{
					var dominator = outgoing[index]
						.Select(arc => arc.TargetIndex)
						.DefaultIfEmpty(sinkIndex)
						.Aggregate((left, right) => FindCommonDominator(left, right, dominators, depths));
					dominators[index] = dominator;
					depths[index] = depths[dominator] + 1;
				}
				return dominators;
			}

			private static int FindCommonDominator(int left, int right, int[] dominators, int[] depths)
			{
				while (left != right)
				{
					if (depths[left] >= depths[right])
					{
						left = dominators[left];
					}
					else
					{
						right = dominators[right];
					}
				}
				return left;
			}
		}

		private sealed class StructureTreeBuilder(StructureGraph structureGraph)
		{
			public StructureFragment BuildRoot() => BuildSequence(structureGraph.SourceIndex, structureGraph.SinkIndex, null);

			private StructureFragment BuildSequence(int first, int exit, string? blamedNodeId)
			{
				var parts = new List<StructureFragment>();
				var current = first;
				while (current != exit)
				{
					AddLeaf(parts, current);
					current = structureGraph.IsSplit(current) ? AddBlock(parts, current) : StepForward(current, exit, blamedNodeId);
				}
				return CreateSequence(parts);
			}

			private void AddLeaf(List<StructureFragment> parts, int index)
			{
				if (structureGraph.Nodes[index] is { } node)
				{
					parts.Add(CreateLeaf(node));
				}
			}

			private int StepForward(int current, int exit, string? blamedNodeId)
			{
				var arcs = structureGraph.Outgoing[current];
				if (arcs.Count == 0)
				{
					throw new StructureViolationException(blamedNodeId ?? structureGraph.IdOf(current));
				}
				var next = arcs[0].TargetIndex;
				EnsureSingleEntry(next, exit, blamedNodeId);
				return next;
			}

			private int AddBlock(List<StructureFragment> parts, int split)
			{
				var join = structureGraph.ImmediatePostDominators[split];
				var blamedNodeId = structureGraph.IdOf(split) ?? structureGraph.IdOf(join);
				EnsureMatchingJoin(split, join, blamedNodeId);
				var branches = structureGraph.Outgoing[split]
					.OrderBy(arc => structureGraph.IdOf(arc.TargetIndex) ?? string.Empty, StringComparer.Ordinal)
					.ThenBy(arc => arc.FlowId, StringComparer.Ordinal)
					.Select(arc => BuildBranch(arc.TargetIndex, join, blamedNodeId))
					.ToList();
				parts.Add(CreateBlock(split, join, branches));
				return join;
			}

			private StructureFragment BuildBranch(int entry, int join, string? blamedNodeId)
			{
				if (entry == join)
				{
					return CreateEmptySequence(structureGraph.IdOf(join) ?? string.Empty);
				}
				EnsureSingleEntry(entry, join, blamedNodeId);
				return BuildSequence(entry, join, blamedNodeId);
			}

			private void EnsureSingleEntry(int index, int exit, string? blamedNodeId)
			{
				if (index != exit && structureGraph.IncomingCounts[index] != 1)
				{
					throw new StructureViolationException(blamedNodeId ?? structureGraph.IdOf(index));
				}
			}

			private void EnsureMatchingJoin(int split, int join, string? blamedNodeId)
			{
				var isVirtualJoin = join == structureGraph.SinkIndex;
				var hasMatchingFlowCount = isVirtualJoin || structureGraph.IncomingCounts[join] == structureGraph.Outgoing[split].Count;
				if (!hasMatchingFlowCount || !IsMatchingJoin(BlockKindOf(structureGraph.Nodes[split]), structureGraph.Nodes[join]))
				{
					throw new StructureViolationException(blamedNodeId);
				}
			}

			private StructureFragment CreateBlock(int split, int join, IReadOnlyList<StructureFragment> branches)
			{
				var splitNode = structureGraph.Nodes[split];
				var kind = BlockKindOf(splitNode);
				var gatewayKind = kind == EFragmentKind.Parallel ? null : splitNode?.Kind;
				var isControllable = splitNode?.Kind != EFlowNodeKind.EventBasedGateway;
				var entryNodeId = splitNode?.BpmnId ?? branches[0].EntryNodeId;
				return new StructureFragment(kind, entryNodeId, null, splitNode?.BpmnId, structureGraph.IdOf(join), gatewayKind, isControllable, branches, CountBlock(kind, branches));
			}

			private static EFragmentKind BlockKindOf(FlowNode? splitNode) => splitNode?.Kind switch
			{
				EFlowNodeKind.ExclusiveGateway or EFlowNodeKind.EventBasedGateway => EFragmentKind.Exclusive,
				EFlowNodeKind.InclusiveGateway => EFragmentKind.Inclusive,
				_ => EFragmentKind.Parallel
			};

			private static bool IsMatchingJoin(EFragmentKind blockKind, FlowNode? joinNode) => blockKind switch
			{
				EFragmentKind.Exclusive => joinNode?.Kind == EFlowNodeKind.ExclusiveGateway,
				EFragmentKind.Inclusive => joinNode?.Kind == EFlowNodeKind.InclusiveGateway,
				_ => joinNode is null || joinNode.Kind == EFlowNodeKind.ParallelGateway || !joinNode.IsGateway
			};

			private static BigInteger CountBlock(EFragmentKind blockKind, IReadOnlyList<StructureFragment> branches) => blockKind switch
			{
				EFragmentKind.Exclusive => branches.Aggregate(BigInteger.Zero, (total, branch) => total + branch.Count),
				EFragmentKind.Inclusive => FragmentChoices.Multiply(branches.Select(branch => branch.Count + BigInteger.One)) - BigInteger.One,
				_ => FragmentChoices.Product(branches)
			};

			private static StructureFragment CreateLeaf(FlowNode node) =>
				new(EFragmentKind.Leaf, node.BpmnId, node.BpmnId, null, null, null, node.IsControllable, [], BigInteger.One);

			private static StructureFragment CreateSequence(IReadOnlyList<StructureFragment> parts) =>
				new(EFragmentKind.Sequence, parts[0].EntryNodeId, null, null, null, null, true, parts, FragmentChoices.Product(parts));

			private static StructureFragment CreateEmptySequence(string joinNodeId) =>
				new(EFragmentKind.Sequence, joinNodeId, null, null, null, null, true, [], BigInteger.One);
		}
	}
}
