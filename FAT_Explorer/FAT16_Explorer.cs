using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace FAT_Explorer {
	class FAT16_Explorer {

		// Importing DLLs
		[DllImport("kernel32.dll",
				   CallingConvention = CallingConvention.Winapi,
				   CharSet = CharSet.Unicode,
				   EntryPoint = "CreateFileW", SetLastError = true)]
		extern static IntPtr CreateFile(string lpFileName,
								 uint dwDesiredAccess,
								 uint dwShareMode,
								 IntPtr lpSecurityAttributes,
								 uint dwCreationDisposition,
								 uint dwFlagsAndAttributes,
								 IntPtr hTemplateFile);

		[DllImport("kernel32.dll", EntryPoint = "SetFilePointerEx")]
		extern static bool SetFilePointerEx(IntPtr hFile, long liDistanceToMove, out long lpNewFilePointer, uint dwMoveMethod);

		[DllImport("kernel32.dll", EntryPoint = "GetLastError", SetLastError = true)]
		extern static int GetLastError();

		[DllImport("kernel32.dll", EntryPoint = "CloseHandle", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		extern static bool CloseHandle(IntPtr hHandle);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool ReadFile(IntPtr hFile, [Out] byte[] lpBuffer,
		   uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool WriteFile(IntPtr hFile, [Out] byte[] lpBuffer,
			uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr overlapped);

		// Public constnats
		public const int FILE_NAME = 0;
		public const int FILE_LONG_NAME = 1;
		public const int FILE_EXTENSION = 2;
		public const int FILE_SIZE = 3;
		public const int FILE_YEAR = 4;
		public const int FILE_MONTH = 5;
		public const int FILE_DAY = 6;
		public const int FILE_HOUR = 7;
		public const int FILE_MINUTE = 8;
		public const int FILE_SECOND = 9;

		// Private constants
		// For functions imported from DLLs
		private const uint GENERIC_ALL = (0x10000000);
		private const uint GENERIC_EXECUTE = (0x20000000);
		private const uint GENERIC_WRITE = (0x40000000);
		private const uint GENERIC_READ = (0x80000000);

		private const uint FILE_SHARE_READ = (0x00000001);
		private const uint FILE_SHARE_WRITE = (0x00000002);
		private const uint FILE_SHARE_DELETE = (0x00000004);

		private const int CREATE_NEW = 1;
		private const int CREATE_ALWAYS = 2;
		private const int OPEN_EXISTING = 3;
		private const int OPEN_ALWAYS = 4;
		private const int TRUNCATE_EXISTING = 5;

		// setDataReaderPointer
		private const uint DRIVE_START = 0;
		private const uint DRIVE_CURRENT = 1;
		private const uint DRIVE_END = 2;

		// findCluster
		private const int CLUSTER_BOOT_SECTOR = -10;
		private const int CLUSTER_FAT_1 = -21;
		private const int CLUSTER_FAT_2 = -22;
		private const int CLUSTER_FAT_3 = -23;
		private const int CLUSTER_ROOT = 0;

		// nextCluster
		private const int CLUSTER_AVAILABLE = 0x0000;
		private const int CLUSTER_RESERVED_1 = 0xFFF0;
		private const int CLUSTER_RESERVED_2 = 0xFFF6;
		private const int CLUSTER_DAMAGED = 0xFFF7;
		private const int CLUSTER_LAST_1 = 0xFFF8;
		private const int CLUSTER_LAST_2 = 0xFFFF;

		// getFileData
		private const string FILE_DELETED = "\ue500FILE_IS_DELETED";
		private const string FILE_HIDDEN = "\ue500FILE_IS_HIDDEN";
		private const string FILE_NO_MORE = "\ue500FILE_NO_MORE";
		private const string FILE_SYSTEM = "\ue500FILE_SYSTEM";

		// getDate
		private const int DATE_YEAR = 0;
		private const int DATE_MONTH = 1;
		private const int DATE_DAY = 2;

		// getTime
		private const int TIME_HOUR = 0;
		private const int TIME_MINUTE = 1;
		private const int TIME_SECOND = 2;

		// Private attributes
		private IntPtr file;
		private int currentCluster;
		private int _lastError;

		private byte[] FAT;

		private int bytesPerSector;
		private int sectorsPerCluster;
		private int reservedSectors;
		private int FATs;
		private int rootEntries;
		private int sectorsPerFAT;

		private List<string> _directory;

		public int lastError {
			set {
				_lastError = value;
			}
			get {
				return _lastError;
			}
		}

		public List<string> directory {
			get{
				return _directory;
			}
		}

		// Constructions
		public FAT16_Explorer() {
			_directory = new List<string>();
			file = IntPtr.Zero;
			bytesPerSector = 0;
			sectorsPerCluster = 0;
			reservedSectors = 0;
			FATs = 0;
			rootEntries = 0;
			sectorsPerFAT = 0;
			lastError = 0;
		}

		// Public functions
		public bool initDrive(string address) {
			if(!createDataRW(address))
				return false;
			directory.Add(address.ElementAt(4) + "");

			// Read Boot Sector
			byte[] buffer = new byte[512];
			if(!readData(buffer))
				return false;

			// Calculate Important Data
			bytesPerSector = (buffer[12] << 8) + buffer[11];
			sectorsPerCluster = buffer[13];
			reservedSectors = (buffer[15] << 8) + buffer[14];
			FATs = buffer[16];
			rootEntries = (buffer[18] << 8) + buffer[17];
			sectorsPerFAT = (buffer[23] << 8) + buffer[22];

			// Reading FAT
			FAT = new byte[bytesPerSector * sectorsPerFAT];
			if(!findCluster(CLUSTER_FAT_1))
				return false;
			if(!readData(FAT))
				return false;

			// File pointer is pointing to root directory
			if(!findCluster(CLUSTER_ROOT))
				return false;
			return true;
		}

		public bool cd(string name) {
			// Preprocess
			int firstCluster = currentCluster;

			// Process
			byte[] clusterBytes = new byte[bytesPerSector * sectorsPerCluster];
			int next;
			do {
				readData(clusterBytes);
				int entry = findEntry(clusterBytes, name);
				if(entry != -1) {
					if(!isDirectory(clusterBytes, entry)) {
						// Postprocess
						findCluster(firstCluster);
						return false;
					}
					int cluster = getCluster(clusterBytes, entry);
					string checkForDir = getName(clusterBytes, entry);
					if(checkForDir.CompareTo("..") == 0)
						directory.RemoveAt(directory.Count - 1);
					else if(checkForDir.CompareTo(".") != 0)
						directory.Add(checkForDir);
					findCluster(cluster);
					return true;
				}
				next = getNextCluster(currentCluster);
			} while(next != CLUSTER_LAST_1 && next != CLUSTER_LAST_2 && findCluster(next));

			// Postprocess
			findCluster(firstCluster);
			return false;
		}

		public List<string[]> dir() {
			// Preprocess
			int firstCluster = currentCluster;

			// Process
			List<string[]> result = new List<string[]>();
			int next;
			do {
				result.AddRange(dirCluster());
				next = getNextCluster(currentCluster);
			} while(next != CLUSTER_LAST_1 && next != CLUSTER_LAST_2 && findCluster(next));

			// Postprocess
			findCluster(firstCluster);
			return result;
		}

		public bool delete(string name) {
			// Preprocess
			int firstCluster = currentCluster;

			// Process
			byte[] clusterBytes;
			if(firstCluster == CLUSTER_ROOT)
				clusterBytes = new byte[32 * rootEntries];
			else
				clusterBytes = new byte[bytesPerSector * sectorsPerCluster];
			int next;
			do {
				int secondCluster = currentCluster;
				readData(clusterBytes);
				int entry = findEntry(clusterBytes, name);
				if(entry != -1) {
					if(isDirectory(clusterBytes, entry)) {
						int dirCluster = getCluster(clusterBytes, entry);
						if(!findCluster(dirCluster)) {
							lastError = GetLastError();
							return false;
						}

						List<string[]> entries = dir();
						for(int i = 0; i < entries.Count; i++) {
							bool b1 = entries[i][FILE_NAME].CompareTo(".") != 0;
							bool b2 = entries[i][FILE_NAME].CompareTo("..") != 0;
							bool b3 = entries[i][FILE_NAME].CompareTo(FILE_DELETED) != 0;
							if(entries[i][FILE_NAME].CompareTo(".") != 0 && entries[i][FILE_NAME].CompareTo("..") != 0 && entries[i][FILE_NAME].CompareTo(FILE_DELETED) != 0)
								if(entries[i][FILE_EXTENSION].CompareTo("<DIR>") == 0)
									delete(entries[i][FILE_LONG_NAME]);
								else
									delete(entries[i][FILE_LONG_NAME] + "." + entries[i][FILE_EXTENSION]);
						}
					}
					clusterBytes[32 * entry] = 0xe5;
					if(!findCluster(secondCluster) || !writeData(clusterBytes)) {
						lastError = GetLastError();
						return false;
					}

					int cluster = getCluster(clusterBytes, entry);
					bool res = deleteCluster(cluster);
					findCluster(firstCluster);
					return res;
				}
				next = getNextCluster(currentCluster);
			} while(next != CLUSTER_LAST_1 && next != CLUSTER_LAST_2 && findCluster(next));

			// Postprocess
			findCluster(firstCluster);
			return false;
		}

		public List<ClusterEntryName> getDeletedFiles() {
			// Preprocess
			int firstCluster = currentCluster;

			// Process
			byte[] clusterBytes;
			if(currentCluster == CLUSTER_ROOT)
				clusterBytes = new byte[32 * rootEntries];
			else
				clusterBytes = new byte[bytesPerSector * sectorsPerCluster];
			// Find deleted files
			List<ClusterEntryName> deletedFiles = new List<ClusterEntryName>();
			int next;
			do {
				readData(clusterBytes);
				for(int i = 0; i < bytesPerSector * sectorsPerCluster / 32; i++) {
					if(clusterBytes[i * 32] == 0)
						break;
					else if(isVolumeName(clusterBytes, i))
						continue;
					else if(clusterBytes[i * 32] == 0xe5 && !isLFNentry(clusterBytes, i)) {
						byte[] name_ = subArray(clusterBytes, i * 32, 8);
						string name = System.Text.Encoding.Default.GetString(name_).Trim();
						deletedFiles.Add(new ClusterEntryName(currentCluster, i, isDirectory(clusterBytes, i) ? name : name + "." + getExtension(clusterBytes, i), isLFNentry(clusterBytes, i - 1)));
					}
				}
				next = getNextCluster(currentCluster);
			} while(next != CLUSTER_LAST_1 && next != CLUSTER_LAST_2 && findCluster(next));

			// Postprocess
			findCluster(firstCluster);
			return deletedFiles;
		}

		public bool undelete(ClusterEntryName cen, char firstChar) {
			// Preprocess
			int firstCluster = currentCluster;

			// Process
			if(!findCluster(cen.cluster)) {
				lastError = GetLastError();
				return false;
			}

			byte[] clusterBytes;
			if(currentCluster == CLUSTER_ROOT)
				clusterBytes = new byte[32 * rootEntries];
			else
				clusterBytes = new byte[bytesPerSector * sectorsPerCluster];
			if(!readData(clusterBytes)) {
				lastError = GetLastError();
				return false;
			}

			if(cen.hasLFN) {
				clusterBytes[32 * cen.entry] = clusterBytes[32 * (cen.entry - 1) + 1];
				int i = 1;
				for(; isLFNentry(clusterBytes, cen.entry - i); i++)
					clusterBytes[32 * (cen.entry - i)] = (byte)i;
				clusterBytes[32 * (cen.entry - i - 1)] += 0x40;
			} else {
				clusterBytes[32 * cen.entry] = (byte) firstChar;
			}
			if(!findCluster(cen.cluster) || !writeData(clusterBytes)) {
				lastError = GetLastError();
				return false;
			}

			int clus = getCluster(clusterBytes, cen.entry);
			FAT[2 * clus] = FAT[2 * clus + 1] = 0xff;
			if(!findCluster(CLUSTER_FAT_1) || !writeData(FAT)) {
				lastError = GetLastError();
				return false;
			}

			// Postprocess
			if(!findCluster(firstCluster)) {
				lastError = GetLastError();
				return false;
			}
			return true;
		}

		public bool makeDir(string name) {
			// Preprocess
			int firstCluster = currentCluster;
			int secondCluster;

			// Process
			byte[] clusterBytes;
			if(currentCluster == CLUSTER_ROOT)
				clusterBytes = new byte[32 * rootEntries];
			else
				clusterBytes = new byte[bytesPerSector * sectorsPerCluster];
			if(!readData(clusterBytes)) {
				lastError = GetLastError();
				return false;
			}
			int itmp = findFreeEntry(clusterBytes);
			if(itmp == -1) {
				if(currentCluster == CLUSTER_ROOT)
					return false;
				itmp = findFreeCluster();
				if(itmp == -1)
					return false;
				setNextCluster(itmp);
				if(!findCluster(itmp)) {
					lastError = GetLastError();
					return false;
				}
				secondCluster = currentCluster;
				clusterBytes = new byte[bytesPerSector * sectorsPerCluster];
				for(int i = 0; i < clusterBytes.Length; i++)
					clusterBytes[i] = 0;
				if(!writeData(clusterBytes)) {
					lastError = GetLastError();
					return false;
				}
				itmp = findFreeEntry(clusterBytes);
			}
			int cluster = findFreeCluster();
			if(cluster == -1)
				return false;

			int repeat = getRepeat(firstCluster, name);
			if(repeat == 0) {
			} else {
			}
			string tmp;
			if(name.Length > 8)
				tmp = name.Substring(0, 6) + "~" + repeat;
			else
				tmp = name;
			int k;
			for(k = 0; k < tmp.Length; k++)
				clusterBytes[32 * itmp + k] = (byte) tmp.ElementAt(k);
			for(int i = k; i < 11; i++)
				clusterBytes[32 * itmp + i] = 0x20;
			clusterBytes[32 * itmp + 0x0b] = 0x10;


				// Postprocess
				if(!findCluster(firstCluster)) {
					lastError = GetLastError();
					return false;
				}
			return true;
		}

		public byte[] getFAT(int index) {
			// Preprocess
			int firstCluster = currentCluster;

			// Process
			switch(index) {
				case 1:
					if(!findCluster(CLUSTER_FAT_1)) {
						lastError = GetLastError();
						if(!findCluster(firstCluster)) {
							lastError = GetLastError();
							return null;
						}
						return null;
					}
					break;
				case 2:
					if(!findCluster(CLUSTER_FAT_2)) {
						lastError = GetLastError();
						if(!findCluster(firstCluster)) {
							lastError = GetLastError();
							return null;
						}
						return null;
					}
					break;
				case 3:
					if(!findCluster(CLUSTER_FAT_3)) {
						lastError = GetLastError();
						if(!findCluster(firstCluster)) {
							lastError = GetLastError();
							return null;
						}
						return null;
					}
					break;
				default:
					return null;
			}

			byte[] fatData = new byte[bytesPerSector * sectorsPerFAT];
			if(!readData(fatData)) {
				lastError = GetLastError();
				return null;
			}

			// Postprocess
			if(!findCluster(firstCluster)) {
				lastError = GetLastError();
				return null;
			}
			return fatData;
		}

		public bool workArea() {
			byte[] buffer = new byte[512];

			// Read Root Directory
			findCluster(CLUSTER_BOOT_SECTOR);
			if(!readData(buffer))
				return false;
			System.IO.File.WriteAllBytes("E:\\zzz\\00.Boot Sector.txt", buffer);

			// Read FAT Directory
			findCluster(CLUSTER_FAT_1);
			for(int i = 0; i < sectorsPerFAT; i++) {
				if(!readData(buffer))
					return false;
				System.IO.File.WriteAllBytes("E:\\zzz\\FAT1\\FAT_" + i + ".txt", buffer);
			}

			// Read Root Directory
			findCluster(CLUSTER_ROOT);
			if(!readData(buffer))
				return false;
			System.IO.File.WriteAllBytes("E:\\zzz\\02.Root.txt", buffer);
			if(!readData(buffer))
				return false;
			System.IO.File.WriteAllBytes("E:\\zzz\\02.Root2.txt", buffer);
			if(!readData(buffer))
				return false;
			System.IO.File.WriteAllBytes("E:\\zzz\\02.Root3.txt", buffer);

			
			findCluster(CLUSTER_ROOT);
			List<string[]> dirRes = dir();
			for(int i = 0; i < dirRes.Count; i++) {
				Console.Write(i + ": ");
				for(int j = 0; j < dirRes[i].Length; j++)
					Console.Write(dirRes[i][j] + ", ");
				Console.WriteLine();
			}
			
			/*
			if(!delete("1")) {
				lastError = GetLastError();
				return false;
			}
			 * */
			/*
			List<ClusterEntryName> cens = getDeletedFiles();
			for(int i = 0; i < cens.Count; i++)
				Console.WriteLine((i + 1) + ". " + cens[i].name + ", Cluster: " + cens[i].cluster.ToString() + ", Entry: " + cens[i].entry.ToString() + ", has lfn: " + cens[i].hasLFN);

			string line = Console.ReadLine();
			undelete(cens[Int16.Parse(line) - 1], '1');
			
			cd("1");

			cens = getDeletedFiles();
			for(int i = 0; i < cens.Count; i++)
				Console.WriteLine((i + 1) + ". " + cens[i].name + ", Cluster: " + cens[i].cluster.ToString() + ", Entry: " + cens[i].entry.ToString() + ", has lfn: " + cens[i].hasLFN);

			line = Console.ReadLine();
			undelete(cens[Int16.Parse(line) - 1], '1');*/
			return true;
		}

		public bool finalizeDrive() {
			return closeDataRW();
		}

		// Private functions
		private bool createDataRW(string address) {
			if(file.Equals(IntPtr.Zero)) {
				file = CreateFile(address, GENERIC_READ | GENERIC_WRITE, 0, IntPtr.Zero, OPEN_ALWAYS, 0, IntPtr.Zero);
				if(file == new IntPtr(-1)) {
					Console.WriteLine("There is an error in openning data R/W.");
					lastError = GetLastError();
					return false;
				}
				return true;
			} else {
				Console.WriteLine("There is an error in openning data R/W.");
				lastError = GetLastError();
				return false;
			}
		}

		private bool setDataRWPointer(long sectors, uint beginning) {
			long ret = 0;
			if(!file.Equals(IntPtr.Zero) && SetFilePointerEx(file, sectors * bytesPerSector, out ret, beginning))
				return true;
			else {
				Console.WriteLine("There is an error in seeking data.");
				lastError = GetLastError();
				return false;
			}
		}

		private bool findCluster(int cluster) {
			int previousCluster = currentCluster;
			currentCluster = cluster;
			if(cluster == CLUSTER_BOOT_SECTOR && setDataRWPointer(0, DRIVE_START))
				return true;
			else if((cluster == CLUSTER_FAT_1 || cluster == CLUSTER_FAT_2 || cluster == CLUSTER_FAT_3) && CLUSTER_FAT_1 - cluster < FATs && setDataRWPointer(reservedSectors + (CLUSTER_FAT_1 - cluster) * sectorsPerFAT, DRIVE_START))
				return true;
			else if(cluster == CLUSTER_ROOT && setDataRWPointer(reservedSectors + FATs * sectorsPerFAT, DRIVE_START))
				return true;
			else if(cluster > 1 && setDataRWPointer(reservedSectors + FATs * sectorsPerFAT + (rootEntries * 32) / bytesPerSector + (cluster - 2) * sectorsPerCluster, DRIVE_START))
				return true;
			else {
				currentCluster = previousCluster;
				return false;
			}
		}

		private int getNextCluster(int cluster) {
			if(cluster == CLUSTER_ROOT)
				return CLUSTER_LAST_1;
			else
				return (FAT[cluster * 2 + 1] << 8) + FAT[cluster * 2];
		}

		private bool readData(byte[] buffer) {
			uint length = 0;
			if(!file.Equals(IntPtr.Zero) && ReadFile(file, buffer, (uint) buffer.Length, out length, IntPtr.Zero))
				return true;
			else {
				Console.WriteLine("There is an error in reading data.");
				lastError = GetLastError();
				return false;
			}
		}

		private bool writeData(byte[] buffer) {
			uint length = 0;
			if(!file.Equals(IntPtr.Zero) && WriteFile(file, buffer, (uint) buffer.Length, out length, IntPtr.Zero))
				return true;
			else {
				Console.WriteLine("There is an error in writing data.");
				lastError = GetLastError();
				return false;
			}
		}

		// Special functions for dir(): START
		private List<string[]> dirCluster() {
			byte[] clusterBytes;
			if(currentCluster == CLUSTER_ROOT)
				clusterBytes = new byte[32 * rootEntries];
			else
				clusterBytes = new byte[bytesPerSector * sectorsPerCluster];
			readData(clusterBytes);

			List<string[]> result = new List<string[]>();

			for(int i = 0; i < clusterBytes.Length; i += 32) {
				if(isVolumeName(clusterBytes, i / 32))
					continue;
				string[] temp = getFileData(clusterBytes, i);
				if(temp[0].Equals(FILE_DELETED) || temp[0].Equals(FILE_HIDDEN))
					continue;
				else if(temp[0].Equals(FILE_NO_MORE))
					break;
				else
					result.Add(temp);
			}
			return result;
		}

		private string[] getFileData(byte[] buffer, int beginIndex) {
			// Return special values
			if(buffer[beginIndex] == 229) { // File is deleted
				string[] minResult = new string[1];
				minResult[0] = FILE_DELETED;
				return minResult;
			}
			if((buffer[beginIndex + 8 + 3] & (1 << 1)) != 0) { // File is hidden
				string[] minResult = new string[1];
				minResult[0] = FILE_HIDDEN;
				return minResult;
			}
			if(buffer[beginIndex] == 0) { // There is no more file
				string[] minResult = new string[1];
				minResult[0] = FILE_NO_MORE;
				return minResult;
			}
			// Make byte[]s
			byte[] name_ = subArray(buffer, beginIndex, 8);
			byte[] attribute_ = subArray(buffer, beginIndex + 8 + 3, 1);
			byte[] reserved_ = subArray(buffer, beginIndex + 8 + 3 + 1, 10);
			byte[] time_ = subArray(buffer, beginIndex + 8 + 3 + 1 + 10, 2);
			byte[] date_ = subArray(buffer, beginIndex + 8 + 3 + 1 + 10 + 2, 2);
			byte[] firstCluster_ = subArray(buffer, beginIndex + 8 + 3 + 1 + 10 + 2 + 2, 2);
			byte[] size_ = subArray(buffer, beginIndex + 8 + 3 + 1 + 10 + 2 + 2 + 2, 4);

			// Calculations
			int[] time = getTime(time_);
			int[] date = getDate(date_);
			int size = size_[3] * 256 * 256 * 256 + size_[2] * 256 * 256 + size_[1] * 256 + size_[0];

			// Make result[]
			string[] result = new string[10];
			result[FILE_NAME] = System.Text.Encoding.Default.GetString(name_).Trim();
			result[FILE_LONG_NAME] = getName(buffer, beginIndex / 32);
			result[FILE_EXTENSION] = getExtension(buffer, beginIndex / 32);
			result[FILE_SIZE] = isDirectory(buffer, beginIndex/32) ? "" : size.ToString();
			result[FILE_YEAR] = date[DATE_YEAR].ToString();
			result[FILE_MONTH] = (date[DATE_MONTH] < 10 ? "0" : "") + date[DATE_MONTH].ToString();
			result[FILE_DAY] = (date[DATE_DAY] < 10 ? "0" : "") + date[DATE_DAY].ToString();
			result[FILE_HOUR] = (time[TIME_HOUR] < 10 ? "0" : "") + time[TIME_HOUR].ToString();
			result[FILE_MINUTE] = (time[TIME_MINUTE] < 10 ? "0" : "") + time[TIME_MINUTE].ToString();
			result[FILE_SECOND] = (time[TIME_SECOND] < 10 ? "0" : "") + time[TIME_SECOND].ToString();
			return result;
		}

		private byte[] subArray(byte[] data, int index, int length) {
			byte[] result = new byte[length];
			try {
				Array.Copy(data, index, result, 0, length);
			} catch {
				Console.WriteLine("There is an exception.");
			}
			return result;
		}

		private int[] getTime(byte[] time_) {
			int[] result = new int[3];
			result[TIME_SECOND] = 0;
			result[TIME_MINUTE] = 0;
			result[TIME_HOUR] = 0;
			result[TIME_SECOND] = time_[0];
			if(result[TIME_SECOND] >= 128) {
				result[TIME_SECOND] -= 128;
				result[TIME_MINUTE] += 4;
			}
			if(result[TIME_SECOND] >= 64) {
				result[TIME_SECOND] -= 64;
				result[TIME_MINUTE] += 2;
			}
			if(result[TIME_SECOND] >= 32) {
				result[TIME_SECOND] -= 32;
				result[TIME_MINUTE] += 1;
			}
			result[TIME_SECOND] *= 2;
			if((time_[1] & (1 << 0)) != 0) {
				result[TIME_MINUTE] += 8;
			}
			if((time_[1] & (1 << 1)) != 0) {
				result[TIME_MINUTE] += 16;
			}
			if((time_[1] & (1 << 2)) != 0) {
				result[TIME_MINUTE] += 32;
			}
			if((time_[1] & (1 << 3)) != 0) {
				result[TIME_HOUR] += 1;
			}
			if((time_[1] & (1 << 4)) != 0) {
				result[TIME_HOUR] += 2;
			}
			if((time_[1] & (1 << 5)) != 0) {
				result[TIME_HOUR] += 4;
			}
			if((time_[1] & (1 << 6)) != 0) {
				result[TIME_HOUR] += 8;
			}
			if((time_[1] & (1 << 7)) != 0) {
				result[TIME_HOUR] += 16;
			}
			return result;
		}

		private int[] getDate(byte[] date_) {
			int[] result = new int[3];
			result[DATE_DAY] = date_[0];
			result[DATE_MONTH] = 0;
			result[DATE_YEAR] = 0;
			if(result[DATE_DAY] >= 128) {
				result[DATE_DAY] -= 128;
				result[DATE_MONTH] += 4;
			}
			if(result[DATE_DAY] >= 64) {
				result[DATE_DAY] -= 64;
				result[DATE_MONTH] += 2;
			}
			if(result[DATE_DAY] >= 32) {
				result[DATE_DAY] -= 32;
				result[DATE_MONTH] += 1;
			}
			if((date_[DATE_MONTH] & (1 << 0)) != 0) {
				result[DATE_MONTH] += 8;
			}
			if((date_[DATE_MONTH] & (1 << 1)) != 0) {
				result[DATE_YEAR] += 1;
			}
			if((date_[DATE_MONTH] & (1 << 2)) != 0) {
				result[DATE_YEAR] += 2;
			}
			if((date_[DATE_MONTH] & (1 << 3)) != 0) {
				result[DATE_YEAR] += 4;
			}
			if((date_[DATE_MONTH] & (1 << 4)) != 0) {
				result[DATE_YEAR] += 8;
			}
			if((date_[DATE_MONTH] & (1 << 5)) != 0) {
				result[DATE_YEAR] += 16;
			}
			if((date_[DATE_MONTH] & (1 << 6)) != 0) {
				result[DATE_YEAR] += 32;
			}
			if((date_[DATE_MONTH] & (1 << 7)) != 0) {
				result[DATE_YEAR] += 64;
			}
			result[DATE_YEAR] += 1980;
			return result;
		}
		// Special functions for dir(): END

		// Special functions for delete(string): START
		private bool deleteCluster(int cluster) {
			do {
				FAT[2 * cluster] = FAT[2 * cluster + 1] = 0;
				cluster = getNextCluster(cluster);
			} while(cluster != CLUSTER_LAST_1 && cluster != CLUSTER_LAST_2);

			int previousCluster = currentCluster;
			if(!findCluster(CLUSTER_FAT_1) || !writeData(FAT)) {
				findCluster(previousCluster);
				return false;
			}

			findCluster(previousCluster);
			return true;
		}
		// Special functions for delete(string): END

		// Special functions for findFreeCluster(): START
		private int findFreeCluster() {
			for(int i = 0; i < FAT.Length / 2; i++)
				if(FAT[2 * i] == 0 && FAT[2 * i + 1] == 0)
					return i;
			return -1;
		}

		private int findFreeEntry(byte[] clusterBytes) {
			for(int i = 0; i < clusterBytes.Length; i += 32)
				if(clusterBytes[i] == 0)
					return i / 32;
			for(int i = 0; i < clusterBytes.Length; i += 32)
				if(clusterBytes[i] == 0xe5)
					return i / 32;
			return -1;
		}

		private void setNextCluster(int next) {
			FAT[2 * currentCluster] = (byte) (next >> 8);
			FAT[2 * currentCluster] = (byte) (next & 0x00ff);
		}

		private int getRepeat(int cluster, string name) {
			// Preprocess
			int firstCluster = currentCluster;

			// Process
			int repeat = 0;

			// Postprocess
			if(!findCluster(firstCluster)) {
				lastError = GetLastError();
				return -1;
			}
			return repeat;
		}

		// Special functions for findFreeCluster(): END

		private int getCluster(byte[] clusterBytes, int entry) {
			return (clusterBytes[32 * entry + 0x1B] << 8) + clusterBytes[32 * entry + 0x1A];
		}

		private bool isDirectory(byte[] clusterBytes, int entry) {
			return (clusterBytes[entry * 32 + 0x0B] & (1 << 4)) == 16; // 16 = 0001 0000
		}

		private bool isVolumeName(byte[] clusterBytes, int entry) {
			if(entry * 32 + 0x0B >= clusterBytes.Length)
				return false;
			else
				return (clusterBytes[entry * 32 + 0x0B] & (1 << 3)) == 8; // 16 = 0000 1000
		}

		private string getName(byte[] clusterBytes, int entry) {
			if(clusterBytes[32 * entry] == 0)
				return FILE_NO_MORE;
			else if(isLFNentry(clusterBytes, entry))
				return FILE_SYSTEM;
			else {
				string name = "";
				if(isLFNentry(clusterBytes, entry - 1) && !isLFNentry(clusterBytes, entry)) {
					Stack<byte[]> nameStack = new Stack<byte[]>();
					for(int i = 1; entry - i >= 0 && isLFNentry(clusterBytes, entry - i); i++)
						nameStack.Push(clusterBytes.ToList().GetRange(32 * (entry - i), 32).ToArray());
					while(nameStack.Count > 0) {
						byte[] bTmp = nameStack.Pop();
						name = Encoding.Unicode.GetString(bTmp.ToList().GetRange(1, 10).ToArray()) +
								Encoding.Unicode.GetString(bTmp.ToList().GetRange(14, 12).ToArray()) +
								Encoding.Unicode.GetString(bTmp.ToList().GetRange(28, 4).ToArray()) + name;
					}
					name = name.Remove(name.LastIndexOf('\0'));
					if(!isDirectory(clusterBytes, entry))
						name = name.Remove(name.LastIndexOf('.'));
				} else
					name = System.Text.Encoding.Default.GetString(subArray(clusterBytes, 32 * entry, 8));
				if(name.ElementAt(0) == 0x05) {
					name = 0xe5 + name.Remove(0, 1);
				}
				return name.Trim();
			}
		}

		private bool isLFNentry(byte[] clusterBytes, int entry) {
			if(entry >= 0 && clusterBytes[32 * entry + 11] == 0x0f)
				return true;
			return false;
		}

		private string getExtension(byte[] clusterBytes, int entry) {
			return isDirectory(clusterBytes, entry) ? "<DIR>" : System.Text.Encoding.Default.GetString(subArray(clusterBytes, 32 * entry + 8, 3)).Trim().ToLowerInvariant();
		}

		private int findEntry(byte[] clusterBytes, string name) {
			for(int i = 0; i < clusterBytes.Length; i += 32) {
				if(!isVolumeName(clusterBytes, i) && ((!isDirectory(clusterBytes, i / 32) && name.CompareTo(getName(clusterBytes, i / 32) + "." + getExtension(clusterBytes, i / 32)) == 0) || (isDirectory(clusterBytes, i / 32) && name.CompareTo(getName(clusterBytes, i / 32)) == 0)))
					return i / 32;
				else if(clusterBytes[i] == 0)
					return -1;
			}
			return -1;
		}

		private bool closeDataRW() {
			if(!file.Equals(IntPtr.Zero) && CloseHandle(file)) {
				file = IntPtr.Zero;
				return true;
			} else {
				Console.WriteLine("There is an error in closing data R/W.");
				lastError = GetLastError();
				return false;
			}
		}
	}

	class ClusterEntryName {

		private string _s;
		private int _e;
		private int _c;
		private bool _hasLFN;

		public string name {
			set {
				_s = value;
			}
			get {
				return _s;
			}
		}

		public int cluster {
			set {
				_c = value;
			}
			get {
				return _c;
			}
		}

		public int entry {
			set {
				_e = value;
			}
			get {
				return _e;
			}
		}

		public bool hasLFN {
			set {
				_hasLFN = value;
			}
			get {
				return _hasLFN;
			}
		}

		public ClusterEntryName(int cluster, int entry, string name, bool hasLFN) {
			this.name = name;
			this.cluster = cluster;
			this.entry = entry;
			this.hasLFN = hasLFN;
		}
	}
}
