using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate;
using System.Reflection;

namespace ExoGraph.NHibernate
{
	/// <summary>
	/// An <see cref="NHibernate.IInterceptor"/> which watches for entity initialization
	/// </summary>
	/// <remarks>
	/// Uses the <see cref="ExoGraph.NHibernate.DataBindingFactory"/> to create the new instance
	/// </remarks>
	public class DataBindingInterceptor : EmptyInterceptor
	{
		public ISessionFactory SessionFactory { get; set; }

		public override object Instantiate(string clazz, EntityMode entityMode, object id)
		{
			if (entityMode == EntityMode.Poco)
			{
				Type type = FindByType(clazz);
				if (type != null)
				{
					var instance = DataBindingFactory.Create(type);
					SessionFactory.GetClassMetadata(clazz).SetIdentifier(instance, id, entityMode);
					return instance;
				}
			}

			return base.Instantiate(clazz, entityMode, id);
		}

		public override string GetEntityName(object entity)
		{
			var markerInterface = entity as DataBindingFactory.IMarkerInterface;
			if (markerInterface != null)
				return markerInterface.TypeName;

			return base.GetEntityName(entity);
		}

		private Type FindByType(string typeName)
		{
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type foundType = assembly.GetType(typeName);

				if (foundType != null)
					return foundType;
			}

			return null;
		}
	}
}
