using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lightclip {
	public class VideoDiskStream : BaseVideoStream {
		FileStream firstBuffer;
		FileStream secondBuffer;
		FileStream curBuffer;
		static string directory = Path.GetDirectoryName(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath);
		Dictionary<FileStream, long> sampleCounts = new();
		long curSampleCount = 0;
		const int BUFFER_SIZE = 128 * 1024;

		public VideoDiskStream() {
			clearBufferFiles();

			firstBuffer = new FileStream(Path.Combine(directory, "buffer1.bin"), FileMode.Create, FileAccess.ReadWrite, FileShare.Read, BUFFER_SIZE);
			secondBuffer = new FileStream(Path.Combine(directory, "buffer2.bin"), FileMode.Create, FileAccess.ReadWrite, FileShare.Read, BUFFER_SIZE);
			curBuffer = firstBuffer;
			sampleCounts[firstBuffer] = 0;
			sampleCounts[secondBuffer] = 0;
		}

		internal override void WriteChunk(MemoryStream chunk, uint sampleSize) {
			chunk.Position = 0;
			chunk.CopyTo(curBuffer, BUFFER_SIZE);
			curBuffer.Flush();
			sampleCounts[curBuffer] += sampleSize;
			curSampleCount += sampleSize;

			if (curSampleCount >= MaxFrameCount) {
				curSampleCount = 0;

				curBuffer = getOtherBuffer();
				deletedChunkOffset += curBuffer.Length;
				FrameCount -= sampleCounts[curBuffer];
				curBuffer.SetLength(0);
				curBuffer.Position = 0;
				curBuffer.Flush();
				sampleCounts[curBuffer] = 0;
			}
		}

		public override FileStream GetFinalStream() {
			FileStream stream = new FileStream(Path.Combine(directory, "temp_buffer.bin"),
				FileMode.Create, FileAccess.ReadWrite, FileShare.Read, BUFFER_SIZE, FileOptions.DeleteOnClose);
			FileStream otherBuffer = getOtherBuffer();

			// race condition hell
			Header.Position = 0;
			Header.CopyTo(stream);
			stream.Flush();
			otherBuffer.Position = 0;
			otherBuffer.CopyTo(stream, BUFFER_SIZE);
			otherBuffer.Position = otherBuffer.Length;
			stream.Flush();
			curBuffer.Position = 0;
			curBuffer.CopyTo(stream, BUFFER_SIZE);
			curBuffer.Position = curBuffer.Length;
			stream.Flush();
			stream.Position = 0;

			// fix offsets
			long cursor = Header.Length;
			while (cursor < stream.Length) {
				offsetTfhdHeaders(stream, cursor);

				// skip over moof and mdat box
				for (int i = 0; i < 2; i++) {
					stream.Position = cursor;
					byte[] sizeArray = new byte[4];
					stream.Read(sizeArray, 0, 4);
					uint size = BinaryPrimitives.ReadUInt32BigEndian(sizeArray);
					cursor += size;
				}
			}

			return stream;
		}

		private FileStream getOtherBuffer() {
			return (curBuffer == firstBuffer) ? secondBuffer : firstBuffer;
		}

		private void clearBufferFiles() {
			foreach (string fileName in (string[])["buffer1.bin", "buffer2.bin", "temp_buffer.bin"]) {
				string path = Path.Combine(directory, fileName);
				if (File.Exists(path)) {
					File.Delete(path);
				}
			}
		}

		public void CloseHandles() {
			firstBuffer.Dispose();
			secondBuffer.Dispose();
			curBuffer = null;
			sampleCounts.Clear();
			clearBufferFiles();
		}

		protected override void Dispose(bool disposing) {
			CloseHandles();
			base.Dispose(disposing);
		}
	}
}
