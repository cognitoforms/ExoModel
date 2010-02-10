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

		internal GraphInstance InitGraphInstance(object instance)
		{
			return OnInit(instance);
		}

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

			((INotifyPropertyAccessed) result).PropertyAccessed += new PropertyAccessedEventHandler(NHibernateGraphContext_PropertyAccessed);
			((INotifyPropertyModified) result).PropertyModified += new PropertyModifiedEventHandler(NHibernateGraphContext_PropertyModified);

			return result;
		}

		private void NHibernateGraphContext_PropertyModified(object sender, PropertyModifiedEventArgs e)
		{
			OnPropertyChanged(((IGraphInstance) sender).Instance, e.PropertyName, e.OldValue, e.NewValue);
		}

		private void NHibernateGraphContext_PropertyAccessed(object sender, PropertyAccessedEventArgs e)
		{
			OnPropertyGet(((IGraphInstance) sender).Instance, e.PropertyName);
		}

		protected override void DeleteInstance(object instance)
		{
			ISession session = SessionFactory.GetCurrentSession();

			using (ITransaction tx = session.BeginTransaction())
			{
				session.Delete(instance.GetType().BaseType.ToString(), instance);
				tx.Commit();
			}
		}

		protected override void Save(GraphInstance graphInstance)
		{
			ISession session = SessionFactory.GetCurrentSession();

			using (ITransaction tx = session.BeginTransaction())
			{
				session.SaveOrUpdate(graphInstance.Instance.GetType().BaseType.ToString(), graphInstance.Instance);
				tx.Commit();
			}
		}

		internal void OnSave(GraphInstance graphInstance)
		{
			base.OnSave(graphInstance);
		}

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
