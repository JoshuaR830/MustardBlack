using System;
using System.Collections.Generic;
using System.IO;
using MustardBlack.Hosting;

namespace MustardBlack.ViewEngines.Razor.Build
{
	public sealed class TestFileSystem : IFileSystem
	{
		readonly string root;

		public TestFileSystem(string root)
		{
			this.root = root;
		}

		public string GetFullPath(string path)
		{
			if (path.StartsWith("~/"))
				path = path.Substring(2);

			if (Path.IsPathRooted(path))
				return path;
			
			return Path.GetFullPath(Path.Combine(this.root, path));
		}

		public bool Exists(string path)
		{
			var fullPath = this.GetFullPath(path);

			if (File.Exists(fullPath))
				return true;

			if (Directory.Exists(fullPath))
				return true;

			return false;
		}

		public IEnumerable<string> GetFiles(string folderPath)
		{
			if (!Directory.Exists(folderPath))
				return new string[0];

			return Directory.GetFiles(folderPath);
		}

		public TResult Read<TResult>(string path, Func<StreamReader, TResult> streamAction)
		{
			path = this.GetFullPath(path);

			using (var streamReader = new StreamReader(path))
				return streamAction(streamReader);
		}

		public DateTime GetLastWriteTime(string path)
		{
			var fullPath = this.GetFullPath(path);
			return File.GetLastWriteTimeUtc(fullPath);
		}

		public void Write(Stream stream, string path)
		{
			path = this.GetFullPath(path);

			using (var fileStream = File.Create(path))
			{
				stream.Seek(0, SeekOrigin.Begin);
				stream.CopyTo(fileStream);
			}
		}
	}
}
