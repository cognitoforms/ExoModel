using Afterthought;

namespace ExoModel.Json.Amender
{
	internal class JsonEntityAmendment<TEntity> : Amendment<TEntity, TEntity>
		where TEntity : JsonEntity
	{
		public JsonEntityAmendment()
		{
			Implement<IModelInstance>(
				Properties.Add("Instance", JsonEntityAdapter<TEntity>.InitializeModelInstance)
				);

			Properties
				.Where(p =>
					p.Name != "Id" &&
					p.Name != "Instance" &&
					p.Name != "IsInitialized" &&
					p.PropertyInfo != null && p.PropertyInfo.CanRead && p.PropertyInfo.CanWrite &&
					p.PropertyInfo.GetGetMethod() != null && p.PropertyInfo.GetSetMethod().IsPublic)
				.BeforeGet(JsonEntityAdapter<TEntity>.BeforeGet)
				.AfterSet(JsonEntityAdapter<TEntity>.AfterSet);

			Properties.Where(p => p.Name == "Id").BeforeSet(JsonEntityAdapter<TEntity>.BeforeSetId);
		}
	}
}
