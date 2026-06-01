using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lightclip {
	public class VideoMemoryStream : MemoryStream {
		public MemoryStream Header = new MemoryStream();
		public List<MemoryStream> Chunks = new();
		MemoryStream curChunk = new MemoryStream();
		bool chunking = false;
		long deletedChunkOffset = 0;
		public long TotalFrameCount = 0;
		public long FrameCount = 0;
		public long MaxFrameCount = 0;
		public event EventHandler OnUnexpectedDisposal;
		public event EventHandler OnChunkWritten;

		public override void Write(byte[] buffer, int offset, int count) {
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
				byte[] data = curChunk.ToArray();
				MemoryStream chunk = new MemoryStream();
				chunk.Write(data, 0, (int)cursor);

				MemoryStream newChunk = new MemoryStream();
				newChunk.Write(data, (int)cursor, (int)(curChunk.Length - cursor));
				curChunk.Dispose();
				curChunk = newChunk;

				uint sampleCount = getSampleCount(chunk);
				Chunks.Add(chunk);
				FrameCount += sampleCount;
				TotalFrameCount += sampleCount;

				while (FrameCount - getSampleCount(Chunks[0]) > MaxFrameCount) {
					deletedChunkOffset += Chunks[0].Length;
					FrameCount -= getSampleCount(Chunks[0]);
					Chunks[0].Dispose();
					Chunks.RemoveAt(0);
				}

				if (deletedChunkOffset > 4e9) { // cant have the uint overflow. realistically this would take a while to happen, probably over a day
					Debug.WriteLine("overflow");
					OnUnexpectedDisposal?.Invoke(this, null);
					Dispose();
					return;
				}

				OnChunkWritten?.Invoke(this, null);
			}
		}

		public MemoryStream GetFinalStream() {
			MemoryStream stream = new MemoryStream();

			Header.Position = 0;
			Header.CopyTo(stream);

			//bool isFirst = true;
			foreach (MemoryStream chunk in Chunks.ToList()) {
				chunk.Position = 0;
				long curPosition = stream.Position;
				chunk.CopyTo(stream);

				/*// set first sample flag in first trun
				if (isFirst && false) {
					stream.Position = curPosition + 68;
					byte[] flagArray = new byte[4];
					stream.Read(flagArray, 0, 4);
					uint offset = BinaryPrimitives.ReadUInt32BigEndian(flagArray);
					Debug.WriteLine(offset);
					offset |= 0x000004;
					Debug.WriteLine(offset);

					byte[] newValue = new byte[4];
					BinaryPrimitives.WriteUInt32BigEndian(newValue, offset);
					stream.Position = curPosition + 68;
					stream.Write(newValue, 0, 4);
				}
				isFirst = false;*/

				// moof header has absolute byte location in one of its values, so it has to be offsetted to account for the deleted chunks
				offsetTfhdHeaders(stream, curPosition);
			}

			return stream;
		}

		private void offsetTfhdHeaders(MemoryStream stream, long basePosition) {
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

		private uint getSampleCount(MemoryStream chunk) {
			chunk.Position = 72; // sample count in trun box
			byte[] countArray = new byte[4];
			chunk.Read(countArray, 0, 4);
			
			return BinaryPrimitives.ReadUInt32BigEndian(countArray);
		}

		protected override void Dispose(bool disposing) {
			Header.Dispose();
			foreach (MemoryStream chunk in Chunks.ToList()) {
				chunk.Dispose();
			}
			Chunks.Clear();
			Chunks.TrimExcess();
			curChunk.Dispose();
			OnChunkWritten?.Invoke(this, null);

			base.Dispose(disposing);
		}
	}
}
