using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoModel
{
	/// <summary>
	/// Wrap exceptions in this type to control the information displayed in the UI
	/// when a more abstracted or informative message is desired.
	/// 
	/// See ServiceHandler.ProcessRequest for implications
	/// </summary>
	public class ModelException : ApplicationException
	{
		public ModelException(string message, Exception innerException)
			: base(message, innerException)
		{}
	}
}
