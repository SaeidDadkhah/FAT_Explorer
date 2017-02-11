using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FAT_Explorer {

	enum Code {CHANGE_DIRECTORY, DIRECTORY, DELETE, UNDELETE, MAKE_DIRECTORY, BACK_UP, HELP, EXIT, ERROR
	};

	class Lexer {

		private Code _code;
		private int _number;
		private string _address;

		public Code code {
			set {
				_code = value;
			}
			get {
				return _code;
			}
		}
		public int number {
			set {
				_number = value;
			}
			get {
				if(code == Code.BACK_UP)
					return _number;
				return 0;
			}
		}
		public string address {
			set {
				_address = value;
			}
			get {
				if(code == Code.CHANGE_DIRECTORY || code == Code.DELETE || code == Code.UNDELETE || code == Code.BACK_UP || code == Code.MAKE_DIRECTORY)
					return _address;
				else
					return null;
			}
		}

		// Constructors
		public Lexer() {
		}

		// Functions
		public void analyse(string command) {
			command = command.Trim();
			int index = command.IndexOf(' ');
			string str = command.Substring(0, index > 0 ? index : command.Length).Trim().ToLower();
			if(str.CompareTo("cd") == 0) {
				index = command.IndexOf(' ');
				if(index > 1) {
					code = Code.CHANGE_DIRECTORY;
					address = command.Substring(index, command.Length - index).Trim();
				} else
					code = Code.ERROR;
			} else if(str.CompareTo("dir") == 0) {
				code = Code.DIRECTORY;
			} else if(str.CompareTo("del") == 0) {
				index = command.IndexOf(' ');
				if(index > 2) {
					code = Code.DELETE;
					address = command.Substring(index, command.Length - index).Trim();
				} else
					code = Code.ERROR;
			} else if(str.CompareTo("undel") == 0) {
				code = Code.UNDELETE;
			} else if(str.CompareTo("md") == 0) {
				index = command.IndexOf(' ');
				if(index > 2) {
					code = Code.MAKE_DIRECTORY;
					address = command.Substring(index, command.Length - index).Trim();
				} else
					code = Code.ERROR;
			} else if(str.CompareTo("bu") == 0) {
				command = command.Substring(index, command.Length - index).Trim();
				index = command.IndexOf(' ');
				if(index > 0) {
					str = command.Substring(0, index).Trim().ToLower();
					number = int.Parse(str);
					index = command.IndexOf(' ');
					if(index > 0) {
						code = Code.BACK_UP;
						address = command.Substring(index, command.Length - index).Trim();
					} else
						code = Code.ERROR;
				} else
					code = Code.ERROR;
			} else if(str.CompareTo("help") == 0) {
				code = Code.HELP;
			} else if(str.CompareTo("exit") == 0) {
				code = Code.EXIT;
			} else
				code = Code.ERROR;
		}


	}
}
