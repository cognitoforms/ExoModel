using System;
using System.Collections.Generic;
using Afterthought;

namespace ExoModel.Json.Amender
{
	[AttributeUsage(AttributeTargets.Assembly)]
	internal class AmendmentsAttribute : Attribute, IAmendmentAttribute
	{
		private static ITypeAmendment CreateGenericAmendment(Type amendmentType, Type targetType)
		{
			var entityAmendmentCtor = amendmentType.MakeGenericType(targetType).GetConstructor(Type.EmptyTypes);
			if (entityAmendmentCtor == null)
				throw new Exception("Cannot create '" + amendmentType.Name + "' for '" + targetType.Name + "'.");

			return (ITypeAmendment)entityAmendmentCtor.Invoke(new object[0]);
		}

		IEnumerable<ITypeAmendment> IAmendmentAttribute.GetAmendments(Type target)
		{
			if (typeof(IJsonEntity).IsAssignableFrom(target))
				yield return CreateGenericAmendment(typeof(JsonEntityAmendment<>), target);
		}
	}
}
