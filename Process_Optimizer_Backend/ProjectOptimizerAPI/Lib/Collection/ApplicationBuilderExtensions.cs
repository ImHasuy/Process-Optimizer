namespace ProjectOptimizerAPI.Lib.Collection
{
	public static class ApplicationBuilderExtensions
	{
		public static WebApplication UseApiDocumentation(this WebApplication app)
		{
			if (!app.Environment.IsDevelopment())
			{
				return app;
			}

			app.UseSwagger();
			app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", ServiceCollection.ApiTitle + " v1"));
			return app;
		}
	}
}
