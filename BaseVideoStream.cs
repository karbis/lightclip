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

				curChunk.Position = cursor;
				byte[] sizeArray = new byte[4];
				curChunk.Read(sizeArray, 0, 4);
				uint size = BinaryPrimitives.ReadUInt32BigEndian(sizeArray);

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
				stream.Position = cursor + 24; // read traf header size
				byte[] sizeArray = new byte[4];
				stream.Read(sizeArray, 0, 4);
				uint size = BinaryPrimitives.ReadUInt32BigEndian(sizeArray);

				stream.Position = cursor + 52; // read tfhd offset value
				byte[] offsetArray = new byte[4];
				stream.Read(offsetArray, 0, 4);
				uint offset = BinaryPrimitives.ReadUInt32BigEndian(offsetArray);
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
			chunk.Position = 72; // sample count in trun box
			byte[] countArray = new byte[4];
			chunk.Read(countArray, 0, 4);
			
			return BinaryPrimitives.ReadUInt32BigEndian(countArray);
		}

		protected override void Dispose(bool disposing) {
			closed = true;
			Header.Dispose();
			curChunk.Dispose();

			base.Dispose(true);
		}
	}
}
