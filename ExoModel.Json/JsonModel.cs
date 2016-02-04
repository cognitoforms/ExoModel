using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ExoModel.Json
{
	/// <summary>
	/// Represents a complete dynamic model based on JSON type and instance data, based on the JSON
	/// format used by ExoWeb for sending type and instance data to web clients.
	/// </summary>
	public class JsonModel
	{
		internal static IEnumerable<Type> GetEntityTypes(Assembly @assembly, string @namespace = null)
		{
			return (string.IsNullOrEmpty(@namespace)
				? assembly.GetTypes()
				: assembly.GetTypes()
					.Where(t => t.Namespace == @namespace)
				).Where(t => typeof (IJsonEntity).IsAssignableFrom(t));
		}

		public static JsonEntityContext Initialize(Assembly @assembly, string storagePath, Action<ModelContext> onContextCreated = null)
		{
			var contextProvider = new ModelContextProvider();

			var entityContext = new JsonEntityContext(storagePath, false, GetEntityTypes(@assembly).ToArray());

			contextProvider.CreateContext += (s, args) =>
			{
				// TODO: Reset entity context when a model context is created?
				var typeProvider = new JsonModelTypeProvider(@assembly, entityContext);
				var ctx = args.Context = new ModelContext(typeProvider);
				if (onContextCreated != null)
					onContextCreated(ctx);
			};

			return entityContext;
		}

		public static JsonEntityContext Initialize(Assembly @assembly, string @namespace, string storagePath, Action<ModelContext> onContextCreated = null)
		{
			var contextProvider = new ModelContextProvider();

			var entityContext = new JsonEntityContext(storagePath, false, GetEntityTypes(@assembly, @namespace).ToArray());

			contextProvider.CreateContext += (s, args) =>
			{
				// TODO: Reset entity context when a model context is created?
				var typeProvider = new JsonModelTypeProvider(@assembly, @namespace, entityContext);
				var ctx = args.Context = new ModelContext(typeProvider);
				if (onContextCreated != null)
					onContextCreated(ctx);
			};

			return entityContext;
		}
	}
}
