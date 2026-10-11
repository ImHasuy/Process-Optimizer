namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record LayoutBounds(int X, int Y, int Width, int Height)
	{
		public int CenterY => Y + Height / 2;

		public int Right => X + Width;
	}
}
