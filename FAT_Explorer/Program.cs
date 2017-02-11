using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace FAT_Explorer
{

    class Program
    {
		static void Main(string[] args) {
			FAT16_Explorer f16e = new FAT16_Explorer();
			Lexer lexer = new Lexer();
			String command = "";
			bool getOrder = false;
			if(f16e.initDrive("\\\\.\\H:")) {//&& f16e.workArea()
				command = "The drive has been mounted succesfully. Please Enter Commands: (Help)\n";
				getOrder = true;
			} else {
				command = "There is an error.\n";
				Console.ReadKey();
			}
			while(getOrder) {
				command += "\n" + makeDir(f16e) + ">";
				Console.Write(command);
				command = Console.ReadLine();
				lexer.analyse(command);
				switch(lexer.code) {
					case Code.CHANGE_DIRECTORY:
						f16e.cd(lexer.address);
						command = "";
						break;
					case Code.DIRECTORY:
						command = dir(f16e);
						break;
					case Code.DELETE:
						f16e.delete(lexer.address);
						command = "";
							break;
					case Code.UNDELETE:
						List<ClusterEntryName> deletedFiles = f16e.getDeletedFiles();
						Console.WriteLine("The following files have been removed, please select one to undelete:\n");
						for(int i = 0; i < deletedFiles.Count; i++)
							Console.WriteLine((i + 1) + ". " + deletedFiles[i].name);
						int selected = int.Parse(Console.ReadLine()) - 1;
						if(selected < 0 || deletedFiles.Count <= selected) {
							command = "Input is not correct.\n";
							break;
						} else {
							char c = 'a';
							if(!deletedFiles[selected].hasLFN) {
								Console.Write("A character is needed to complete the name of file, please enter a character: ");
								c = Console.ReadKey().KeyChar;
							}
							if(f16e.undelete(deletedFiles[selected], c))
								command = "\nUndelete has been done succesfully.\n";
						}
						break;
					case Code.BACK_UP:
						System.IO.File.WriteAllBytes(lexer.address, f16e.getFAT(lexer.number));
						command = "";
						break;
					case Code.HELP:
						command = "1. See files:\t\tdir\n" + "2. Change directory:\tcd <Name of Directory>\n" + "3. Delete file:\t\tdel <Name of File>\n4. Undelete file:\tundel: Then follow the instructions.\n" + "5. Back up FAT:\t\tbu <FAT number> <Address of file>\n" +"6. Exit:\t\texit\n";
						break;
					case Code.EXIT:
						f16e.finalizeDrive();
						getOrder = false;
						break;
					case Code.ERROR:
					default:
						command = "";
						break;
				}
			}
		}

		private static string dir(FAT16_Explorer f16e) {
			List<string[]> dir = f16e.dir();
			int maxSizeLength = maxLength(dir, FAT16_Explorer.FILE_SIZE);
			string result = "\n Directory of " + makeDir(f16e) + "\n\n";
			for(int i = 0; i < dir.Count; i++) {
				result += i + "-> " + dir[i][FAT16_Explorer.FILE_YEAR] + "-" + dir[i][FAT16_Explorer.FILE_MONTH] + "-" + dir[i][FAT16_Explorer.FILE_DAY] + "  " + dir[i][FAT16_Explorer.FILE_HOUR] + ":" + dir[i][FAT16_Explorer.FILE_MINUTE] + ":" + dir[i][FAT16_Explorer.FILE_SECOND] + "   " + (dir[i][FAT16_Explorer.FILE_EXTENSION].CompareTo("<DIR>") == 0 ? "<DIR>  " : "       ");
				for(int j = 0; j < maxSizeLength - dir[i][FAT16_Explorer.FILE_SIZE].Length; j++)
					result += " ";
				result += dir[i][FAT16_Explorer.FILE_SIZE] + " " + dir[i][FAT16_Explorer.FILE_LONG_NAME] + (dir[i][FAT16_Explorer.FILE_EXTENSION].CompareTo("<DIR>") != 0 ? "." + dir[i][FAT16_Explorer.FILE_EXTENSION] : "") + "\n";
			}
				return result;
		}

		private static string makeDir(FAT16_Explorer f16e) {
			string res = f16e.directory[0] + ":\\";
			for(int i = 1; i < f16e.directory.Count; i++)
				res += f16e.directory[i] + "\\";
			return res.Length > 3 ? res.Substring(0, res.Length - 1) : res;
		}

		private static int maxLength(List<string[]> dir, int index) {
			int max = 0;
			for(int i = 0; i < dir.Count; i++)
				if(dir[i][index].Length > max)
					max = dir[i][index].Length;
			return max;
		}
    }
}
