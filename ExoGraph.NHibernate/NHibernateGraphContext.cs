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
	public class NHibernateGraphContext : StronglyTypedGraphContext
	{
		public ISessionFactoryImplementor SessionFactory
		{
			private get;
			set;
		}

		public NHibernateGraphContext(IEnumerable<Type> types)
			: base(types, null, null)
		{
		}

		/// <summary>
		/// Initializes a <see cref="ExoGraph.GraphInstance" for the instance passed-in />
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		internal GraphInstance InitGraphInstance(object instance)
		{
			return OnInit(instance);
		}

		/// <summary>
		/// Returns an entity from the persistence store, or a new entity
		/// </summary>
		/// <param name="type"></param>
		/// <param name="id">String representation of the id</param>
		/// <returns></returns>
		protected override object GetInstance(GraphType type, string id)
		{
			object result = null;
			Type objectType = Type.GetType(type.QualifiedName);
			ISession session = SessionFactory.GetCurrentSession();

			if (string.IsNullOrEmpty(id))
			{
				result = DataBindingFactory.Create(objectType);
			}
			else
			{
				IClassMetadata metadata = SessionFactory.GetClassMetadata(objectType);
				object idObject = TypeDescriptor.GetConverter(metadata.IdentifierType.ReturnedClass).ConvertFromString(id);
				result = session.Load(objectType, idObject);
			}

			return result;
		}

		internal void NHibernateGraphContext_PropertyModified(object sender, PropertyModifiedEventArgs e)
		{
			OnPropertyChanged(((IGraphInstance) sender).Instance, e.PropertyName, e.OldValue, e.NewValue);
		}

		internal void NHibernateGraphContext_PropertyAccessed(object sender, PropertyAccessedEventArgs e)
		{
			OnPropertyGet(((IGraphInstance) sender).Instance, e.PropertyName);
		}

		/// <summary>
		/// Deletes an instance from the persistence store
		/// </summary>
		/// <param name="instance"></param>
		protected override void DeleteInstance(object instance)
		{
			ISession session = SessionFactory.GetCurrentSession();

			using (ITransaction tx = session.BeginTransaction())
			{
				session.Delete(instance.GetType().BaseType.ToString(), instance);
				tx.Commit();
			}
		}

		/// <summary>
		/// Persists an entity to the persistence store
		/// </summary>
		/// <param name="graphInstance"></param>
		protected override void Save(GraphInstance graphInstance)
		{
			ISession session = SessionFactory.GetCurrentSession();

			using (ITransaction tx = session.BeginTransaction())
			{
				session.SaveOrUpdate(graphInstance.Instance.GetType().BaseType.ToString(), graphInstance.Instance);
				tx.Commit();
			}
		}

		/// <summary>
		/// Exposes the OnSave event
		/// </summary>
		/// <param name="graphInstance"></param>
		internal void OnSave(GraphInstance graphInstance)
		{
			base.OnSave(graphInstance);
		}

		/// <summary>
		/// Returns the <see cref="ExoGraph.GraphInstance"/> associated with a loaded entity
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		public override GraphInstance GetGraphInstance(object instance)
		{
			if (instance is DataBindingFactory.IGraphInstance)
				return ((DataBindingFactory.IGraphInstance) instance).Instance;

			return null;
		}

		protected override GraphType GetGraphType(object instance)
		{
			return base.GetGraphType(instance.GetType().BaseType);
		}

		/// <summary>
		/// Retrieves the Id of an existing entity
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		protected override string GetId(object instance)
		{
			using (SuspendGetNotifications())
			{
				IClassMetadata metadata = SessionFactory.GetClassMetadata(instance.GetType().BaseType);
				IEntityPersister persister = SessionFactory.GetEntityPersister(instance.GetType().BaseType.ToString());

				object id = metadata.GetIdentifier(instance, EntityMode.Poco);

				// This is a bit of a hack.
				if (persister.EntityMetamodel.IdentifierProperty.UnsavedValue.IsUnsaved(id).GetValueOrDefault())
					return null;

				string idString = TypeDescriptor.GetConverter(id).ConvertToString(id);
				return idString;
			}
		}
	}
}
