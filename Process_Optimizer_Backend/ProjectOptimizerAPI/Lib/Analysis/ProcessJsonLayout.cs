using ProjectOptimizerAPI.Lib.Analysis.Model;

namespace ProjectOptimizerAPI.Lib.Analysis
{
	public static class ProcessJsonLayout
	{
		private const int OriginX = 200;
		private const int OriginY = 120;
		private const int ColumnWidth = 180;
		private const int RowHeight = 130;
		private const int EventSize = 36;
		private const int GatewaySize = 50;
		private const int ActivityWidth = 100;
		private const int ActivityHeight = 80;
		private const string EventSuffix = "Event";
		private const string GatewaySuffix = "Gateway";

		public static IReadOnlyDictionary<string, LayoutBounds> Arrange(IReadOnlyList<(string Id, string ElementName)> nodes, IReadOnlyList<(string SourceId, string TargetId)> edges)
		{
			var layers = AssignLayers(nodes.Select(node => node.Id).ToList(), edges);
			var rows = new Dictionary<int, int>();
			var bounds = new Dictionary<string, LayoutBounds>(StringComparer.Ordinal);
			foreach (var (id, elementName) in nodes)
			{
				var layer = layers[id];
				var row = rows.GetValueOrDefault(layer);
				rows[layer] = row + 1;
				bounds[id] = BoundsAt(OriginX + layer * ColumnWidth, OriginY + row * RowHeight, elementName);
			}

			return bounds;
		}

		private static Dictionary<string, int> AssignLayers(IReadOnlyList<string> nodeIds, IReadOnlyList<(string SourceId, string TargetId)> edges)
		{
			var successors = edges.ToLookup(edge => edge.SourceId, edge => edge.TargetId, StringComparer.Ordinal);
			var incomingCounts = nodeIds.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
			foreach (var edge in edges)
			{
				incomingCounts[edge.TargetId]++;
			}

			var layers = nodeIds.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
			var ready = new Queue<string>(nodeIds.Where(id => incomingCounts[id] == 0));
			var placed = new HashSet<string>(StringComparer.Ordinal);
			while (ready.TryDequeue(out var id))
			{
				placed.Add(id);
				foreach (var successor in successors[id])
				{
					layers[successor] = Math.Max(layers[successor], layers[id] + 1);
					incomingCounts[successor]--;
					if (incomingCounts[successor] == 0)
					{
						ready.Enqueue(successor);
					}
				}
			}

			PlaceCyclicNodes(nodeIds, placed, layers);
			return layers;
		}

		private static void PlaceCyclicNodes(IReadOnlyList<string> nodeIds, HashSet<string> placed, Dictionary<string, int> layers)
		{
			var nextLayer = placed.Count == 0 ? 0 : placed.Max(id => layers[id]) + 1;
			foreach (var id in nodeIds.Where(id => !placed.Contains(id)))
			{
				layers[id] = nextLayer++;
			}
		}

		private static LayoutBounds BoundsAt(int centerX, int centerY, string elementName)
		{
			var (width, height) = SizeOf(elementName);
			return new LayoutBounds(centerX - width / 2, centerY - height / 2, width, height);
		}

		private static (int Width, int Height) SizeOf(string elementName)
		{
			if (elementName.EndsWith(EventSuffix, StringComparison.Ordinal))
			{
				return (EventSize, EventSize);
			}

			return elementName.EndsWith(GatewaySuffix, StringComparison.Ordinal) ? (GatewaySize, GatewaySize) : (ActivityWidth, ActivityHeight);
		}
	}
}
