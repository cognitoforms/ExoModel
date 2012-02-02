using System;
using System.Runtime.Serialization;

namespace ExoModel
{
	public interface ITransactedModelEvent
	{
		void Perform(ModelTransaction transaction);

		void Rollback(ModelTransaction transaction);
	}
}
