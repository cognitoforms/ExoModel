using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace ExoModel.ETL
{
	/// <summary>
	/// Exposes a file stream and file metadata.
	/// </summary>
	public class FileStream : IDisposable
	{
		public FileStream(string fileName, string contentType, int contentLength, Stream inputStream)
		{
			this.FileName = fileName;
			this.ContentType = contentType;
			this.ContentLength = contentLength;
			this.InputStream = inputStream;
		}

		public FileStream(HttpPostedFileBase postedFile)
			: this(postedFile.FileName, postedFile.ContentType, postedFile.ContentLength, postedFile.InputStream)
		{ }

		/// <summary>
		/// Gets the size of an uploaded file, in bytes.
		/// </summary>
		public int ContentLength { get; private set; }

		/// <summary>
		/// Gets the MIME content type of a file sent by a client.
		/// </summary>
		public string ContentType { get; private set; }
	
		/// <summary>
		/// Gets the fully qualified name of the file on the client.
		/// </summary>
		public string FileName { get; private set; }

		/// <summary>
		/// Gets a System.IO.Stream object that points to an uploaded file to prepare
		/// </summary>
		public Stream InputStream { get; private set; }

		/// <summary>
		/// Disposes the associated <see cref="Stream"/>.
		/// </summary>
		void IDisposable.Dispose()
		{
			InputStream.Dispose();
		}

		/// <summary>
		/// Implicitly converts a <see cref="HttpPostedFileBase"/> into a <see cref="FileStream"/>.
		/// </summary>
		/// <param name="postedFile"></param>
		/// <returns></returns>
		public static implicit operator FileStream(HttpPostedFileBase postedFile)
		{
			return new FileStream(postedFile);
		}
	}
}
