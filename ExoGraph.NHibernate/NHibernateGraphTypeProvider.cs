using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate;
using System.Reflection;
using ExoGraph.Injection;
using System.ComponentModel;

using IGraphInstance = ExoGraph.NHibernate.DataBindingFactory.IGraphInstance;
using INHibernateProxy = NHibernate.Proxy.INHibernateProxy;
using NHibernate.Context;
using NHibernate.Type;
using NHibernate.Engine;
using NHibernate.Metadata;
using NHibernate.Persister.Entity;

namespace ExoGraph.NHibernate
{
	/// <summary>
	/// NHibernate-specific implementation of the <see cref="ExoGraph.GraphContext"/>
	/// </summary>
	/// <remarks>
	/// Relies on the <see cref="NHibernate.Context.CurrentSessionContext"/> having been initialized
	/// </remarks>
	public class NHibernateGraphTypeProvider : StronglyTypedGraphTypeProvider
	{
		public ISessionFactoryImplementor SessionFactory
		{
			private get;
			set;
		}

		public NHibernateGraphTypeProvider(string @namespace, IEnumerable<Type> types)
			: base(@namespace, types)
		{ }

		protected override Type GetUnderlyingType(object instance)
		{
		    return instance.GetType().BaseType;
		}

		protected override StrongGraphType CreateGraphType(string @namespace, Type type, Func<GraphInstance, object> extensionFactory)
		{
			return new NHibernateGraphType(@namespace, type, extensionFactory, SessionFactory);
		}

		#region NHibernateGraphType

		internal class NHibernateGraphType : StrongGraphType
		{
			private ISessionFactoryImplementor sessionFactory;

			protected internal NHibernateGraphType(string @namespace, Type type, Func<GraphInstance, object> extensionFactory, ISessionFactoryImplementor sessionFactory)
				: base(@namespace, type, extensionFactory)
			{
				this.sessionFactory = sessionFactory;
			}

			/// <summary>
			/// Persists an entity to the persistence store
			/// </summary>
			/// <param name="graphInstance"></param>
			protected override void SaveInstance(GraphInstance graphInstance)
			{
				ISession session = sessionFactory.GetCurrentSession();

				using (ITransaction tx = session.BeginTransaction())
				{
					session.SaveOrUpdate(graphInstance.Instance.GetType().BaseType.ToString(), graphInstance.Instance);
					tx.Commit();
				}

				this.OnSave(graphInstance);
			}

			/// <summary>
			/// Retrieves the Id of an existing entity
			/// </summary>
			/// <param name="instance"></param>
			/// <returns></returns>
			protected override string GetId(object instance)
			{
				IClassMetadata metadata = sessionFactory.GetClassMetadata(instance.GetType().BaseType);
				IEntityPersister persister = sessionFactory.GetEntityPersister(instance.GetType().BaseType.ToString());

				((IGraphInstance) instance).SuspendNotifications = true;
				object id = metadata.GetIdentifier(instance, EntityMode.Poco);
				((IGraphInstance) instance).SuspendNotifications = false;

				// This is a bit of a hack.
				if (persister.EntityMetamodel.IdentifierProperty.UnsavedValue.IsUnsaved(id).GetValueOrDefault())
					return null;

				string idString = TypeDescriptor.GetConverter(id).ConvertToString(id);

				return idString;
			}

			/// <summary>
			/// Returns an entity from the persistence store, or a new entity
			/// </summary>
			/// <param name="type"></param>
			/// <param name="id">String representation of the id</param>
			/// <returns></returns>
			protected override object GetInstance(string id)
			{
				object result = null;
				Type objectType = Type.GetType(this.QualifiedName);
				ISession session = sessionFactory.GetCurrentSession();

				if (string.IsNullOrEmpty(id))
				{
					result = DataBindingFactory.Create(objectType);
				}
				else
				{
					IClassMetadata metadata = sessionFactory.GetClassMetadata(objectType);
					object idObject = TypeDescriptor.GetConverter(metadata.IdentifierType.ReturnedClass).ConvertFromString(id);
					result = session.Load(objectType, idObject);
				}

				return result;
			}

			/// <summary>
			/// Deletes an instance from the persistence store
			/// </summary>
			/// <param name="instance"></param>
			protected override void DeleteInstance(GraphInstance instance)
			{
				ISession session = sessionFactory.GetCurrentSession();

				using (ITransaction tx = session.BeginTransaction())
				{
					session.Delete(instance.Instance.GetType().BaseType.ToString(), instance);
					tx.Commit();
				}
			}

			public override GraphInstance GetGraphInstance(object instance)
			{
				return ((IGraphInstance) instance).Instance;
			}

			public void RaiseOnSave(GraphInstance instance)
			{
				base.OnSave(instance);
			}

			internal GraphInstance RaiseOnInit(object instance)
			{
				return base.OnInit(instance);
			}

			internal void NHibernateGraphType_PropertyModified(object sender, PropertyModifiedEventArgs e)
			{
				OnPropertyChanged(((IGraphInstance) sender).Instance, e.PropertyName, e.OldValue, e.NewValue);
			}

			internal void NHibernateGraphType_PropertyAccessed(object sender, PropertyAccessedEventArgs e)
			{
				OnPropertyGet(((IGraphInstance) sender).Instance, e.PropertyName);
			}
		}

		#endregion
	}
}