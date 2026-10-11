using System.Numerics;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record StructureFragment(
		EFragmentKind Kind,
		string EntryNodeId,
		string? NodeId,
		string? SplitNodeId,
		string? JoinNodeId,
		EFlowNodeKind? GatewayKind,
		bool IsControllable,
		IReadOnlyList<StructureFragment> Children,
		BigInteger Count);
}
