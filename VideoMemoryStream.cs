using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lightclip {
	public class VideoMemoryStream : BaseVideoStream {
		public List<MemoryStream> Chunks = new();

		public override MemoryStream GetFinalStream() {
			MemoryStream stream = new MemoryStream();

			Header.Position = 0;
			Header.CopyTo(stream);

			foreach (MemoryStream chunk in Chunks.ToList()) {
				chunk.Position = 0;
				long curPosition = stream.Position;
				chunk.CopyTo(stream);

				// moof header has absolute byte location in one of its values, so it has to be offsetted to account for the deleted chunks
				offsetTfhdHeaders(stream, curPosition);
			}

			return stream;
		}

		internal override void WriteChunk(MemoryStream chunk, uint sampleSize) {
			Chunks.Add(chunk);

			while (Chunks.Count > 1 && FrameCount - getSampleCount(Chunks[0]) > MaxFrameCount) {
				deletedChunkOffset += Chunks[0].Length;
				FrameCount -= getSampleCount(Chunks[0]);
				Chunks[0].Dispose();
				Chunks.RemoveAt(0);
			}
		}

		protected override void Dispose(bool disposing) {
			foreach (MemoryStream chunk in Chunks) {
				chunk.Dispose();
			}
			Chunks.Clear();
			Chunks.TrimExcess();

			base.Dispose(true);
		}
	}
}
