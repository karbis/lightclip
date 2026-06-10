using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lightclip {
	public class VideoMemoryStream : BaseVideoStream {
		public List<MemoryStream> Chunks = new();
		int deletionRange = 1;
		uint deletedSamples = 0;

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

		bool firstChunk = true;
		internal override void WriteChunk(MemoryStream chunk, uint sampleSize) {
			Chunks.Add(chunk);

			if (firstChunk) {
				firstChunk = false;
				deletedSamples = getSampleCount(chunk);
			}

			while (Chunks.Count - deletedSamples > 1 && FrameCount - deletedSamples - getSampleCount(Chunks[deletionRange]) > MaxFrameCount) {
				uint sampleCount = getSampleCount(Chunks[deletionRange]);
				bool isSync = getSyncSample(Chunks[deletionRange], sampleCount) != -1;

				if (isSync) {
					// dont leave non-sync frames at the start of the stream to not cause any sync issues
					for (int i = 0; i < deletionRange; i++) {
						deletedChunkOffset += Chunks[0].Length;
						FrameCount -= getSampleCount(Chunks[0]);
						Chunks[0].Dispose();
						Chunks.RemoveAt(0);
					}

					deletionRange = 1;
					deletedSamples = sampleCount;
				} else {
					deletionRange++;
					deletedSamples += sampleCount;
				}
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
