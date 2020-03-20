﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using BizHawk.Common.BufferExtensions;
using BizHawk.Common.IOExtensions;


namespace BizHawk.Emulation.Cores.Nintendo.NES
{
	/// <summary>
	/// at least it's not iNES 2.0...
	/// </summary>
	public class Unif
	{
		CartInfo ci = new CartInfo();
		byte[] prgrom;
		byte[] chrrom;

		Dictionary<string, byte[]> chunks = new Dictionary<string, byte[]>();

		void TryAdd(Stream s, string key)
		{
			if (!chunks.TryGetValue(key, out var data))
				return;
			s.Write(data, 0, data.Length);
		}

		public Unif(Stream s)
		{
			BinaryReader br = new BinaryReader(s, Encoding.ASCII);

			if (!Encoding.ASCII.GetBytes("UNIF").SequenceEqual(br.ReadBytes(4)))
				throw new Exception("Missing \"UNIF\" header mark!");
			int ver = br.ReadInt32();
			//if (ver != 7)
			//	throw new Exception($"Unknown UNIF version {ver}!");
			Console.WriteLine("Processing Version {0} UNIF...", ver);
			br.ReadBytes(32 - 4 - 4);

			while (br.PeekChar() > 0)
			{
				string chunkid = Encoding.ASCII.GetString(br.ReadBytes(4));
				int length = br.ReadInt32();
				byte[] chunkdata = br.ReadBytes(length);
				chunks.Add(chunkid, chunkdata);
			}

			MemoryStream prgs = new MemoryStream();
			MemoryStream chrs = new MemoryStream();
			for (int i = 0; i < 16; i++)
			{
				TryAdd(prgs, $"PRG{i:X1}");
				TryAdd(chrs, $"CHR{i:X1}");
			}
			prgs.Close();
			chrs.Close();
			prgrom = prgs.ToArray();
			chrrom = chrs.ToArray();

			ci.PrgSize = (short)(prgrom.Length / 1024);
			ci.ChrSize = (short)(chrrom.Length / 1024);

			if (chunks.TryGetValue("MIRR", out var tmp))
			{
				switch (tmp[0])
				{
					case 0: // hmirror
						ci.PadH = 0;
						ci.PadV = 1;
						break;
					case 1: // vmirror
						ci.PadH = 1;
						ci.PadV = 0;
						break;
				}
			}

			if (chunks.TryGetValue("MAPR", out tmp))
			{
				ci.BoardType = new BinaryReader(new MemoryStream(tmp)).ReadStringUtf8NullTerminated();
			}

			ci.BoardType = ci.BoardType.TrimEnd('\0');
			ci.BoardType = "UNIF_" + ci.BoardType;

			if (chunks.TryGetValue("BATR", out tmp))
			{
				// apparently, this chunk just existing means battery is yes
				ci.WramBattery = true;
			}

			// is there any way using System.Security.Cryptography.SHA1 to compute the hash of
			// prg concatentated with chr?  i couldn't figure it out, so this implementation is dumb
			{
				MemoryStream ms = new MemoryStream();
				ms.Write(prgrom, 0, prgrom.Length);
				ms.Write(chrrom, 0, chrrom.Length);
				ms.Close();
				byte[] all = ms.ToArray();
				ci.Sha1 = "sha1:" + all.HashSHA1(0, all.Length);
			}

			// other code will expect this
			if (chrrom.Length == 0)
				chrrom = null;
		}

		public CartInfo CartInfo => ci;
		public byte[] PRG => prgrom;
		public byte[] CHR => chrrom;
	}
}
