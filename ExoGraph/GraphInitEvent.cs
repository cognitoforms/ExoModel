using System.Runtime.Serialization;
namespace ExoGraph
{
	/// <summary>
	/// Represents the creation of a new or existing graph instance.
	/// </summary>
	[DataContract(Name = "Init")]
	[KnownType(typeof(GraphInitEvent.InitNew))]
	[KnownType(typeof(GraphInitEvent.InitExisting))]
	public abstract class GraphInitEvent : GraphEvent
	{
		internal GraphInitEvent(GraphInstance instance)
			: base(instance)
		{ }

		protected override void OnNotify()
		{
			Instance.Type.RaiseInit(this);
		}

		public override string ToString()
		{
			return "Initialized " + Instance;
		}

		#region InitNew

		/// <summary>
		/// Represents the creation of a new <see cref="GraphInstance"/>.
		/// </summary>
		[DataContract(Name = "InitNew")]
		public class InitNew : GraphInitEvent, ITransactedGraphEvent
		{
			GraphType type;

			internal InitNew(GraphInstance instance)
				: base(instance)
			{
				this.type = instance.Type;
			}

			internal InitNew(GraphType type)
				: base(null)
			{
				this.type = type;
			}

			public GraphType Type
			{
				get
				{
					return type;
				}
			}

			/// <summary>
			/// Gets or sets the name of the <see cref="GraphType"/> in order to allow the type to
			/// be serialized by name instead of serializing the entire type instance.
			/// </summary>
			[DataMember(Name = "Type")]
			string TypeName
			{
				get
				{
					return type.Name;
				}
				set
				{
					type = GraphContext.Current.GraphTypes[value];
				}
			}

			#region ITransactedGraphEvent Members

			/// <summary>
			/// Creates a new <see cref="GraphInstance"/> of the specified <see cref="GraphType"/>.
			/// </summary>
			void ITransactedGraphEvent.Perform()
			{
				if (Instance != null)
					Instance = Type.Create();
			}

			/// <summary>
			/// 
			/// </summary>
			void ITransactedGraphEvent.Commit()
			{ }

			/// <summary>
			/// Deletes and removes the reference to the <see cref="GraphInstance"/> associated with
			/// the current event, which effectively removes the instance from existence.
			/// </summary>
			void ITransactedGraphEvent.Rollback()
			{
				if (Instance != null)
				{
					Instance.Delete();
					Instance = null;
				}
			}

			#endregion
		}

		#endregion

		#region InitExisting

		/// <summary>
		/// Represents the creation of an existing <see cref="GraphInstance"/>.
		/// </summary>
		[DataContract(Name = "InitExisting")]
		internal class InitExisting : GraphInitEvent
		{
			internal InitExisting(GraphInstance instance)
				: base(instance)
			{ }
		}

		#endregion
	}
}
