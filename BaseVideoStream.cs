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
	public abstract class BaseVideoStream : MemoryStream {
		public MemoryStream Header = new MemoryStream();
		internal MemoryStream curChunk = new MemoryStream();
		internal bool chunking = false;
		internal long deletedChunkOffset = 0;
		internal long totalChunkSize = 0;
		public long TotalFrameCount = 0;
		public long FrameCount = 0;
		public long MaxFrameCount = 0;
		public event EventHandler OnOverflow;
		public event EventHandler OnChunkWritten;
		internal bool closed = false;

		public override void Write(byte[] buffer, int offset, int count) {
			if (closed) return;
			if (!chunking) {
				chunking = Encoding.ASCII.GetString(buffer, 4, 4) == "moof"; // checks start of moof box, header should end by then
			}

			if (!chunking) {
				Header.Write(buffer);
				return;
			}

			long cursor = 0;
			string curChunkType = "";
			bool fullChunk = false;
			while (cursor < curChunk.Length) {
				if (curChunkType == "mdat") {
					fullChunk = true; // chunk is longer than moof + mdat header, so its complete
					break;
				}

				uint size = getUintAtPosition(curChunk, cursor);

				byte[] typeArray = new byte[4];
				curChunk.Read(typeArray, 0, 4);
				curChunkType = Encoding.ASCII.GetString(typeArray, 0, 4);
				cursor += size;
			}

			curChunk.Position = curChunk.Length;
			curChunk.Write(buffer);
			if (fullChunk) {
				// split chunk in 2, complete and not complete
				byte[] data = curChunk.GetBuffer();
				MemoryStream chunk = new MemoryStream();
				chunk.Write(data, 0, (int)cursor);

				MemoryStream newChunk = new MemoryStream();
				newChunk.Write(data, (int)cursor, (int)(curChunk.Length - cursor));
				curChunk.Dispose();
				curChunk = newChunk;

				uint sampleCount = getSampleCount(chunk);
				FrameCount += sampleCount;
				TotalFrameCount += sampleCount;
				totalChunkSize += chunk.Length;

				WriteChunk(chunk, sampleCount);

				if (totalChunkSize > 2.14e9) { // cant have the int overflow
					Debug.WriteLine("overflow");
					OnOverflow?.Invoke(this, null);
					closed = true;
					//Dispose();
					return;
				}

				OnChunkWritten?.Invoke(this, null);
			}
		}

		public override long Seek(long offset, SeekOrigin loc) {
			return offset; // crash fix (by lying)
		}

		public abstract Stream GetFinalStream();
		internal abstract void WriteChunk(MemoryStream chunk, uint sampleSize);

		internal void offsetTfhdHeaders(Stream stream, long basePosition) {
			long cursor = basePosition;
			while (true) {
				uint size = getUintAtPosition(stream, cursor + 24); // read traf header size
				uint offset = getUintAtPosition(stream, cursor + 52); // read tfhd offset value
				byte[] newValue = new byte[4];
				BinaryPrimitives.WriteUInt32BigEndian(newValue, (uint)(offset - deletedChunkOffset));
				stream.Position = cursor + 52;
				stream.Write(newValue, 0, 4);

				// check if next box is mdat or another traf
				stream.Position = cursor + size + 24 + 4;
				byte[] typeArray = new byte[4];
				stream.Read(typeArray, 0, 4);
				string boxType = Encoding.ASCII.GetString(typeArray, 0, 4);

				if (boxType != "traf") break;
				cursor += size;
			}

			stream.Position = stream.Length;
		}

		internal uint getSampleCount(MemoryStream chunk) {
			return getUintAtPosition(chunk, 72); // sample count in trun box
		}

		internal int getSyncSample(Stream chunk, uint sampleCount, long offset = 0) {
			int sample = 0;
			long cursor = 0;
			while (sample < sampleCount) { // iterate over sample data in trun box
				uint flags = getUintAtPosition(chunk, cursor + 88 + offset);

				// sync frame
				if ((flags & 0x00010000) == 0) {
					return sample;
				}

				cursor += 16;
				sample++;
			}

			return -1;
		}

		public uint GetNearestSyncSample(Stream stream, long goalSample) {
			long cursor = Header.Length;
			long sampleCount = 0;
			long nearestSyncSample = 0;

			while (cursor < stream.Length && sampleCount < goalSample) {
				uint moofSize = getUintAtPosition(stream, cursor);
				uint chunkSamples = getUintAtPosition(stream, cursor + 72);

				int syncSample = getSyncSample(stream, chunkSamples, cursor);
				if (syncSample != -1) {
					nearestSyncSample = sampleCount + syncSample;
				}
				sampleCount += chunkSamples;

				uint mdatSize = getUintAtPosition(stream, cursor + moofSize);
				cursor += moofSize + mdatSize;
			}

			stream.Position = 0;
			return (uint)nearestSyncSample;
		}

		internal uint getUintAtPosition(Stream chunk, long pos) {
			chunk.Position = pos;
			byte[] uintArray = new byte[4];
			chunk.Read(uintArray, 0, 4);
			return BinaryPrimitives.ReadUInt32BigEndian(uintArray);
		}

		protected override void Dispose(bool disposing) {
			closed = true;
			Header.Dispose();
			curChunk.Dispose();

			base.Dispose(true);
		}
	}
}
