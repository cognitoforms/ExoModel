using System;
using System.Runtime.Serialization;

namespace ExoGraph
{
	public interface ITransactedGraphEvent
	{
		void Perform();

		void Commit();

		void Rollback();
	}
}
